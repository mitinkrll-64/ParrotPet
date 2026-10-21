using System;
using System.Windows;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Физика и перемещение попугая. Не знает про окна и таймеры:
/// снаружи вызывают Update(dt) каждый кадр и читают X, Y, Size, FacingScale.
/// X/Y — левый верхний угол логического хитбокса (квадрат Size x Size) в WPF-единицах (DIP).
/// Окно рисования больше хитбокса на поле Pad со всех сторон, чтобы клюв и хвост не обрезались.
/// </summary>
public class MovementService
{
    /// <summary>Сторона логического хитбокса при Scale = 1.</summary>
    public const double BaseSize = 64;

    /// <summary>Сторона холста рисования при Scale = 1 (хитбокс + поля).</summary>
    public const double CanvasSize = 96;

    /// <summary>Длительность клевания (3 наклона), секунд.</summary>
    public const double EatDuration = 1.8;

    private const double BaseWalkSpeed = 70;    // пикселей в секунду
    private const double Gravity = 1800;        // пикселей/с²
    private const double BounceDamping = 0.35;  // доля скорости после отскока
    private const double MinBounceSpeed = 150;  // ниже — попугай «прилипает» к земле
    private const double BaseFlyDuration = 1.6; // секунд на перелёт
    private const double HoverBobAmplitude = 4; // амплитуда покачивания в парении
    private const double FlyArcHeight = 90;     // насколько дуга поднимается над траекторией
    private const double FlyWobbleAmp = 10;     // амплитуда волнообразного колебания по Y
    private const double FlyWobbleHz = 1.8;     // частота этого колебания

    /// <summary>Где клюв касается земли при наклоне (в долях хитбокса 64): вправо / при развороте влево.</summary>
    private const double BeakPeckX = 68.4;
    private const double BeakPeckXMirrored = BaseSize - BeakPeckX;

    private readonly Random _random = new();
    private readonly ScreenService _screens;
    private readonly SettingsModel _settings;
    private readonly FoodService _food;

    private double _walkTargetX;
    private bool _goingToFood;
    private bool _eatingFood;
    private double _velocityY;
    private double _eatLeft;

    // Параметры полёта: квадратичная кривая Безье P0 -> P1 (контрольная) -> P2
    private Point _flyStart, _flyControl, _flyEnd;
    private double _flyTime;
    private double _hoverLeft;
    private double _hoverTime;
    private double _descendTime, _descendDuration, _descendFrom;

    private double _dragGrabX, _dragGrabY;

    public MovementService(ScreenService screens, SettingsModel settings, FoodService food)
    {
        _screens = screens;
        _settings = settings;
        _food = food;
    }

    /// <summary>Приземление; аргумент — скорость удара о землю.</summary>
    public event Action<double>? Landed;

    /// <summary>Попугай доклевал зерно (после третьего клевка).</summary>
    public event Action? FoodEaten;

    public PetBehaviorState State { get; private set; } = PetBehaviorState.Idle;
    public FlightPhase Phase { get; private set; } = FlightPhase.Cruise;

    /// <summary>Левый верхний угол хитбокса.</summary>
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>Скорость за последний кадр (используется для наклона в полёте).</summary>
    public double VelocityX { get; private set; }
    public double VelocityY { get; private set; }

    /// <summary>1 — смотрит вправо, -1 — влево (для ScaleX спрайта).</summary>
    public double FacingScale { get; private set; } = 1;

    /// <summary>Сторона квадратного хитбокса с учётом масштаба.</summary>
    public double Size => BaseSize * _settings.Scale;

    /// <summary>Сторона окна рисования с учётом масштаба.</summary>
    public double WindowSize => CanvasSize * _settings.Scale;

    /// <summary>Поле между краем окна и хитбоксом.</summary>
    public double Pad => (CanvasSize - BaseSize) / 2 * _settings.Scale;

    private double Speed => _settings.BehaviorSpeed;

    /// <summary>Область перемещения (рабочая область монитора или виртуальный экран).</summary>
    private Rect Bounds => _screens.GetBounds(_settings.SelectedScreen);

    /// <summary>«Земля» — верхняя кромка панели задач под персонажем.</summary>
    private double GroundY => _screens.GetGroundBottom(_settings.SelectedScreen, X + Size / 2) - Size;

    private double MinX => Bounds.Left;
    private double MaxX => Math.Max(Bounds.Left, Bounds.Right - Size);

    /// <summary>Ставит попугая на землю по центру выбранной области.</summary>
    public void PlaceOnGround()
    {
        var b = Bounds;
        X = b.Left + (b.Width - Size) / 2;
        Y = GroundY;
        State = PetBehaviorState.Idle;
    }

