using System;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.ViewModels;

/// <summary>ViewModel главного окна: состояние попугая, физика и автономное поведение.</summary>
public class MainViewModel
{
    private readonly ParrotState _state = new();
    private readonly BehaviorController _behavior;

    public MovementService Movement { get; } = new();

    /// <summary>Вызывается при смене видимости; true — показать окно.</summary>
    public event Action<bool>? VisibilityChanged;

    public MainViewModel()
    {
        _behavior = new BehaviorController(Movement);
    }

    /// <summary>Запуск после того, как известны размеры окна.</summary>
    public void Start(double width, double height)
    {
        Movement.SpriteWidth = width;
        Movement.SpriteHeight = height;
        Movement.PlaceOnGround();
        _behavior.Start();
    }

    /// <summary>Кадр анимации; dt — секунды с прошлого кадра.</summary>
    public void Tick(double dt) => Movement.Update(dt);

    /// <summary>Переключает видимость окна («Показать/Скрыть»).</summary>
    public void ToggleVisibility()
    {
        _state.IsVisible = !_state.IsVisible;
        VisibilityChanged?.Invoke(_state.IsVisible);
    }
}