using System;
using System.Windows;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Физика и перемещение попугая. Не знает про окна WPF и таймеры:
/// снаружи вызывают Update(dt) каждый кадр и читают X, Y, Size, FacingScale.
/// X/Y — левый верхний угол логического хитбокса (квадрат Size x Size) в WPF-единицах (DIP);
/// низ хитбокса — это «лапы». Окно рисования больше хитбокса на поле Pad со всех сторон.
/// Опоры: панель задач (земля), верхняя кромка чужого окна (Perch) и боковая кромка (Cling).
/// </summary>
public class MovementService
{
    /// <summary>Сторона логического хитбокса при Scale = 1.</summary>
    public const double BaseSize = 64;

    /// <summary>Сторона холста рисования при Scale = 1 (хитбокс + поля).</summary>
    public const double CanvasSize = 96;

    /// <summary>Длительность клевания (3 наклона), секунд.</summary>
    public const double EatDuration = 1.8;

    private const double BaseWalkSpeed = 70;     // пикселей в секунду
    private const double WalkAccel = 380;        // разгон при начале ходьбы, px/с²
    private const double WalkDecel = 520;        // торможение перед остановкой, px/с²
    private const double Gravity = 1800;         // пикселей/с²
    private const double TerminalVelocity = 1300; // предел скорости падения
    private const double BaseFlyDuration = 1.0;  // секунд на перелёт минимальной длины
    private const double FlyArcHeight = 70;      // насколько дуга поднимается над траекторией
    private const double FlyWobbleAmp = 6;       // амплитуда волнообразного колебания по Y
    private const double FlyWobbleHz = 1.8;      // частота этого колебания
    private const double ApproachFrom = 0.72;    // с какой доли пути начинается заход на посадку
    private const double LandTolerance = 8;      // насколько ниже кромки окна ещё «ловим» падающего попугая

    // Геометрия висения на боку окна (при Scale = 1): птица повёрнута головой вверх,
    // лапы у кромки, тело свисает вверх от лап на ~38 px, хвост вниз на ~28 px.
    private const double ClingOverlap = 0;       // лапки заходят за кромку: пальцы видно «обхватившими» край окна
    private const double ClingTopMargin = 62;    // от верха окна до лап (спрайт приподнят на 24 px, голова выше лап на ~38 px)
    private const double ClingBottomMargin = 16; // от низа окна до лап в самой нижней точке (хвост свисает ниже видимых лап)
    private const double ClingHopMargin = 14;    // лапы на этой глубине под кромкой — можно запрыгнуть наверх
    private const double ClimbSpeed = 26;        // скорость переползания, px/с

    /// <summary>Где клюв касается земли при наклоне (в долях хитбокса 64): вправо / при развороте влево.</summary>
    private const double BeakPeckX = 70.9;
    private const double BeakPeckXMirrored = BaseSize - BeakPeckX;

    private readonly Random _random = new();
    private readonly ScreenService _screens;
    private readonly SettingsModel _settings;
    private readonly FoodService _food;
    private readonly WindowSurfaceService _surfaces;

    private double _walkTargetX;
    private double _walkSpeed;
    private bool _goingToFood;
    private bool _eatingFood;
    private double _velocityX, _velocityY;
    private double _eatLeft;

    // Опора-окно: на нём сидим (Perch). IntPtr.Zero — стоим на панели задач
    private IntPtr _perchWindow;
    private double _perchLeft, _perchTop;

    // Висение на боку окна
    private IntPtr _clingWindow;
    private double _clingLeft;          // сколько ещё висеть, секунд
    private double _clingTargetFeet;    // куда переползаем (Y лап)
    private double _clingPause;         // отдых между переползаниями
    private bool _clingHop;             // в конце запрыгнуть наверх, иначе сорваться
    private bool _clingExiting;

    // Полёт: квадратичная кривая Безье P0 -> P1 (контрольная) -> P2
    private Point _flyStart, _flyControl, _flyEnd;
    private double _flyTime, _flyDuration;
    private IntPtr _flyWindow;
    private Rect _flyWindowBounds;

    private double _dragGrabX, _dragGrabY;

    public MovementService(ScreenService screens, SettingsModel settings, FoodService food, WindowSurfaceService surfaces)
    {
        _screens = screens;
        _settings = settings;
        _food = food;
        _surfaces = surfaces;
    }

