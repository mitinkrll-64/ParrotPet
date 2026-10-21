namespace ParrotPet.Models;

/// <summary>Временное состояние питомца в рамках сессии (сохраняемые данные — в PetProfile).</summary>
public class ParrotState
{
    public bool IsVisible { get; set; } = true;
}