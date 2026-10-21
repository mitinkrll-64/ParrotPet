using System;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.ViewModels;

/// <summary>ViewModel главного окна: профиль, физика, зерно, анимации и автономное поведение.</summary>
public class MainViewModel
{
    private const double FeedAmount = 20;

    private readonly ParrotState _state = new();
    private readonly SettingsService _settings;

    public MovementService Movement { get; }
    public FoodService Food { get; }
    public AnimationService Animator { get; } = new();
    public BehaviorController Behavior { get; }
    public PetProfile Profile { get; }

    /// <summary>Вызывается при смене видимости; true — показать окно.</summary>
    public event Action<bool>? VisibilityChanged;

    /// <summary>Запрос открыть окно настроек (меню попугая, трей).</summary>
    public event Action? SettingsRequested;

    /// <summary>Запрос открыть/закрыть Debug-окно (меню, хоткей, F12).</summary>
    public event Action? DebugToggleRequested;

    public MainViewModel(SettingsService settings, ScreenService screens, PetProfile profile)
    {
        _settings = settings;
        Profile = profile;
        Food = new FoodService(screens, settings.Current);
        Movement = new MovementService(screens, settings.Current, Food);
        Behavior = new BehaviorController(Movement);

        Movement.Landed += Animator.TriggerLand;
        Movement.FoodEaten += OnFoodEaten;
        settings.Changed += Movement.OnSettingsChanged;
        settings.Changed += Food.OnSettingsChanged;
    }

    /// <summary>Масштаб из настроек (применяется к ScaleTransform окон).</summary>
    public double Scale => _settings.Current.Scale;

    public bool IsPetVisible => _state.IsVisible;

    public void Start()
    {
        Movement.PlaceOnGround();
        Behavior.Start();
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
        Profile.Hunger += FeedAmount;
    }

    // ---- Ручной запуск действий (Debug-окно). Каждое прерывает текущее действие FSM
    // и откладывает следующее случайное, чтобы автономное поведение не мешало тесту.

    public void PlayIdle()
    {
        Food.Cancel();
        Movement.Interrupt();
        Behavior.Reschedule();
    }

    public void PlayWalk(int direction)
    {
        Movement.Interrupt();
        Movement.StartWalk(direction * 150);
        Behavior.Reschedule();
    }

    public void PlayFly()
    {
        Movement.Interrupt();
        Movement.StartFly();
        Behavior.Reschedule();
    }

    public void PlayFall()
    {
        Movement.ForceFall();
        Behavior.Reschedule();
    }

    /// <summary>Полный цикл: новое зерно, подход, 3 клевка, крошки и сердечко.</summary>
    public void PlayEat()
    {
        Food.Cancel();
        Movement.Interrupt();
        Food.Drop(Movement.X + Movement.Size / 2);
        Behavior.Reschedule();
    }

    public void PlayPreen()
    {
        Movement.Interrupt();
        Animator.RequestPreen();
        Behavior.Reschedule();
    }

    public void PlayHeart() => Animator.TriggerHeart();

    public void TeleportToCenter()
    {
        Movement.TeleportToCenter();
        Behavior.Reschedule();
    }

    /// <summary>Переключает видимость окна («Показать/Скрыть»).</summary>
    public void ToggleVisibility()
    {
        _state.IsVisible = !_state.IsVisible;
        VisibilityChanged?.Invoke(_state.IsVisible);
    }

    public void RequestSettings() => SettingsRequested?.Invoke();

    public void ToggleDebug() => DebugToggleRequested?.Invoke();
}