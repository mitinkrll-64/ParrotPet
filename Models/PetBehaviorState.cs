namespace ParrotPet.Models;

/// <summary>Состояния конечного автомата поведения попугая.</summary>
public enum PetBehaviorState
{
    /// <summary>Сидит на панели задач.</summary>
    Idle,
    /// <summary>Идёт влево или вправо по панели задач.</summary>
    Walk,
    /// <summary>Летит по дуге в случайную точку и парит там.</summary>
    Fly,
    /// <summary>Падает под действием гравитации с пружинящим приземлением.</summary>
    Fall,
    /// <summary>Удерживается мышью.</summary>
    Dragged,
    /// <summary>Клюёт корм (несколько наклонов вниз).</summary>
    Eat
}