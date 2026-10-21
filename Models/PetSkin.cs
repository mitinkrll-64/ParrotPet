namespace ParrotPet.Models;

/// <summary>
/// Скин — только набор цветов (hex). Он ничего не знает про анимации:
/// части тела в MainWindow берут кисти из ресурсов Skin.*, которые подставляет SkinService.
/// </summary>
public class PetSkin
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Скин доступен для выбора (задел под будущий магазин).</summary>
    public bool IsUnlocked { get; init; } = true;

    public required string ColorBody { get; init; }        // грудка/спинка сверху
    public required string ColorBodyShade { get; init; }   // низ тела (градиент)
    public required string ColorHead { get; init; }
    public required string ColorStripes { get; init; }     // полоски на затылке и точки на горле
    public required string ColorWing { get; init; }
    public required string ColorWingStripes { get; init; } // волнистый узор на крыле
    public required string ColorBeak { get; init; }
    public required string ColorCheeks { get; init; }      // щёчное пятно
    public required string ColorCheeks2 { get; init; }     // малое щёчное пятно
    public required string ColorTail1 { get; init; }       // ступенчатый хвост: длинное перо
    public required string ColorTail2 { get; init; }
    public required string ColorTail3 { get; init; }
    public required string ColorFeet { get; init; }
}