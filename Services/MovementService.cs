using System;
using System.Windows;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Физика и перемещение попугая. Не знает про окна и таймеры:
/// снаружи вызывают Update(dt) каждый кадр и читают X, Y, Size, FacingScale.
/// Все координаты — в WPF-единицах (DIP). Спрайт всегда квадратный.
/// </summary>
public class MovementService
{
    /// <summary>Базовая сторона квадратного спрайта при Scale = 1.</summary>
    public const double BaseSize = 64;

    /// <summary>Длительность кормления, секунд.</summary>
    public const double EatDuration = 1.8;

    private const double BaseWalkSpeed = 70;    // пикселей в секунду
    private const double Gravity = 1800;        // пикселей/с²
    private const double BounceDamping = 0.35;  // доля скорости после отскока
    private const double MinBounceSpeed = 150;  // ниже — попугай «прилипает» к земле
    private const double BaseFlyDuration = 1.6; // секунд на перелёт
    private const double HoverBobAmplitude = 4; // амплитуда покачивания в парении
    private const double FlyArcHeight = 90;     // насколько дуга поднимается над траекторией

    private readonly Random _random = new();
    private readonly ScreenService _screens;
    private readonly SettingsModel _settings;

    private double _walkTargetX;
    private double _velocityY;
    private double _eatLeft;

    // Параметры полёта: квадратичная кривая Безье P0 -> P1 (контрольная) -> P2
    private Point _flyStart, _flyControl, _flyEnd;
    private double _flyTime;
    private double _hoverLeft;
    private double _hoverTime;

    private double _dragGrabX, _dragGrabY;

    public MovementService(ScreenService screens, SettingsModel settings)
    {
        _screens = screens;
        _settings = settings;
    }

    /// <summary>Приземление; аргумент — скорость удара о землю.</summary>
    public event Action<double>? Landed;

    public PetBehaviorState State { get; private set; } = PetBehaviorState.Idle;
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>1 — смотрит вправо, -1 — влево (для ScaleX спрайта).</summary>
    public double FacingScale { get; private set; } = 1;

    /// <summary>Сторона квадрата с учётом масштаба.</summary>
    public double Size => BaseSize * _settings.Scale;

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
        State = PetBehaviorState.Fly;
    }

    /// <summary>Покормить: несколько наклонов вниз.</summary>
    public void StartEat()
    {
        if (State != PetBehaviorState.Idle) return;
        _eatLeft = EatDuration;
        State = PetBehaviorState.Eat;
    }

    /// <summary>Начало перетаскивания: запоминаем, за какую точку взяли окно.</summary>
    public void BeginDrag(double grabX, double grabY)
    {
        _dragGrabX = grabX;
        _dragGrabY = grabY;
        _velocityY = 0;
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
        switch (State)
        {
            case PetBehaviorState.Idle: UpdateIdle(); break;
            case PetBehaviorState.Walk: UpdateWalk(dt); break;
            case PetBehaviorState.Fly: UpdateFly(dt); break;
            case PetBehaviorState.Fall: UpdateFall(dt); break;
            case PetBehaviorState.Eat: UpdateEat(dt); break;
        }
    }

    private void Fall()
    {
        _velocityY = 0;
        State = PetBehaviorState.Fall;
    }

    private void UpdateIdle()
    {
        // Следим за панелью задач: если её сдвинули или сменили масштаб, остаёмся на ней
        Y = GroundY;
        X = Math.Clamp(X, MinX, MaxX);
    }

    private void UpdateEat(double dt)
    {
        UpdateIdle();
        _eatLeft -= dt;
        if (_eatLeft <= 0) State = PetBehaviorState.Idle;
    }

    private void UpdateWalk(double dt)
    {
        double step = BaseWalkSpeed * Speed * dt;
        double remaining = _walkTargetX - X;
        if (Math.Abs(remaining) <= step)
        {
            X = _walkTargetX;
            State = PetBehaviorState.Idle;
        }
        else
        {
            X += Math.Sign(remaining) * step;
        }
        Y = GroundY; // в режиме «Все экраны» земля зависит от X
    }

    private void UpdateFly(double dt)
    {
        double duration = BaseFlyDuration / Speed;
        if (_flyTime < duration)
        {
            _flyTime = Math.Min(duration, _flyTime + dt);
            double t = _flyTime / duration;
            t = t * t * (3 - 2 * t); // smoothstep: плавный разгон и торможение
            double inv = 1 - t;
            X = inv * inv * _flyStart.X + 2 * inv * t * _flyControl.X + t * t * _flyEnd.X;
            Y = inv * inv * _flyStart.Y + 2 * inv * t * _flyControl.Y + t * t * _flyEnd.Y;
            return;
        }

        // Парение: лёгкое покачивание вверх-вниз вокруг точки прилёта
        _hoverTime += dt;
        _hoverLeft -= dt;
        Y = _flyEnd.Y + Math.Sin(_hoverTime * 4) * HoverBobAmplitude;
        if (_hoverLeft <= 0) Fall();
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