    /// <summary>Настройки изменились (монитор, масштаб): возвращаем персонажа в допустимые границы.</summary>
    public void OnSettingsChanged()
    {
        var b = Bounds;
        double centerX = X + Size / 2;
        _goingToFood = false;
        _eatingFood = false;

        if (centerX < b.Left || centerX > b.Right)
        {
            // Выбрали другой монитор — «роняем» попугая в его центр сверху
            X = b.Left + (b.Width - Size) / 2;
            Y = b.Top;
            if (State != PetBehaviorState.Dragged) Fall();
            return;
        }

        X = Math.Clamp(X, MinX, MaxX);
        Y = Math.Clamp(Y, b.Top, Math.Max(b.Top, GroundY));
        if (State is PetBehaviorState.Walk or PetBehaviorState.Eat) State = PetBehaviorState.Idle;
        else if (State == PetBehaviorState.Fly) Fall();
    }

    /// <summary>
    /// Принудительно прерывает любое действие (отладка): сбрасывает флаги кормления
    /// и сажает попугая на землю в Idle. Дальше FSM работает как обычно.
    /// </summary>
    public void Interrupt()
    {
        _goingToFood = false;
        _eatingFood = false;
        _velocityY = 0;
        State = PetBehaviorState.Idle;
        X = Math.Clamp(X, MinX, MaxX);
        Y = GroundY;
    }

    /// <summary>Поднимает попугая над землёй и роняет (отладка падения).</summary>
    public void ForceFall()
    {
        Interrupt();
        Y = Math.Max(Bounds.Top, GroundY - 220);
        Fall();
    }

    /// <summary>Переносит попугая в центр области перемещения; оттуда он падает на землю.</summary>
    public void TeleportToCenter()
    {
        Interrupt();
        var b = Bounds;
        X = b.Left + (b.Width - Size) / 2;
        Y = b.Top + (b.Height - Size) / 2;
        Fall();
    }

    /// <summary>Идти на deltaX пикселей (знак задаёт направление).</summary>
    public void StartWalk(double deltaX)
    {
        if (State != PetBehaviorState.Idle) return;
        _walkTargetX = Math.Clamp(X + deltaX, MinX, MaxX);
        if (Math.Abs(_walkTargetX - X) < 1) return;
        FacingScale = _walkTargetX > X ? 1 : -1;
        State = PetBehaviorState.Walk;
    }

    /// <summary>Взлететь в случайную точку области и парить несколько секунд.</summary>
    public void StartFly()
    {
        if (State != PetBehaviorState.Idle) return;

        var b = Bounds;
        double targetX = MinX + _random.NextDouble() * (MaxX - MinX);
        double minY = b.Top;
        double maxY = Math.Max(minY, GroundY - 2 * Size);
        double targetY = minY + _random.NextDouble() * (maxY - minY);

        _flyStart = new Point(X, Y);
        _flyEnd = new Point(targetX, targetY);
        // Контрольная точка выше траектории — получается плавная дуга вверх
        _flyControl = new Point((X + targetX) / 2, Math.Max(b.Top, Math.Min(Y, targetY) - FlyArcHeight));
        _flyTime = 0;
        _hoverLeft = 3 + _random.NextDouble() * 3;
        _hoverTime = 0;
        FacingScale = targetX >= X ? 1 : -1;
        Phase = FlightPhase.Cruise;
        State = PetBehaviorState.Fly;
    }

    /// <summary>Начало перетаскивания: запоминаем, за какую точку хитбокса взяли попугая.</summary>
    public void BeginDrag(double grabX, double grabY)
    {
        _dragGrabX = grabX;
        _dragGrabY = grabY;
        _velocityY = 0;
        _goingToFood = false;
        _eatingFood = false;
        State = PetBehaviorState.Dragged;
    }

    /// <summary>Перемещение при перетаскивании; cursorX/Y — позиция курсора в координатах экрана.</summary>
    public void DragTo(double cursorX, double cursorY)
    {
        if (State != PetBehaviorState.Dragged) return;
        X = Math.Clamp(cursorX - _dragGrabX, MinX, MaxX);
        Y = Math.Clamp(cursorY - _dragGrabY, Bounds.Top, Math.Max(Bounds.Top, GroundY));
    }

    /// <summary>Отпустили мышь: в воздухе — падаем, на земле — сидим.</summary>
    public void EndDrag()
    {
        if (State != PetBehaviorState.Dragged) return;
        if (Y < GroundY - 0.5)
        {
            Fall();
        }
        else
        {
            Y = GroundY;
            State = PetBehaviorState.Idle;
        }
    }

    /// <summary>Шаг физики; dt — время кадра в секундах.</summary>
    public void Update(double dt)
    {
        double prevX = X, prevY = Y;

        switch (State)
        {
            case PetBehaviorState.Idle:
                SnapToGround();
                TryGoToFood();
                break;
            case PetBehaviorState.Walk: UpdateWalk(dt); break;
            case PetBehaviorState.Fly: UpdateFly(dt); break;
            case PetBehaviorState.Fall: UpdateFall(dt); break;
            case PetBehaviorState.Eat: UpdateEat(dt); break;
        }

        if (dt > 0)
        {
            VelocityX = (X - prevX) / dt;
            VelocityY = (Y - prevY) / dt;
        }
    }

