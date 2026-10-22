namespace ParrotPet.Models;

/// <summary>Фазы полёта внутри состояния Fly. Зависания нет: попугай летит сразу к точке посадки.</summary>
public enum FlightPhase
{
    /// <summary>Набор высоты и перелёт по дуге.</summary>
    Cruise,
    /// <summary>Заход на посадку: торможение крыльями, нос вверх.</summary>
    Approach
}
