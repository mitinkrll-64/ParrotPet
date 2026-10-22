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

        // Вес действий: посидеть 1, пройтись 2, взлететь 2, развернуться 1, посмотреть на пользователя 2.
        // Клевание запускается только кормлением.
        int roll = _random.Next(8);
        if (roll == 0)
        {
            // посидеть
        }
        else if (roll <= 2)
        {
            double distance = 100 + _random.NextDouble() * 100;
            _movement.StartWalk(_random.Next(2) == 0 ? -distance : distance);
        }
        else if (roll <= 4)
        {
            _movement.StartFly();
        }
        else if (roll == 5)
        {
            _movement.TurnAround();
        }
        else
        {
            _movement.FaceUser(8 + _random.NextDouble() * 10);
        }
        ScheduleNext();
    }
}