    /// <summary>Приземление; аргумент — скорость удара о опору.</summary>
    public event Action<double>? Landed;

    /// <summary>Попугай доклевал зерно (после третьего клевка).</summary>
    public event Action? FoodEaten;

    public PetBehaviorState State { get; private set; } = PetBehaviorState.Idle;
    public FlightPhase Phase { get; private set; } = FlightPhase.Cruise;

    /// <summary>Куда летит попугай (имеет смысл в состоянии Fly).</summary>
    public LandingKind FlyTarget { get; private set; } = LandingKind.Ground;

    /// <summary>Доля пройденного пути полёта 0..1.</summary>
    public double FlightProgress => _flyDuration > 0 ? Math.Clamp(_flyTime / _flyDuration, 0, 1) : 0;

    /// <summary>Попугай сидит на верхней кромке окна, а не на панели задач.</summary>
    public bool IsPerched => _perchWindow != IntPtr.Zero;

    /// <summary>Левый верхний угол хитбокса.</summary>
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>Скорость за последний кадр (используется для наклона в полёте).</summary>
    public double VelocityX { get; private set; }
    public double VelocityY { get; private set; }

    /// <summary>1 — смотрит вправо, -1 — влево (для ScaleX спрайта). При висении смотрит на окно.</summary>
    public double FacingScale { get; private set; } = 1;

    /// <summary>Сторона квадратного хитбокса с учётом масштаба.</summary>
    public double Size => BaseSize * _settings.Scale;

    /// <summary>Сторона окна рисования с учётом масштаба.</summary>
    public double WindowSize => CanvasSize * _settings.Scale;

    /// <summary>Поле между краем окна и хитбоксом.</summary>
    public double Pad => (CanvasSize - BaseSize) / 2 * _settings.Scale;

    private double Scale => _settings.Scale;
    private double Speed => _settings.BehaviorSpeed;

    /// <summary>Область перемещения (рабочая область монитора или виртуальный экран).</summary>
    private Rect Bounds => _screens.GetBounds(_settings.SelectedScreen);

    /// <summary>«Земля» — верхняя кромка панели задач под персонажем.</summary>
    private double GroundY => GroundAt(X + Size / 2);

    private double GroundAt(double centerX) => _screens.GetGroundBottom(_settings.SelectedScreen, centerX) - Size;

    /// <summary>Y хитбокса на текущей опоре: верх окна или панель задач.</summary>
    private double SupportY => IsPerched ? _perchTop - Size : GroundY;

    private double MinX => Bounds.Left;
    private double MaxX => Math.Max(Bounds.Left, Bounds.Right - Size);

    /// <summary>Границы хождения: на окне центр не заходит за его края.</summary>
    private double WalkMinX => IsPerched ? Math.Max(MinX, _perchLeft - Size / 2 + Size * 0.15) : MinX;
    private double WalkMaxX => IsPerched ? Math.Min(MaxX, _perchLeft + _perchWidth - Size / 2 - Size * 0.15) : MaxX;
    private double _perchWidth;

    /// <summary>Ставит попугая на землю по центру выбранной области.</summary>
    public void PlaceOnGround()
    {
        var b = Bounds;
        LeaveSupport();
        X = b.Left + (b.Width - Size) / 2;
        Y = GroundY;
        State = PetBehaviorState.Idle;
    }

    /// <summary>Настройки изменились (монитор, масштаб): возвращаем персонажа в допустимые границы.</summary>
    public void OnSettingsChanged()
    {
        var b = Bounds;
        double centerX = X + Size / 2;
        bool wasOnWindow = IsPerched;
        _goingToFood = false;
        _eatingFood = false;
        LeaveSupport();

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
        else if (State is PetBehaviorState.Fly or PetBehaviorState.Cling) Fall();
        else if (wasOnWindow && State == PetBehaviorState.Idle) Fall();
    }

    /// <summary>
    /// Принудительно прерывает любое действие (отладка): сбрасывает флаги кормления
    /// и сажает попугая на землю в Idle. Дальше FSM работает как обычно.
    /// </summary>
    public void Interrupt()
    {
        _goingToFood = false;
        _eatingFood = false;
        _velocityX = 0;
        _velocityY = 0;
        LeaveSupport();
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
        _walkTargetX = Math.Clamp(X + deltaX, WalkMinX, WalkMaxX);
        if (Math.Abs(_walkTargetX - X) < 1) return;
        BeginWalk();
    }

