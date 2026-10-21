namespace ParrotPet.Models;

/// <summary>Фазы полёта внутри состояния Fly.</summary>
public enum FlightPhase
{
    /// <summary>Перелёт к цели по дуге.</summary>
    Cruise,
    /// <summary>Зависание на месте.</summary>
    Hover,
    /// <summary>Плавное планирование вниз до панели задач.</summary>
    Descend
}