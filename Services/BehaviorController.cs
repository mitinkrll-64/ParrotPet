using System;
using System.Windows.Threading;

namespace ParrotPet.Services;

/// <summary>Раз в 7–15 секунд выбирает случайное действие для попугая.</summary>
public class BehaviorController
{
    private readonly MovementService _movement;
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer = new();

    public BehaviorController(MovementService movement)
    {
        _movement = movement;
        _timer.Tick += (_, _) => OnTick();
    }

    private bool _enabled = true;

    /// <summary>Случайное поведение включено (в Debug-окне можно выключить).</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (value) ScheduleNext();
            else _timer.Stop();
        }
    }

    public void Start() => Reschedule();

    /// <summary>Отсчитывает следующее случайное действие заново (после ручного вмешательства).</summary>
    public void Reschedule()
    {
        if (_enabled) ScheduleNext();
    }

    public void Stop() => _timer.Stop();

    private void ScheduleNext()
    {
        _timer.Interval = TimeSpan.FromSeconds(7 + _random.NextDouble() * 8);
        _timer.Start();
    }

    private void OnTick()
    {
        _timer.Stop();

        // 0 — посидеть, 1–2 — пройтись, 3–4 — взлететь (панель задач, верх окна или бок окна).
        // Клевание запускается только кормлением.
        switch (_random.Next(5))
        {
            case 1:
            case 2:
                double distance = 100 + _random.NextDouble() * 100;
                _movement.StartWalk(_random.Next(2) == 0 ? -distance : distance);
                break;
            case 3:
            case 4:
                _movement.StartFly();
                break;
        }

        ScheduleNext();
    }
}