    private void BeginWalk()
    {
        FacingScale = _walkTargetX > X ? 1 : -1;
        _walkSpeed = 0; // трогаемся с места плавно
        State = PetBehaviorState.Walk;
    }

    /// <summary>Взлететь: случайно на окно сверху, к боку окна или на панель задач.</summary>
    public void StartFly()
    {
        double roll = _random.NextDouble();
        StartFly(roll < 0.35 ? LandingKind.WindowTop : roll < 0.6 ? LandingKind.WindowSide : LandingKind.Ground);
    }

    /// <summary>
    /// Взлететь и лететь прямо к точке посадки — без зависания в воздухе.
    /// Если подходящего окна нет, садится на панель задач.
    /// </summary>
    public void StartFly(LandingKind kind)
    {
        if (State != PetBehaviorState.Idle) return;
        if (kind != LandingKind.Ground && PlanWindowFlight(kind)) return;
        PlanGroundFlight();
    }

    private void PlanGroundFlight()
    {
        double targetX = MinX + _random.NextDouble() * (MaxX - MinX);
        BeginFlight(LandingKind.Ground, new Point(targetX, GroundAt(targetX + Size / 2)), IntPtr.Zero, default, 0);
    }

    /// <summary>Подбирает случайное окно и точку на нём; false — подходящих окон нет.</summary>
    private bool PlanWindowFlight(LandingKind kind)
    {
        var b = Bounds;
        var windows = _surfaces.GetWindows();
        if (windows.Count == 0) return false;

        int start = _random.Next(windows.Count);
        for (int n = 0; n < windows.Count; n++)
        {
            int i = (start + n) % windows.Count;
            var w = windows[i];
            var r = w.Bounds;
            if (w.Handle == _perchWindow) continue; // с этого окна летим на другое
            if (r.Right < b.Left || r.Left > b.Right) continue;

            if (kind == LandingKind.WindowTop)
            {
                if (r.Top - Size < b.Top || r.Top > GroundY + Size) continue;
                double lo = Math.Max(r.Left + Size * 0.3, b.Left + Size / 2);
                double hi = Math.Min(r.Right - Size * 0.3, b.Right - Size / 2);
                if (hi <= lo) continue;
                double cx = lo + _random.NextDouble() * (hi - lo);
                if (_surfaces.IsCovered(windows, i, cx, r.Top + 2)) continue;
                BeginFlight(kind, new Point(cx - Size / 2, r.Top - Size), w.Handle, r, 0);
                return true;
            }

            // Боковая кромка: слева от окна (стена справа от птицы, dir = +1) или справа (dir = -1)
            double minFeet = Math.Max(r.Top + ClingTopMargin * Scale, b.Top + Size);
            double maxFeet = Math.Min(r.Bottom - ClingBottomMargin * Scale, GroundY + Size);
            if (maxFeet - minFeet < 20 * Scale) continue;
            double feet = minFeet + _random.NextDouble() * (maxFeet - minFeet);

            double dir = _random.Next(2) == 0 ? 1 : -1;
            for (int side = 0; side < 2; side++, dir = -dir)
            {
                double x = ClingX(r, dir);
                if (x < MinX || x > MaxX) continue;
                double wallX = dir > 0 ? r.Left - 3 : r.Right + 3;
                if (_surfaces.IsCovered(windows, i, wallX, feet - 20 * Scale)) continue;
                BeginFlight(kind, new Point(x, feet - Size), w.Handle, r, dir);
                return true;
            }
        }
        return false;
    }

    /// <summary>X хитбокса при висении: dir = +1 — птица слева от окна, лапы на его левой кромке.</summary>
    private double ClingX(Rect r, double dir) =>
        dir > 0 ? r.Left - Size / 2 + ClingOverlap * Scale : r.Right - Size / 2 - ClingOverlap * Scale;

