using System;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Процедурные анимации: дыхание, повороты головы, чистка пёрышек, моргание, ходьба,
/// полёт с махами, паническое падение, клевание, сердечко и пружина приземления.
/// Только считает позу — рисует окно.
/// </summary>
public class AnimationService
{
    private const double BlendTime = 0.18;      // сглаживание при смене состояния
    private const double PreenDuration = 1.6;
    private const double HeartDuration = 1.3;
    private const double BlinkDuration = 0.16;

    private readonly Random _random = new();

    private double _time, _stateTime;
    private double _walkPhase, _flapPhase;
    private PetBehaviorState _lastState = PetBehaviorState.Idle;
    private AnimationPose _snapshot = AnimationPose.Neutral;

    private double _nextBlink, _blinkTime = -1;
    private double _nextHeadTurn, _headTarget, _headCurrent;
    private double _nextPreen, _preenTime = -1;
    private double _heartTime = -1;
    private bool _preenRequested;
    private double _landTime = 99, _landAmplitude;

    public AnimationService()
    {
        _nextBlink = Range(4, 8);
        _nextHeadTurn = Range(1.5, 4);
        _nextPreen = Range(6, 12);
    }

    /// <summary>Итоговая поза кадра.</summary>
    public AnimationPose Pose { get; private set; } = AnimationPose.Neutral;

    private double Range(double min, double max) => min + _random.NextDouble() * (max - min);

    /// <summary>Приземление: impactSpeed — скорость удара о землю (px/с).</summary>
    public void TriggerLand(double impactSpeed)
    {
        _landAmplitude = Math.Min(1, impactSpeed / 1000);
        _landTime = 0;
    }

    /// <summary>Запустить чистку пёрышек (сработает, когда попугай в состоянии Idle).</summary>
    public void RequestPreen() => _preenRequested = true;

    /// <summary>Над попугаем появляется сердечко.</summary>
    public void TriggerHeart() => _heartTime = 0;

    public void Update(double dt, MovementService movement, double speed)
    {
        _time += dt;
        var state = movement.State;

        if (state != _lastState)
        {
            _snapshot = Pose; // от текущей позы плавно переходим к новой
            _lastState = state;
            _stateTime = 0;
            _preenTime = -1;
        }
        _stateTime += dt;

        var raw = state switch
        {
            PetBehaviorState.Walk => WalkPose(dt, speed),
            PetBehaviorState.Fly => FlyPose(dt, movement),
            PetBehaviorState.Fall => FallPose(dt),
            PetBehaviorState.Dragged => DraggedPose(dt),
            PetBehaviorState.Eat => EatPose(),
            _ => IdlePose(dt)
        };

        double blend = Math.Min(1, _stateTime / BlendTime);
        blend = blend * blend * (3 - 2 * blend);
        var pose = AnimationPose.Lerp(_snapshot, raw, blend);

        // Приземление накладывается поверх: затухающая пружина (squash & stretch)
        double sx = pose.ScaleX, sy = pose.ScaleY;
        if (_landTime < 1.5)
        {
            _landTime += dt;
            double spring = _landAmplitude * Math.Exp(-7 * _landTime) * Math.Cos(2 * Math.PI * 3.5 * _landTime);
            sy *= 1 - 0.28 * spring;
            sx *= 1 + 0.18 * spring;
        }

        Pose = pose with { ScaleX = sx, ScaleY = sy, EyeOpen = UpdateBlink(dt), Heart = UpdateHeart(dt) };
    }

    /// <summary>Моргание раз в 4–8 секунд.</summary>
    private double UpdateBlink(double dt)
    {
        _nextBlink -= dt;
        if (_blinkTime < 0 && _nextBlink <= 0)
        {
            _blinkTime = 0;
            _nextBlink = Range(4, 8);
        }
        if (_blinkTime < 0) return 1;

        _blinkTime += dt;
        double u = _blinkTime / BlinkDuration;
        if (u >= 1)
        {
            _blinkTime = -1;
            return 1;
        }
        return 1 - 0.95 * Math.Sin(Math.PI * u);
    }

    private double UpdateHeart(double dt)
    {
        if (_heartTime < 0) return -1;
        _heartTime += dt;
        if (_heartTime >= HeartDuration)
        {
            _heartTime = -1;
            return -1;
        }
        return _heartTime / HeartDuration;
    }

