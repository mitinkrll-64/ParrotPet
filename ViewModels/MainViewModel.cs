using System;
using ParrotPet.Models;

namespace ParrotPet.ViewModels;

/// <summary>ViewModel главного окна: хранит состояние попугая и сообщает об изменении видимости.</summary>
public class MainViewModel
{
    private readonly ParrotState _state = new();

    /// <summary>Вызывается при смене видимости; true — показать окно.</summary>
    public event Action<bool>? VisibilityChanged;

    /// <summary>Переключает видимость окна («Показать/Скрыть»).</summary>
    public void ToggleVisibility()
    {
        _state.IsVisible = !_state.IsVisible;
        VisibilityChanged?.Invoke(_state.IsVisible);
    }
}