    private void BeginFlight(LandingKind kind, Point end, IntPtr window, Rect windowBounds, double wallDir)
    {
        _flyStart = new Point(X, Y);
        _flyEnd = end;
        double dx = end.X - X, dy = end.Y - Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        double top = Bounds.Top;
        if (kind == LandingKind.WindowSide)
        {
            // Заход вдоль стены: последний участок почти горизонтальный, носом к окну
            double lead = Math.Clamp(Math.Abs(dx) * 0.5, 50, 220);
            _flyControl = new Point(end.X - wallDir * lead, Math.Max(top, end.Y - 20));
        }
        else
        {
            // Контрольная точка выше траектории — получается дуга: подъём и плавный спуск
            double arc = Math.Clamp(FlyArcHeight + dist * 0.1, FlyArcHeight, 150);
            _flyControl = new Point((X + end.X) / 2, Math.Max(top, Math.Min(Y, end.Y) - arc));
        }

        _flyDuration = Math.Clamp(BaseFlyDuration + dist / 600, 1.0, 2.2) / Speed;
        _flyTime = 0;
        _flyWindow = window;
        _flyWindowBounds = windowBounds;
        FlyTarget = kind;
        Phase = FlightPhase.Cruise;
        FacingScale = kind == LandingKind.WindowSide ? wallDir : (Math.Abs(dx) < 1 ? FacingScale : Math.Sign(dx));
        _wallDir = wallDir;

        LeaveSupport();
        State = PetBehaviorState.Fly;
    }

    private double _wallDir;

    /// <summary>Начало перетаскивания: запоминаем, за какую точку хитбокса взяли попугая.</summary>
    public void BeginDrag(double grabX, double grabY)
    {
        _dragGrabX = grabX;
        _dragGrabY = grabY;
        _velocityX = 0;
        _velocityY = 0;
        _goingToFood = false;
        _eatingFood = false;
        LeaveSupport();
        State = PetBehaviorState.Dragged;
    }

    /// <summary>Перемещение при перетаскивании; cursorX/Y — позиция курсора в координатах экрана.</summary>
    public void DragTo(double cursorX, double cursorY)
    {
        if (State != PetBehaviorState.Dragged) return;
        X = Math.Clamp(cursorX - _dragGrabX, MinX, MaxX);
        Y = Math.Clamp(cursorY - _dragGrabY, Bounds.Top, Math.Max(Bounds.Top, GroundY));
    }

