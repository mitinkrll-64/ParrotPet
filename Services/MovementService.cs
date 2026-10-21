using System;
using System.Windows;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Физика и перемещение попугая. Не знает про окна и таймеры:
/// снаружи вызывают Update(dt) каждый кадр и читают X, Y, FacingScale.
/// Все координаты — в WPF-единицах (DIP).
/// </summary>
public class MovementService
{
    private const double WalkSpeed = 70;        // пикселей в секунду
    private const double Gravity = 1800;        // пикселей/с²
    private const double BounceDamping = 0.35;  // доля скорости после отскока
    private const double MinBounceSpeed = 150;  // ниже — попугай «прилипает» к земле
    private const double FlyDuration = 1.6;     // секунд на перелёт
    private const double HoverBobAmplitude = 6; // амплитуда покачивания в парении

    private readonly Random _random = new();

    private double _walkTargetX;
    private double _velocityY;

    // Параметры полёта: квадратичная кривая Безье P0 -> P1 (контрольная) -> P2
    private Point _flyStart, _flyControl, _flyEnd;
    private double _flyTime;
    private double _hoverLeft;
    private double _hoverTime;

    private double _dragGrabX, _dragGrabY;

    public PetBehaviorState State { get; private set; } = PetBehaviorState.Idle;
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>1 — смотрит вправо, -1 — влево (для ScaleX спрайта).</summary>
    public double FacingScale { get; private set; } = 1;

    public double SpriteWidth { get; set; } = 160;
    public double SpriteHeight { get; set; } = 200;

    /// <summary>Рабочая область без панели задач; читаем каждый раз, т.к. панель могут передвинуть.</summary>
    private Rect Area => SystemParameters.WorkArea;

    /// <summary>«Земля» — верхняя кромка панели задач.</summary>
    private double GroundY => Area.Bottom - SpriteHeight;

    private double MinX => Area.Left;
    private double MaxX => Math.Max(Area.Left, Area.Right - SpriteWidth);

    /// <summary>Ставит попугая на землю по центру экрана.</summary>
    public void PlaceOnGround()
    {
        X = Area.Left + (Area.Width - SpriteWidth) / 2;
        Y = GroundY;
        State = PetBehaviorState.Idle;
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

    /// <summary>Взлететь в случайную точку экрана и парить несколько секунд.</summary>
    public void StartFly()
    {
        if (State != PetBehaviorState.Idle) return;

        double targetX = MinX + _random.NextDouble() * (MaxX - MinX);
        double minY = Area.Top;
        double maxY = Math.Max(minY, GroundY - 120);
        double targetY = minY + _random.NextDouble() * (maxY - minY);

        _flyStart = new Point(X, Y);
        _flyEnd = new Point(targetX, targetY);
        // Контрольная точка выше середины пути — получается плавная дуга вверх
        _flyControl = new Point((X + targetX) / 2, Math.Min(Y, targetY) - 120);
        _flyTime = 0;
        _hoverLeft = 3 + _random.NextDouble() * 3;
        _hoverTime = 0;
        FacingScale = targetX >= X ? 1 : -1;
        State = PetBehaviorState.Fly;
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
        Y = Math.Clamp(cursorY - _dragGrabY, Area.Top, GroundY);
    }

    /// <summary>Отпустили мышь: в воздухе — падаем, на земле — сидим.</summary>
    public void EndDrag()
    {
        if (State != PetBehaviorState.Dragged) return;
        _velocityY = 0;
        State = Y < GroundY - 0.5 ? PetBehaviorState.Fall : PetBehaviorState.Idle;
        if (State == PetBehaviorState.Idle) Y = GroundY;
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
        }
    }

    private void UpdateIdle()
    {
        // Следим за панелью задач: если её сдвинули, остаёмся на ней
        Y = GroundY;
        X = Math.Clamp(X, MinX, MaxX);
    }

    private void UpdateWalk(double dt)
    {
        Y = GroundY;
        double step = WalkSpeed * dt;
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
    }

    private void UpdateFly(double dt)
    {
        if (_flyTime < FlyDuration)
        {
            _flyTime = Math.Min(FlyDuration, _flyTime + dt);
            double t = _flyTime / FlyDuration;
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
        if (_hoverLeft <= 0)
        {
            _velocityY = 0;
            State = PetBehaviorState.Fall;
        }
    }

    private void UpdateFall(double dt)
    {
        _velocityY += Gravity * dt;
        Y += _velocityY * dt;

        if (Y < GroundY) return;

        Y = GroundY;
        if (_velocityY > MinBounceSpeed)
        {
            _velocityY = -_velocityY * BounceDamping; // пружинящее приземление
        }
        else
        {
            _velocityY = 0;
            State = PetBehaviorState.Idle;
        }
    }
}