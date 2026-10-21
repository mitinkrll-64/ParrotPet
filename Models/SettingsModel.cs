using System;

namespace ParrotPet.Models;

/// <summary>Пользовательские настройки; хранятся в settings.json.</summary>
public class SettingsModel
{
    public const double MinScale = 0.5;
    public const double MaxScale = 2.0;

    /// <summary>"Primary", "Screen 1", "Screen 2", ... или "All".</summary>
    public string SelectedScreen { get; set; } = "Primary";

    /// <summary>Коэффициент размера персонажа (0.5–2.0).</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Множитель скорости перемещения (0.5–2.0).</summary>
    public double BehaviorSpeed { get; set; } = 1.0;

    public SettingsModel Clone() => (SettingsModel)MemberwiseClone();

    public void CopyFrom(SettingsModel other)
    {
        SelectedScreen = other.SelectedScreen;
        Scale = other.Scale;
        BehaviorSpeed = other.BehaviorSpeed;
    }

    /// <summary>Приводит значения к допустимым (на случай ручной правки json).</summary>
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(SelectedScreen)) SelectedScreen = "Primary";
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, MinScale, MaxScale) : 1.0;
        BehaviorSpeed = double.IsFinite(BehaviorSpeed) ? Math.Clamp(BehaviorSpeed, MinScale, MaxScale) : 1.0;
    }
}