    /// <summary>Отпустили мышь: в воздухе — падаем (и садимся на окно, если попали на него), на земле — сидим.</summary>
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
                if (SnapToSupport()) TryGoToFood();
                break;
            case PetBehaviorState.Walk: UpdateWalk(dt); break;
            case PetBehaviorState.Fly: UpdateFly(dt); break;
            case PetBehaviorState.Fall: UpdateFall(dt); break;
            case PetBehaviorState.Eat: UpdateEat(dt); break;
            case PetBehaviorState.Cling: UpdateCling(dt); break;
        }

        if (dt > 0)
        {
            VelocityX = (X - prevX) / dt;
            VelocityY = (Y - prevY) / dt;
        }
    }

    /// <summary>Падение; vx/vy — начальная скорость (например, прыжок или срыв с окна).</summary>
    private void Fall(double vx = 0, double vy = 0)
    {
        _velocityX = vx;
        _velocityY = vy;
        LeaveSupport();
        State = PetBehaviorState.Fall;
    }

    /// <summary>Забываем окно-опору и окно-стену.</summary>
    private void LeaveSupport()
    {
        _perchWindow = IntPtr.Zero;
        _clingWindow = IntPtr.Zero;
    }

    private void EnterPerch(IntPtr window, Rect r)
    {
        _perchWindow = window;
        _perchLeft = r.Left;
        _perchTop = r.Top;
        _perchWidth = r.Width;
    }

    /// <summary>
    /// Следим за опорой: за панелью задач и за окном, на котором сидим (оно может двигаться).
    /// false — опора пропала и попугай уже начал падать.
    /// </summary>
    private bool SnapToSupport()
    {
        if (IsPerched)
        {
            var b = Bounds;
            if (!_surfaces.TryGetBounds(_perchWindow, out var r) || r.Top - Size < b.Top)
            {
                Fall();
                return false;
            }
            X += r.Left - _perchLeft; // едем вместе с окном
            EnterPerch(_perchWindow, r);
            double center = X + Size / 2;
            if (center < Math.Max(r.Left, b.Left) || center > Math.Min(r.Right, b.Right))
            {
                Fall(); // окно уехало из-под лап
                return false;
            }
        }

        Y = SupportY;
        X = Math.Clamp(X, WalkMinX, WalkMaxX);
        return true;
    }

    /// <summary>Если на земле лежит зерно — разворачиваемся и идём к нему клювом.</summary>
    private void TryGoToFood()
    {
        if (!_food.IsLanded) return;

        if (IsPerched)
        {
            // Зерно лежит на панели задач: спрыгиваем с окна в его сторону
            Fall(_food.X >= X + Size / 2 ? 70 : -70);
            return;
        }

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
            BeginWalk();
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
        if (!SnapToSupport()) return;
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
        if (!SnapToSupport()) return;

        double remaining = _walkTargetX - X;
        double distance = Math.Abs(remaining);

        // Плавный разгон и торможение: скорость не больше той, с которой ещё успеваем остановиться
        double brake = Math.Sqrt(2 * WalkDecel * distance) + 6;
        _walkSpeed = Math.Min(Math.Min(_walkSpeed + WalkAccel * dt, BaseWalkSpeed * Speed), brake);
        double step = _walkSpeed * dt;

        if (distance <= step)
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
        Y = SupportY; // в режиме «Все экраны» земля зависит от X
    }

    private void UpdateFly(double dt)
    {
        // Цель-окно могла сдвинуться (или исчезнуть) — сдвигаем точку посадки вместе с ним
        if (_flyWindow != IntPtr.Zero)
        {
            if (!_surfaces.TryGetBounds(_flyWindow, out var r))
            {
                Fall(VelocityX * 0.4, Math.Max(VelocityY * 0.4, 0));
                return;
            }
            var shift = new Vector(r.Left - _flyWindowBounds.Left, r.Top - _flyWindowBounds.Top);
            _flyEnd += shift;
            _flyControl += shift;
            _flyWindowBounds = r;
        }

        _flyTime = Math.Min(_flyDuration, _flyTime + dt);
        double raw = _flyTime / _flyDuration;
        // Мягкий разгон и мягкое торможение, но без полной остановки в конце: касание на малой скорости
        double smooth = raw * raw * (3 - 2 * raw);
        double t = raw + (smooth - raw) * 0.75;

        double inv = 1 - t;
        X = inv * inv * _flyStart.X + 2 * inv * t * _flyControl.X + t * t * _flyEnd.X;
        Y = inv * inv * _flyStart.Y + 2 * inv * t * _flyControl.Y + t * t * _flyEnd.Y;
        // Волнообразное колебание; в начале и конце пути гаснет, чтобы не было скачков
        Y += Math.Sin(_flyTime * 2 * Math.PI * FlyWobbleHz) * FlyWobbleAmp * Math.Sin(Math.PI * raw);
        Y = Math.Max(Y, Bounds.Top);
        Phase = raw >= ApproachFrom ? FlightPhase.Approach : FlightPhase.Cruise;

        if (raw < 1) return;

        X = _flyEnd.X;
        Y = _flyEnd.Y;
        switch (FlyTarget)
        {
            case LandingKind.WindowTop:
                EnterPerch(_flyWindow, _flyWindowBounds);
                State = PetBehaviorState.Idle;
                Landed?.Invoke(130);
                break;
            case LandingKind.WindowSide:
                EnterCling(_flyWindow);
                break;
            default:
                Y = GroundY;
                State = PetBehaviorState.Idle;
                Landed?.Invoke(130);
                break;
        }
    }

    // ---- Висение на боку окна

    private void EnterCling(IntPtr window)
    {
        _clingWindow = window;
        FacingScale = _wallDir; // смотрим на окно
        _clingLeft = 5 + _random.NextDouble() * 5;
        _clingHop = _random.Next(2) == 0;
        _clingExiting = false;
        _clingTargetFeet = Y + Size;
        _clingPause = 0.8 + _random.NextDouble();
        _velocityX = 0;
        _velocityY = 0;
        State = PetBehaviorState.Cling;
        Landed?.Invoke(160); // лёгкий толчок при хвате
    }

    private void UpdateCling(double dt)
    {
        var b = Bounds;
        if (!_surfaces.TryGetBounds(_clingWindow, out var r))
        {
            Release();
            return;
        }

        double s = Scale;
        X = ClingX(r, _wallDir); // едем вместе с окном
        double minFeet = Math.Max(r.Top + ClingTopMargin * s, b.Top + Size);
        double maxFeet = Math.Min(r.Bottom - ClingBottomMargin * s, GroundY + Size);
        if (X < MinX - 1 || X > MaxX + 1 || maxFeet < minFeet - 1)
        {
            Release(); // окно уехало за край экрана или стало слишком низким
            return;
        }

        double feet = Math.Clamp(Y + Size, minFeet, maxFeet);

        if (!_clingExiting)
        {
            _clingLeft -= dt;
            if (_clingLeft <= 0)
            {
                // Конец висения: забраться на окно сверху (если сверху есть место) или отпустить лапки
                // Лезть вверх имеет смысл, только если до кромки недалеко и над окном есть место
                bool nearTop = feet - (r.Top + ClingHopMargin * s) < 160 * s;
                if (_clingHop && nearTop && r.Top - Size >= b.Top)
                {
                    _clingExiting = true;
                    _clingTargetFeet = r.Top + ClingHopMargin * s;
                }
                else
                {
                    Release();
                    return;
                }
            }
            else
            {
                // Иногда переползаем на новое место, между этим отдыхаем
                _clingPause -= dt;
                if (_clingPause <= 0 && Math.Abs(_clingTargetFeet - feet) < 1)
                {
                    _clingTargetFeet = Math.Clamp(feet + (_random.NextDouble() * 2 - 1) * 70 * s, minFeet, maxFeet);
                    _clingPause = 1.5 + _random.NextDouble() * 2;
                }
            }
        }

        // Переползание: скорость плавно растёт и гаснет у цели
        double diff = _clingTargetFeet - feet;
        double speed = Math.Min(ClimbSpeed * Speed * (_clingExiting ? 2 : 1), Math.Sqrt(2 * 120 * Math.Abs(diff)) + 4);
        double move = Math.Sign(diff) * Math.Min(Math.Abs(diff), speed * dt);
        feet += move;
        Y = feet - Size;

        if (_clingExiting && Math.Abs(_clingTargetFeet - feet) < 1.5)
        {
            // Запрыгиваем на кромку: подпрыгиваем и чуть вдвигаемся над окном, дальше работает обычное падение на окно
            double rise = 34 * s;
            Fall(_wallDir * 95, -Math.Sqrt(2 * Gravity * rise));
        }
    }

    /// <summary>Разжать лапки: падение от стены.</summary>
    private void Release() => Fall(-_wallDir * 45, -60);

    // ---- Падение

    private void UpdateFall(double dt)
    {
        double prevBottom = Y + Size;
        _velocityY = Math.Min(_velocityY + Gravity * dt, TerminalVelocity);
        Y += _velocityY * dt;
        X = Math.Clamp(X + _velocityX * dt, MinX, MaxX);
        _velocityX *= Math.Exp(-1.5 * dt); // горизонтальный разлёт быстро затухает

        if (_velocityY > 0 && TryLandOnWindow(prevBottom)) return;

        double ground = GroundY;
        if (Y < ground) return;

        Y = ground;
        _velocityX = 0;
        State = PetBehaviorState.Idle;
        Landed?.Invoke(_velocityY);
        _velocityY = 0;
    }

    /// <summary>Падающий попугай пересёк верхнюю кромку окна — садится на неё без отскока.</summary>
    private bool TryLandOnWindow(double prevBottom)
    {
        double newBottom = Y + Size;
        double cx = X + Size / 2;
        var windows = _surfaces.GetWindows();
        var top = Bounds.Top;

        int best = -1;
        double bestTop = double.MaxValue;
        for (int i = 0; i < windows.Count; i++)
        {
            var r = windows[i].Bounds;
            if (r.Top >= bestTop || r.Top - Size < top) continue;
            if (prevBottom > r.Top + LandTolerance || newBottom < r.Top) continue;
            if (cx < r.Left + 2 || cx > r.Right - 2) continue;
            if (_surfaces.IsCovered(windows, i, cx, r.Top + 2)) continue;
            best = i;
            bestTop = r.Top;
        }
        if (best < 0) return false;

        // Кэш списка мог устареть — сверяемся со свежими границами окна
        var w = windows[best];
        if (!_surfaces.TryGetBounds(w.Handle, out var fresh) || Math.Abs(fresh.Top - bestTop) > LandTolerance
            || cx < fresh.Left || cx > fresh.Right) return false;

        Y = fresh.Top - Size;
        EnterPerch(w.Handle, fresh);
        double impact = _velocityY;
        _velocityX = 0;
        _velocityY = 0;
        State = PetBehaviorState.Idle;
        Landed?.Invoke(impact);
        return true;
    }
}