    private void Fall()
    {
        _velocityY = 0;
        State = PetBehaviorState.Fall;
    }

    private void SnapToGround()
    {
        // Следим за панелью задач: если её сдвинули или сменили масштаб, остаёмся на ней
        Y = GroundY;
        X = Math.Clamp(X, MinX, MaxX);
    }

    /// <summary>Если на земле лежит зерно — разворачиваемся и идём к нему клювом.</summary>
    private void TryGoToFood()
    {
        if (!_food.IsLanded) return;

        double scale = Size / BaseSize;
        bool toRight = _food.X >= X + Size / 2;
        double stopX = Math.Clamp(_food.X - (toRight ? BeakPeckX : BeakPeckXMirrored) * scale, MinX, MaxX);
        FacingScale = toRight ? 1 : -1;

        if (Math.Abs(stopX - X) < 1.5)
        {
            X = stopX;
            BeginEat();
        }
        else
        {
            _walkTargetX = stopX;
            _goingToFood = true;
            State = PetBehaviorState.Walk;
        }
    }

    private void BeginEat()
    {
        _eatLeft = EatDuration;
        _eatingFood = true;
        State = PetBehaviorState.Eat;
    }

    private void UpdateEat(double dt)
    {
        SnapToGround();
        _eatLeft -= dt;
        if (_eatLeft > 0) return;

        State = PetBehaviorState.Idle;
        if (_eatingFood)
        {
            _eatingFood = false;
            FoodEaten?.Invoke();
        }
    }

    private void UpdateWalk(double dt)
    {
        double step = BaseWalkSpeed * Speed * dt;
        double remaining = _walkTargetX - X;
        if (Math.Abs(remaining) <= step)
        {
            X = _walkTargetX;
            if (_goingToFood)
            {
                _goingToFood = false;
                BeginEat();
            }
            else
            {
                State = PetBehaviorState.Idle;
            }
        }
        else
        {
            X += Math.Sign(remaining) * step;
        }
        Y = GroundY; // в режиме «Все экраны» земля зависит от X
    }

    private void UpdateFly(double dt)
    {
        switch (Phase)
        {
            case FlightPhase.Cruise:
            {
                double duration = BaseFlyDuration / Speed;
                _flyTime = Math.Min(duration, _flyTime + dt);
                double raw = _flyTime / duration;
                double t = raw * raw * (3 - 2 * raw); // smoothstep: плавный разгон и торможение
                double inv = 1 - t;
                X = inv * inv * _flyStart.X + 2 * inv * t * _flyControl.X + t * t * _flyEnd.X;
                Y = inv * inv * _flyStart.Y + 2 * inv * t * _flyControl.Y + t * t * _flyEnd.Y;
                // Волнообразное колебание; в начале и конце пути гаснет, чтобы не было скачков
                Y += Math.Sin(_flyTime * 2 * Math.PI * FlyWobbleHz) * FlyWobbleAmp * Math.Sin(Math.PI * raw);
                Y = Math.Max(Y, Bounds.Top); // колебание не выводит попугая за верх экрана
                if (_flyTime >= duration)
                {
                    Phase = FlightPhase.Hover;
                    _hoverTime = 0;
                }
                break;
            }

            case FlightPhase.Hover:
                // Зависание: лёгкое покачивание вверх-вниз вокруг точки прилёта
                _hoverTime += dt;
                _hoverLeft -= dt;
                Y = _flyEnd.Y + Math.Sin(_hoverTime * 4) * HoverBobAmplitude;
                if (_hoverLeft <= 0)
                {
                    Phase = FlightPhase.Descend;
                    _descendTime = 0;
                    _descendFrom = Y;
                    _descendDuration = Math.Clamp(Math.Max(0, GroundY - Y) / 300, 0.8, 2.5);
                }
                break;

            default: // Descend: планирование с плавным сбросом скорости перед касанием
            {
                _descendTime += dt;
                double r = Math.Min(1, _descendTime / _descendDuration);
                double e = r * r * (3 - 2 * r);
                Y = _descendFrom + (GroundY - _descendFrom) * e;
                if (r >= 1)
                {
                    Y = GroundY;
                    State = PetBehaviorState.Idle;
                    Landed?.Invoke(150); // мягкое касание
                }
                break;
            }
        }
    }

    private void UpdateFall(double dt)
    {
        _velocityY += Gravity * dt;
        Y += _velocityY * dt;

        double ground = GroundY;
        if (Y < ground) return;

        Y = ground;
        if (_velocityY > MinBounceSpeed)
        {
            Landed?.Invoke(_velocityY);
            _velocityY = -_velocityY * BounceDamping; // пружинящее приземление
        }
        else
        {
            _velocityY = 0;
            State = PetBehaviorState.Idle;
        }
    }
}