using System;

namespace ParrotPet.Models;

/// <summary>Состояние питомца: видимость и сытость.</summary>
public class ParrotState
{
    public const int MaxSatiety = 100;

    public bool IsVisible { get; set; } = true;

    /// <summary>Шкала сытости 0–100.</summary>
    public int Satiety { get; private set; } = 50;

    public void AddSatiety(int amount) => Satiety = Math.Clamp(Satiety + amount, 0, MaxSatiety);
}