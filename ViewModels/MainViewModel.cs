using System;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.ViewModels;

/// <summary>ViewModel главного окна: состояние попугая, физика, анимации и автономное поведение.</summary>
public class MainViewModel
{
    private readonly ParrotState _state = new();
    private readonly BehaviorController _behavior;
    private readonly SettingsService _settings;

    public MovementService Movement { get; }
    public AnimationService Animator { get; } = new();

    /// <summary>Вызывается при смене видимости; true — показать окно.</summary>
    public event Action<bool>? VisibilityChanged;

    public MainViewModel(SettingsService settings, ScreenService screens)
    {
        _settings = settings;
        Movement = new MovementService(screens, settings.Current);
        _behavior = new BehaviorController(Movement);

        Movement.Landed += Animator.TriggerLand;
        settings.Changed += Movement.OnSettingsChanged;
    }

    /// <summary>Масштаб из настроек (применяется к ScaleTransform окна).</summary>
    public double Scale => _settings.Current.Scale;

    public void Start()
    {
        Movement.PlaceOnGround();
        _behavior.Start();
    }

    /// <summary>Кадр анимации; dt — секунды с прошлого кадра.</summary>
    public void Tick(double dt)
    {
        Movement.Update(dt);
        Animator.Update(dt, Movement.State, _settings.Current.BehaviorSpeed);
    }

    /// <summary>Покормить попугая (клевание).</summary>
    public void Feed() => Movement.StartEat();

    /// <summary>Переключает видимость окна («Показать/Скрыть»).</summary>
    public void ToggleVisibility()
    {
        _state.IsVisible = !_state.IsVisible;
        VisibilityChanged?.Invoke(_state.IsVisible);
    }
}