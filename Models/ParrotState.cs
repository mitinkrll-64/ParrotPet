namespace ParrotPet.Models;

/// <summary>Состояние питомца (пока только видимость, позже — настроение, анимации).</summary>
public class ParrotState
{
    public bool IsVisible { get; set; } = true;
}