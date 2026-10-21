using System;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.ViewModels;

/// <summary>ViewModel главного окна: состояние попугая, физика, зерно, анимации и автономное поведение.</summary>
public class MainViewModel
{
    private const int FeedSatiety = 20;

    private readonly ParrotState _state = new();
    private readonly BehaviorController _behavior;
    private readonly SettingsService _settings;

    public MovementService Movement { get; }
    public FoodService Food { get; }
    public AnimationService Animator { get; } = new();

    /// <summary>Вызывается при смене видимости; true — показать окно.</summary>
    public event Action<bool>? VisibilityChanged;

    /// <summary>Сытость изменилась (новое значение 0–100).</summary>
    public event Action<int>? SatietyChanged;

    public MainViewModel(SettingsService settings, ScreenService screens)
    {
        _settings = settings;
        Food = new FoodService(screens, settings.Current);
        Movement = new MovementService(screens, settings.Current, Food);
        _behavior = new BehaviorController(Movement);

        Movement.Landed += Animator.TriggerLand;
        Movement.FoodEaten += OnFoodEaten;
        settings.Changed += Movement.OnSettingsChanged;
        settings.Changed += Food.OnSettingsChanged;
    }

    /// <summary>Масштаб из настроек (применяется к ScaleTransform окон).</summary>
    public double Scale => _settings.Current.Scale;

    public bool IsPetVisible => _state.IsVisible;

    public int Satiety => _state.Satiety;

    public void Start()
    {
        Movement.PlaceOnGround();
        _behavior.Start();
    }

    /// <summary>Кадр анимации; dt — секунды с прошлого кадра.</summary>
    public void Tick(double dt)
    {
        Food.Update(dt);
        Movement.Update(dt);
        Animator.Update(dt, Movement, _settings.Current.BehaviorSpeed);
    }

    /// <summary>Покормить: зерно падает рядом с попугаем (одно зерно за раз).</summary>
    public void Feed()
    {
        if (Food.IsVisible) return;
        Food.Drop(Movement.X + Movement.Size / 2);
    }

    private void OnFoodEaten()
    {
        // Зерно могли убрать (смена монитора) — тогда сытость не начисляем
        if (!Food.IsLanded) return;
        Food.Consume();
        Animator.TriggerHeart();
        _state.AddSatiety(FeedSatiety);
        SatietyChanged?.Invoke(_state.Satiety);
    }

    /// <summary>Переключает видимость окна («Показать/Скрыть»).</summary>
    public void ToggleVisibility()
    {
        _state.IsVisible = !_state.IsVisible;
        VisibilityChanged?.Invoke(_state.IsVisible);
    }
}