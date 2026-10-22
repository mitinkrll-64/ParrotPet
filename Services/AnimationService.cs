using System;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Процедурные анимации по реальному поведению волнистых попугайчиков: дыхание, повороты головы,
/// в покое — чистка (крыло, грудка, хвост), встряхивание, потягивание, зевок, скрип клювом, дрёма
/// на одной лапке, чириканье, виляние хвостом, осматривание; ходьба с переступанием, полёт с махами,
/// поджатые лапки и заход на посадку, падение, висение и лазание по боку окна, клевание, сердечко,
/// пружина приземления и приседание перед взлётом.
/// Только считает позу — рисует окно.
/// </summary>
public class AnimationService
{
    private const double HeartDuration = 1.3;
    private const double BlinkDuration = 0.16;
    private const double LaunchDuration = 0.32;
    private const double ClingAngle = -76;       // тело вертикально, голова вверх (птица смотрит на окно)
    private const double ClingLift = 24;         // спрайт приподнят вдоль тела: длинный хвост свисает вниз и не должен резаться краем холста

    private readonly Random _random = new();

    private double _time, _stateTime, _blendTime = 0.2;
    private double _walkPhase, _flapPhase, _flapHz = 6, _pitch, _climbPhase;
    private PetBehaviorState _lastState = PetBehaviorState.Idle;
    private AnimationPose _snapshot = AnimationPose.Neutral;

    private double _nextBlink, _blinkTime = -1;
    private double _nextHeadTurn, _headTarget, _headCurrent;
    private IdleAction _action = IdleAction.None, _lastAction = IdleAction.None;
    private IdleAction _requested = IdleAction.None;
    private double _actionTime, _actionDur, _nextAction;
    private double _eyeGoal = 1, _eyeCur = 1;
    private double _heartTime = -1;
    private double _landTime = 99, _landAmplitude;
    private double _launchTime = 99;

    public AnimationService()
    {
        _nextBlink = Range(4, 8);
        _nextHeadTurn = Range(1.5, 4);
        _nextAction = Range(2.5, 5);
    }

    /// <summary>Итоговая поза кадра.</summary>
    public AnimationPose Pose { get; private set; } = AnimationPose.Neutral;

    private double Range(double min, double max) => min + _random.NextDouble() * (max - min);

