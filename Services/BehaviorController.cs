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

    public void Start() => ScheduleNext();

    public void Stop() => _timer.Stop();

    private void ScheduleNext()
    {
        _timer.Interval = TimeSpan.FromSeconds(7 + _random.NextDouble() * 8);
        _timer.Start();
    }

    private void OnTick()
    {
        _timer.Stop();

        // 0 — посидеть, 1 — пройтись, 2 — взлететь (клевание запускается только кормлением)
        switch (_random.Next(3))
        {
            case 1:
                double distance = 100 + _random.NextDouble() * 100;
                _movement.StartWalk(_random.Next(2) == 0 ? -distance : distance);
                break;
            case 2:
                _movement.StartFly();
                break;
        }

        ScheduleNext();
    }
}