    /// <summary>Покой: дыхание, случайные повороты головы, чистка пёрышек.</summary>
    private AnimationPose IdlePose(double dt)
    {
        _nextHeadTurn -= dt;
        if (_nextHeadTurn <= 0)
        {
            _headTarget = _random.Next(3) == 0 ? 0 : (_random.NextDouble() * 2 - 1) * 14;
            _nextHeadTurn = Range(1.5, 4);
        }
        _headCurrent += (_headTarget - _headCurrent) * (1 - Math.Exp(-6 * dt));

        double head = _headCurrent, wingRot = 0, wingScale = 1;

        _nextPreen -= dt;
        if (_preenTime < 0 && (_nextPreen <= 0 || _preenRequested))
        {
            _preenTime = 0;
            _preenRequested = false;
            _nextPreen = Range(6, 12);
        }
        if (_preenTime >= 0)
        {
            _preenTime += dt;
            double u = _preenTime / PreenDuration;
            if (u >= 1)
            {
                _preenTime = -1;
            }
            else
            {
                double env = Math.Sin(Math.PI * u); // плавно входим и выходим
                wingRot = 16 * env * Math.Sin(2 * Math.PI * 3 * u); // покачивание крыла
                wingScale = 1 + 0.12 * env;
                head = 22 * env; // голова склоняется к крылу
            }
        }

        double breath = 1 + 0.03 * Math.Sin(2 * Math.PI * 0.5 * _time);
        return new AnimationPose(HeadRotation: head, WingScaleY: wingScale, WingRotation: wingRot, Breath: breath);
    }

    /// <summary>Ходьба вперевалку с покачиванием хвоста.</summary>
    private AnimationPose WalkPose(double dt, double speed)
    {
        _walkPhase += dt * 2 * Math.PI * 2.2 * speed;
        return new AnimationPose(
            Rotation: 5 * Math.Sin(_walkPhase),
            ScaleY: 1 + 0.015 * Math.Sin(2 * _walkPhase),
            HeadRotation: 3 * Math.Sin(2 * _walkPhase),
            TailRotation: 14 * Math.Sin(_walkPhase + 1.2));
    }

    /// <summary>Полёт: махи 8–12 Гц, наклон вперёд 10–25°, зависание и планирование.</summary>
    private AnimationPose FlyPose(double dt, MovementService movement)
    {
        double hz = movement.Phase switch
        {
            FlightPhase.Cruise => 10,
            FlightPhase.Hover => 8,
            _ => 5
        };
        _flapPhase += dt * 2 * Math.PI * hz;
        double s = Math.Sin(_flapPhase);

        switch (movement.Phase)
        {
            case FlightPhase.Cruise:
                double norm = Math.Clamp(Math.Sqrt(movement.VelocityX * movement.VelocityX
                    + movement.VelocityY * movement.VelocityY) / 700, 0, 1);
                return new AnimationPose(
                    Rotation: 10 + 12 * norm, // чем быстрее, тем сильнее наклон вперёд (10–22°, больше упирается клювом в край холста)
                    HeadRotation: -8,
                    WingScaleY: 0.9 + 0.45 * s,
                    WingRotation: 22 * s,
                    TailRotation: 6 * Math.Sin(_flapPhase * 0.5));

            case FlightPhase.Hover:
                return new AnimationPose(
                    Rotation: 2,
                    WingScaleY: 0.9 + 0.4 * s,
                    WingRotation: 20 * s,
                    TailRotation: 4 * Math.Sin(_flapPhase * 0.5));

            default: // Descend: крылья широко, лёгкий подъём носа перед посадкой
                return new AnimationPose(
                    Rotation: -6,
                    WingScaleY: 1.25 + 0.1 * s,
                    WingRotation: -10 + 6 * s,
                    TailRotation: -4);
        }
    }

    /// <summary>Падение хвостом вверх и паническое хлопанье крыльями.</summary>
    private AnimationPose FallPose(double dt)
    {
        _flapPhase += dt * 2 * Math.PI * 12;
        double s = Math.Sin(_flapPhase);
        return new AnimationPose(
            Rotation: 30,
            HeadRotation: -12,
            WingScaleY: 0.9 + 0.6 * s,
            WingRotation: 20 * Math.Sin(_flapPhase * 0.5),
            TailRotation: 20 + 4 * s);
    }

    /// <summary>Пока попугая держат мышью, он слегка трепыхается.</summary>
    private AnimationPose DraggedPose(double dt)
    {
        _flapPhase += dt * 2 * Math.PI * 5;
        double s = Math.Sin(_flapPhase);
        return new AnimationPose(
            HeadRotation: -6,
            WingScaleY: 0.95 + 0.3 * s,
            WingRotation: 10 * s,
            TailRotation: 12 * Math.Sin(_time * 3));
    }

    /// <summary>Клевание: три наклона корпуса и головы вниз.</summary>
    private AnimationPose EatPose()
    {
        double u = Math.Min(1, _stateTime / MovementService.EatDuration);
        double peck = (1 - Math.Cos(2 * Math.PI * 3 * u)) / 2;
        return new AnimationPose(Rotation: 28 * peck, HeadRotation: 40 * peck, TailRotation: -5 * peck);
    }
}