    private static double Mix(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Плавная S-кривая 0..1 (smootherstep: нулевая скорость и ускорение на краях).</summary>
    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    /// <summary>Приземление: impactSpeed — скорость удара о опору (px/с).</summary>
    public void TriggerLand(double impactSpeed)
    {
        _landAmplitude = Math.Min(1, impactSpeed / 1000);
        _landTime = 0;
    }

    /// <summary>Запустить чистку пёрышек (сработает, когда попугай в состоянии Idle).</summary>
    public void RequestPreen() => RequestIdle(IdleAction.PreenWing);

    /// <summary>Запустить конкретное действие в покое (Debug-окно); сработает, когда попугай в состоянии Idle.</summary>
    public void RequestIdle(IdleAction action) => _requested = action;

    /// <summary>Какое действие в покое идёт сейчас (для Debug-окна).</summary>
    public IdleAction CurrentIdleAction => _action;

    /// <summary>Над попугаем появляется сердечко.</summary>
    public void TriggerHeart() => _heartTime = 0;

    /// <summary>Сколько секунд смешиваем старую позу с новой при смене состояния.</summary>
    private static double BlendFor(PetBehaviorState from, PetBehaviorState to) => to switch
    {
        PetBehaviorState.Cling => 0.3,
        PetBehaviorState.Fall => from == PetBehaviorState.Cling ? 0.22 : 0.12,
        PetBehaviorState.Fly => 0.16,
        PetBehaviorState.Idle => from is PetBehaviorState.Fly or PetBehaviorState.Fall ? 0.3 : 0.25,
        _ => 0.2
    };

    public void Update(double dt, MovementService movement, double speed)
    {
        _time += dt;
        var state = movement.State;

        if (state != _lastState)
        {
            _snapshot = Pose; // от текущей позы плавно переходим к новой
            _blendTime = BlendFor(_lastState, state);
            if (state == PetBehaviorState.Fly)
            {
                _launchTime = 0;
                _flapHz = 5;
                _pitch = 0;
            }
            _lastState = state;
            _stateTime = 0;
            _action = IdleAction.None; // действие в покое прерывается сменой состояния
            _nextAction = Range(2, 5);
        }
        if (state != PetBehaviorState.Idle) _eyeGoal = 1;
        _stateTime += dt;

        var raw = state switch
        {
            PetBehaviorState.Walk => WalkPose(dt, movement),
            PetBehaviorState.Fly => FlyPose(dt, movement),
            PetBehaviorState.Fall => FallPose(dt, movement),
            PetBehaviorState.Dragged => DraggedPose(dt),
            PetBehaviorState.Eat => EatPose(),
            PetBehaviorState.Cling => ClingPose(dt, movement),
            _ => IdlePose(dt)
        };

        var pose = AnimationPose.Lerp(_snapshot, raw, Smooth(_stateTime / _blendTime));

        // Приземление накладывается поверх: затухающая пружина (squash & stretch)
        double sx = pose.ScaleX, sy = pose.ScaleY;
        if (_landTime < 1.5)
        {
            _landTime += dt;
            double spring = _landAmplitude * Math.Exp(-7 * _landTime) * Math.Cos(2 * Math.PI * 3.5 * _landTime);
            sy *= 1 - 0.28 * spring;
            sx *= 1 + 0.18 * spring;
        }

        // Взлёт: короткое приседание и распрямление — толчок лапами
        if (_launchTime < LaunchDuration)
        {
            _launchTime += dt;
            double u = Math.Clamp(_launchTime / LaunchDuration, 0, 1);
            double push = Math.Sin(2 * Math.PI * u) * (1 - u);
            sy *= 1 - 0.1 * push;
            sx *= 1 + 0.05 * push;
        }

        // Сонный прищур плавно тянется к цели; моргание накладывается поверх
        _eyeCur += (_eyeGoal - _eyeCur) * (1 - Math.Exp(-7 * dt));
        Pose = pose with { ScaleX = sx, ScaleY = sy, EyeOpen = Math.Min(UpdateBlink(dt), _eyeCur), Heart = UpdateHeart(dt) };
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

    // Таблица действий в покое: вес при случайном выборе и длительность, секунд
    private static readonly (IdleAction Action, double Weight, double Duration)[] IdleTable =
    {
        (IdleAction.PreenWing, 3, 5.5), (IdleAction.PreenChest, 2, 4.5), (IdleAction.PreenTail, 1, 4.5),
        (IdleAction.Fluff, 2, 1.8), (IdleAction.Stretch, 2, 3.4), (IdleAction.Yawn, 1.5, 2.4),
        (IdleAction.BeakGrind, 1.5, 5), (IdleAction.Doze, 1, 9), (IdleAction.Chirp, 2, 2.6),
        (IdleAction.TailWag, 1, 1.4), (IdleAction.LookAround, 3, 3.2)
    };

    private void StartAction(IdleAction action)
    {
        _action = action;
        _lastAction = action;
        _actionTime = 0;
        _actionDur = Array.Find(IdleTable, e => e.Action == action).Duration;
    }

    private IdleAction PickAction()
    {
        double total = 0;
        foreach (var e in IdleTable) if (e.Action != _lastAction) total += e.Weight;
        double roll = _random.NextDouble() * total;
        foreach (var e in IdleTable)
        {
            if (e.Action == _lastAction) continue; // два раза подряд одно и то же не делаем
            roll -= e.Weight;
            if (roll <= 0) return e.Action;
        }
        return IdleAction.LookAround;
    }

    /// <summary>
    /// Покой: дыхание и случайные повороты головы, а раз в несколько секунд — одно из действий
    /// IdleTable. Действие плавно вырастает из базовой позы и так же плавно в неё возвращается.
    /// </summary>
    private AnimationPose IdlePose(double dt)
    {
        var rest = RestPose(dt);
        _eyeGoal = 1;

        if (_action == IdleAction.None)
        {
            _nextAction -= dt;
            if (_requested != IdleAction.None)
            {
                StartAction(_requested);
                _requested = IdleAction.None;
            }
            else if (_nextAction <= 0)
            {
                StartAction(PickAction());
            }
            if (_action == IdleAction.None) return rest;
        }

        _actionTime += dt;
        if (_actionTime >= _actionDur)
        {
            _action = IdleAction.None;
            _nextAction = Range(3, 7);
            return rest;
        }

        double env = Smooth(_actionTime / 0.4) * Smooth((_actionDur - _actionTime) / 0.4);
        var full = ActionPose(_action, _actionTime, _actionDur, rest, out double eyeGoal);
        _eyeGoal = Mix(1, eyeGoal, env);
        return AnimationPose.Lerp(rest, full, env);
    }

    /// <summary>Базовый покой: дыхание, покачивание, случайные повороты головы.</summary>
    private AnimationPose RestPose(double dt)
    {
        _nextHeadTurn -= dt;
        if (_nextHeadTurn <= 0)
        {
            _headTarget = _random.Next(3) == 0 ? 0 : (_random.NextDouble() * 2 - 1) * 14;
            _nextHeadTurn = Range(1.5, 4);
        }
        _headCurrent += (_headTarget - _headCurrent) * (1 - Math.Exp(-6 * dt));

        double breath = 1 + 0.03 * Math.Sin(2 * Math.PI * 0.5 * _time);
        // Едва заметное переминание: лёгкое покачивание корпуса и хвоста
        return new AnimationPose(
            Rotation: 0.8 * Math.Sin(2 * Math.PI * 0.23 * _time),
            HeadRotation: _headCurrent,
            TailRotation: 1.5 * Math.Sin(2 * Math.PI * 0.31 * _time + 1), Breath: breath);
    }

    /// <summary>Подъём-удержание-спад: 0 до a, плавно вверх a..b, 1 до c, плавно вниз c..d.</summary>
    private static double Bump(double t, double a, double b, double c, double d) =>
        Smooth((t - a) / (b - a)) * (1 - Smooth((t - c) / (d - c)));

    /// <summary>Поза «на пике» действия; плавный вход и выход накладывает вызывающий код.</summary>
    private AnimationPose ActionPose(IdleAction action, double t, double dur, AnimationPose rest, out double eyeGoal)
    {
        eyeGoal = 1;
        double pi2 = 2 * Math.PI;
        double nibble;

        switch (action)
        {
            case IdleAction.PreenWing:
            {
                // Голова заворачивается назад поверх спинки; клюв перебирает перья, медленно скользя вдоль крыла
                double slide = 0.5 + 0.5 * Math.Sin(pi2 * 0.36 * t);
                nibble = Math.Max(0, Math.Sin(pi2 * 4.2 * t));
                return rest with
                {
                    Rotation = 2, HeadRotation = -(58 + 34 * slide) + 7 * Math.Sin(pi2 * 4.2 * t),
                    WingRotation = 9 + 3 * Math.Sin(pi2 * 2.1 * t), WingScaleY = 1.08,
                    BeakOpen = 3 + 8 * nibble, TailRotation = 4 * Math.Sin(pi2 * 0.8 * t)
                };
            }

            case IdleAction.PreenChest:
            {
                // Голова опускается к груди, клюв легко перебирает пух
                nibble = Math.Max(0, Math.Sin(pi2 * 4 * t));
                return rest with
                {
                    Rotation = 6, HeadRotation = 40 + 8 * Math.Sin(pi2 * 1.3 * t) + 4 * Math.Sin(pi2 * 4 * t),
                    BeakOpen = 3 + 7 * nibble, Breath = 1.05
                };
            }

            case IdleAction.PreenTail:
            {
                // Хвост поднят, корпус подаётся вперёд, голова тянется назад к огузку
                nibble = Math.Max(0, Math.Sin(pi2 * 4 * t));
                return rest with
                {
                    Rotation = 13, HeadRotation = -(88 + 14 * Math.Sin(pi2 * 0.5 * t)) + 5 * Math.Sin(pi2 * 4 * t),
                    TailRotation = 42 + 5 * Math.Sin(pi2 * 1.4 * t), WingRotation = 14, WingScaleY = 1.06,
                    BeakOpen = 3 + 7 * nibble, FootRotation = 6
                };
            }

            case IdleAction.Fluff:
            {
                // Взъерошился -> дрожь всем телом -> перья улеглись
                double puff = Bump(t, 0, 0.45, 1.2, 1.7);
                double shake = Bump(t, 0.45, 0.6, 1.0, 1.25) * Math.Sin(pi2 * 17 * t);
                return rest with
                {
                    Breath = 1 + 0.2 * puff, ScaleX = 1 + 0.07 * puff, Rotation = 4.5 * shake,
                    HeadRotation = rest.HeadRotation * (1 - puff) - 6 * puff + 4 * shake,
                    WingRotation = 6 * puff + 5 * shake, WingScaleY = 1 + 0.1 * puff,
                    TailRotation = 16 * shake, OffsetY = -0.8 * puff
                };
            }

            case IdleAction.Stretch:
            {
                // Крыло и лапка одной стороны уходят назад-вниз; в пике — лёгкий зевок
                double reach = Bump(t, 0.1, 0.9, 2.1, 3.0);
                double yawn = Bump(t, 0.7, 1.1, 1.7, 2.1);
                eyeGoal = 1 - 0.5 * yawn;
                return rest with
                {
                    Rotation = -5 * reach, HeadRotation = -10 * reach,
                    WingRotation = 34 * reach, WingScaleY = 1 + 0.25 * reach,
                    FootRotation = 40 * reach, TailRotation = 12 * reach,
                    BeakOpen = 14 * yawn, Breath = 1 + 0.04 * reach
                };
            }

            case IdleAction.Yawn:
            {
                double open = Bump(t, 0.1, 0.8, 1.5, 2.2);
                eyeGoal = 1 - 0.8 * open;
                return rest with
                {
                    HeadRotation = -16 * open, Rotation = -3 * open, BeakOpen = 34 * open,
                    Breath = 1 + 0.05 * open, WingRotation = 4 * open
                };
            }

            case IdleAction.BeakGrind:
            {
                // Довольный попугай: глаза полузакрыты, нижняя половина клюва едва ходит
                eyeGoal = 0.4;
                return rest with
                {
                    HeadRotation = 6 + 3 * Math.Sin(pi2 * 0.4 * t), Breath = 1.05, ScaleX = 1.02,
                    BeakOpen = 2.5 + 2.5 * Math.Sin(pi2 * 7 * t), Rotation = -1
                };
            }

            case IdleAction.Doze:
            {
                // Засыпает: глаза закрываются, пух взъерошен, одна лапка подобрана; в конце вздрагивает
                double sleep = Bump(t, 0, 2.2, dur - 1.8, dur - 0.4);
                double jolt = Bump(t, dur - 1.2, dur - 1.0, dur - 0.8, dur - 0.5);
                eyeGoal = 1 - 0.92 * sleep + 0.9 * jolt;
                return rest with
                {
                    HeadRotation = 12 * sleep + 4 * Math.Sin(pi2 * 0.18 * t) - 10 * jolt,
                    Breath = 1 + 0.09 * sleep + 0.012 * Math.Sin(pi2 * 0.45 * t), ScaleX = 1 + 0.04 * sleep,
                    ScaleY = 1 - 0.03 * sleep, Rotation = -2 * sleep, FootTuck = Smooth((t - 0.8) / 1.6) * (1 - jolt),
                    WingRotation = -3 * sleep, TailRotation = -3 * sleep
                };
            }

            case IdleAction.Chirp:
            {
                // Серия трелей: клюв открывается в такт, голова покачивается, грудка пульсирует
                double burst = Bump(t, 0.1, 0.3, dur - 0.7, dur - 0.3);
                double beat = Math.Sin(pi2 * 3.4 * t);
                return rest with
                {
                    BeakOpen = 20 * burst * Math.Max(0, beat), HeadRotation = -10 * burst + 7 * burst * beat,
                    Breath = 1 + 0.045 * burst * Math.Max(0, beat), Rotation = -3 * burst,
                    OffsetY = -1.2 * burst * Math.Abs(beat), TailRotation = 5 * burst * Math.Sin(pi2 * 1.7 * t)
                };
            }

            case IdleAction.TailWag:
            {
                double wag = Bump(t, 0.05, 0.2, dur - 0.5, dur - 0.1) * Math.Sin(pi2 * 7.5 * t);
                return rest with { TailRotation = 24 * wag, Rotation = 2 * wag, WingRotation = 3 * wag };
            }

            default: // LookAround
            {
                // Любопытство: повороты головы, быстрые кивки вперёд
                double turn = 16 * Math.Sin(pi2 * 0.55 * t) + 9 * Math.Sin(pi2 * 1.3 * t + 0.6);
                double nod = Math.Max(0, Math.Sin(pi2 * 1.25 * t - 0.5));
                return rest with
                {
                    HeadRotation = turn + 14 * nod * nod, Rotation = 1.5 * Math.Sin(pi2 * 0.55 * t),
                    OffsetX = 1.2 * nod, TailRotation = 3 * Math.Sin(pi2 * 0.9 * t)
                };
            }
        }
    }

    /// <summary>
    /// Ходьба вперевалку: темп шагов следует за реальной скоростью (при разгоне и торможении шаги
    /// реже), корпус подскакивает на каждом шаге и покачивается с боку на бок.
    /// </summary>
    private AnimationPose WalkPose(double dt, MovementService movement)
    {
        double sp = Math.Abs(movement.VelocityX);
        _walkPhase += dt * 2 * Math.PI * 0.0315 * sp;      // 2.2 шага в секунду при скорости 70 px/с
        double drive = Math.Clamp(sp / 35, 0, 1);           // на остановке амплитуда гаснет
        double lean = Math.Clamp(sp / 140, 0, 1);           // лёгкий наклон вперёд при ходьбе

        double s = Math.Sin(_walkPhase);
        double hop = Math.Abs(s);
        return new AnimationPose(
            Rotation: drive * 5 * s + 3 * lean,
            ScaleY: 1 + drive * 0.02 * Math.Cos(2 * _walkPhase),
            HeadRotation: drive * 4 * Math.Sin(2 * _walkPhase - 0.7) - 2 * lean,
            WingRotation: drive * 2 * Math.Sin(_walkPhase + 0.4),
            TailRotation: drive * 12 * Math.Sin(_walkPhase + 1.2),
            OffsetY: -drive * 2.2 * hop,
            FootRotation: drive * 26 * s, FarFootRotation: -drive * 26 * s);
    }

    /// <summary>
    /// Полёт: махи 5–10 Гц, наклон носа по вектору скорости (вверх при наборе высоты, вниз при спуске),
    /// и заход на посадку — нос поднимается, крылья широко, хвост веером, частота махов падает.
    /// К боку окна птица выходит на вертикаль.
    /// </summary>
    private AnimationPose FlyPose(double dt, MovementService movement)
    {
        double flare = movement.Phase == FlightPhase.Approach ? Smooth((movement.FlightProgress - 0.72) / 0.28) : 0;

        _flapHz += (Mix(9.5, 5.5, flare) - _flapHz) * (1 - Math.Exp(-5 * dt));
        _flapPhase += dt * 2 * Math.PI * _flapHz;
        double s = Math.Sin(_flapPhase);

        // Угол по траектории: положительный — клюв вниз
        double pitchTarget = Math.Atan2(movement.VelocityY, Math.Abs(movement.VelocityX) + 90) * 180 / Math.PI * 0.75 + 4;
        pitchTarget = Math.Clamp(pitchTarget, -35, 35);
        _pitch += (pitchTarget - _pitch) * (1 - Math.Exp(-8 * dt));

        bool toWall = movement.FlyTarget == LandingKind.WindowSide;
        double rotation = toWall ? Mix(_pitch, ClingAngle, flare) : _pitch - 24 * flare;

        return new AnimationPose(
            Rotation: rotation,
            HeadRotation: Mix(-6, toWall ? 8 : -4, flare),
            WingScaleY: 0.85 + 0.5 * s + 0.3 * flare,
            WingRotation: 24 * s + 6 * Math.Sin(2 * _flapPhase + 0.5) - 10 * flare,
            TailRotation: 6 * Math.Sin(_flapPhase * 0.5) - 14 * flare,
            OffsetX: toWall ? ClingLift * flare : 0,
            OffsetY: -2 * Math.Sin(_flapPhase + 0.6),
            FootRotation: Mix(38, -22, flare), FarFootRotation: Mix(34, -26, flare),
            LegsTuck: Mix(0.75, 0, flare));
    }

    /// <summary>
    /// Падение: вниз — паническое хлопанье крыльями (чем быстрее, тем чаще и сильнее наклон),
    /// вверх (прыжок с окна) — нос поднят и взмахи вниз.
    /// </summary>
    private AnimationPose FallPose(double dt, MovementService movement)
    {
        double vy = movement.VelocityY;
        bool rising = vy < -30;
        double speedK = Math.Clamp(vy / 900, 0, 1);

        double hz = rising ? 11 : Mix(8, 12.5, speedK);
        _flapHz += (hz - _flapHz) * (1 - Math.Exp(-8 * dt));
        _flapPhase += dt * 2 * Math.PI * _flapHz;
        double s = Math.Sin(_flapPhase);

        return new AnimationPose(
            Rotation: rising ? -18 : 12 + 22 * speedK,
            HeadRotation: -10,
            WingScaleY: 0.9 + (rising ? 0.5 : 0.6) * s,
            WingRotation: rising ? 26 * s : 20 * Math.Sin(_flapPhase * 0.5),
            TailRotation: rising ? 4 : 14 + 4 * s,
            OffsetY: -1.5 * s,
            FootRotation: rising ? 20 : 25 + 12 * s, FarFootRotation: rising ? 14 : 15 - 12 * s,
            BeakOpen: rising ? 4 : 12);
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
            TailRotation: 12 * Math.Sin(_time * 3),
            FootRotation: 14 * s, FarFootRotation: -14 * s,
            BeakOpen: 10 * Math.Max(0, Math.Sin(_time * 9)));
    }

    /// <summary>
    /// Висение на боку окна — хорошо читаемое: тело вертикально головой вверх, лапки вытянуты к кромке
    /// и перехватываются при лазании (в противофазе), клюв помогает: при подтягивании касается стены.
    /// Хвост упирается в стену, как подпорка; изредка попугай оглядывается в комнату и хлопает крыльями
    /// для равновесия; дышит заметнее обычного.
    /// </summary>
    private AnimationPose ClingPose(double dt, MovementService movement)
    {
        double climb = Math.Clamp(Math.Abs(movement.VelocityY) / 14, 0, 1);
        _climbPhase += dt * 2 * Math.PI * 2.4 * climb;
        double step = Math.Sin(_climbPhase);
        double pull = Math.Max(0, step) * climb;   // рывок вверх

        double glance = Math.Clamp(Math.Sin(2 * Math.PI * 0.13 * _time) * 2.5, 0, 1);       // взгляд в комнату
        double flutter = Math.Clamp((Math.Sin(2 * Math.PI * 0.15 * _time + 1) - 0.72) * 4, 0, 1); // взмахи для равновесия
        double look = 9 * Math.Sin(2 * Math.PI * 0.27 * _time);
        double grip = 4 * Math.Sin(2 * Math.PI * 0.9 * _time);                              // лапки подстраиваются

        return new AnimationPose(
            Rotation: ClingAngle - 4 + 2 * Math.Sin(2 * Math.PI * 0.4 * _time) - 4 * pull,
            ScaleY: 1 - 0.025 * pull,
            HeadRotation: look - 38 * glance + 26 * pull,
            WingScaleY: 0.84 + 0.35 * flutter + 0.08 * pull,
            WingRotation: 6 + 18 * flutter * Math.Sin(2 * Math.PI * 9 * _time) + 9 * climb * Math.Sin(_climbPhase * 2),
            TailRotation: -14 + 3 * Math.Sin(_climbPhase) - 3 * glance,
            Breath: 1 + 0.045 * Math.Sin(2 * Math.PI * 0.6 * _time),
            OffsetX: ClingLift + 3 * pull,
            FootRotation: grip + 22 * climb * step, FarFootRotation: -grip - 22 * climb * step,
            BeakOpen: 10 * pull);
    }

    /// <summary>Клевание: три наклона корпуса и головы вниз.</summary>
    private AnimationPose EatPose()
    {
        double u = Math.Min(1, _stateTime / MovementService.EatDuration);
        double peck = (1 - Math.Cos(2 * Math.PI * 3 * u)) / 2;
        return new AnimationPose(Rotation: 28 * peck, HeadRotation: 40 * peck, TailRotation: -5 * peck,
            BeakOpen: 18 * (1 - Math.Abs(2 * peck - 1)));
    }
}
