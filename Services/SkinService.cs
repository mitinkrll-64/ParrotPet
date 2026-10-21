using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Реестр скинов и их применение. Скин меняет только кисти в ресурсах приложения (Skin.*),
/// поэтому текущая анимация, поза и позиция не затрагиваются.
/// </summary>
public class SkinService
{
    public const string DefaultSkinId = "classic-green";

    /// <summary>Шесть пресетов по референсу Assets/Skins/skins.jpg.</summary>
    public static IReadOnlyList<PetSkin> All { get; } = new[]
    {
        new PetSkin
        {
            Id = DefaultSkinId, DisplayName = "Классический зелёный",
            ColorBody = "#8DE35C", ColorBodyShade = "#45B84A", ColorHead = "#FFEB5B", ColorStripes = "#37474F",
            ColorWing = "#3FA146", ColorWingStripes = "#263238", ColorBeak = "#F2B84B",
            ColorCheeks = "#6A4FE0", ColorCheeks2 = "#2F7BFF",
            ColorTail1 = "#2563C9", ColorTail2 = "#4C93F0", ColorTail3 = "#7DB4FA", ColorFeet = "#D98A2B"
        },
        new PetSkin
        {
            Id = "sky-blue", DisplayName = "Небесно-голубой",
            ColorBody = "#6EC6FF", ColorBodyShade = "#2C9CE8", ColorHead = "#F4F8FF", ColorStripes = "#3B4B66",
            ColorWing = "#3E4D66", ColorWingStripes = "#DCE6F2", ColorBeak = "#E9A66B",
            ColorCheeks = "#3E6BE0", ColorCheeks2 = "#5B3FD6",
            ColorTail1 = "#1E88E5", ColorTail2 = "#4FA9F0", ColorTail3 = "#8CCBFA", ColorFeet = "#E39A7B"
        },
        new PetSkin
        {
            Id = "lutino", DisplayName = "Лютино (кремовый)",
            ColorBody = "#FFF0B3", ColorBodyShade = "#F5D77A", ColorHead = "#FFFBE0", ColorStripes = "#E2C468",
            ColorWing = "#EBCB6A", ColorWingStripes = "#C9A43F", ColorBeak = "#EBC48A",
            ColorCheeks = "#FF7043", ColorCheeks2 = "#FF8A65",
            ColorTail1 = "#F2D36B", ColorTail2 = "#F7E19A", ColorTail3 = "#FBEFC0", ColorFeet = "#D9B38C"
        },
        new PetSkin
        {
            Id = "violet-opaline", DisplayName = "Фиолетовый опалин",
            ColorBody = "#C9B8F0", ColorBodyShade = "#A58FDB", ColorHead = "#FFFFFF", ColorStripes = "#B3A3DE",
            ColorWing = "#9A86D4", ColorWingStripes = "#7A66B8", ColorBeak = "#F0B94F",
            ColorCheeks = "#8E7AD8", ColorCheeks2 = "#B7A8EC",
            ColorTail1 = "#8E7AD0", ColorTail2 = "#B2A2E8", ColorTail3 = "#D2C8F5", ColorFeet = "#E6B26B"
        },
        new PetSkin
        {
            Id = "teal-grey", DisplayName = "Бирюзово-серый",
            ColorBody = "#B9C4C6", ColorBodyShade = "#98A8AC", ColorHead = "#BFCBCD", ColorStripes = "#8FA0A4",
            ColorWing = "#2FA7A8", ColorWingStripes = "#1F7F85", ColorBeak = "#E39B61",
            ColorCheeks = "#7FB5B8", ColorCheeks2 = "#5FA3A8",
            ColorTail1 = "#1F8F93", ColorTail2 = "#2FB0B2", ColorTail3 = "#6FCFCF", ColorFeet = "#9A8F87"
        },
        new PetSkin
        {
            Id = "sunset-orange", DisplayName = "Закатный оранжевый",
            ColorBody = "#FF9A3C", ColorBodyShade = "#F26B1D", ColorHead = "#FF8A2B", ColorStripes = "#C85A12",
            ColorWing = "#8F9A2F", ColorWingStripes = "#6D7522", ColorBeak = "#E7B08A",
            ColorCheeks = "#FF5A36", ColorCheeks2 = "#FFB347",
            ColorTail1 = "#D8342B", ColorTail2 = "#F25C3A", ColorTail3 = "#FF9A6A", ColorFeet = "#6D5A4C"
        }
    };

    private readonly PetProfile _profile;
    private PetGender _gender;

    public SkinService(PetProfile profile)
    {
        _profile = profile;
        Active = All[0];
        // Id скина могли поменять не через ApplySkin (сброс сохранения) — подтягиваем внешний вид
        profile.Changed += OnProfileChanged;
    }

    public PetSkin Active { get; private set; }

    /// <summary>Применяет скин из профиля; неизвестный или закрытый заменяется на стандартный.</summary>
    public void Initialize()
    {
        if (!ApplySkin(_profile.ActiveSkinId)) ApplySkin(DefaultSkinId);
    }

    /// <summary>
    /// Мгновенно меняет окрас и запоминает выбор в профиле. Трансформации анимаций
    /// не трогаются, поэтому полёт, ходьба и клевание продолжаются без сброса.
    /// </summary>
    public bool ApplySkin(string? skinId)
    {
        var skin = All.FirstOrDefault(s => s.Id == skinId);
        if (skin is null || !skin.IsUnlocked) return false;

        Active = skin;
        ApplyResources();
        _profile.ActiveSkinId = skin.Id; // сохранится вместе с профилем в save_data.json
        return true;
    }

    /// <summary>Перерисовывает слои спрайта под активный скин и пол (восковица зависит от пола).</summary>
    private void ApplyResources()
    {
        var resources = (Application.Current ?? throw new InvalidOperationException("Нет Application")).Resources;
        var images = PixelSpriteBuilder.Build(Active, _profile.Gender);
        resources["Skin.TailImage"] = images.Tail;
        resources["Skin.BodyImage"] = images.Body;
        resources["Skin.WingImage"] = images.Wing;
        resources["Skin.HeadImage"] = images.Head;
        _gender = _profile.Gender;
    }

    private void OnProfileChanged()
    {
        if (_profile.ActiveSkinId != Active.Id)
        {
            if (!ApplySkin(_profile.ActiveSkinId)) ApplySkin(DefaultSkinId);
        }
        else if (_profile.Gender != _gender)
        {
            ApplyResources(); // пол сменился — только восковица на голове
        }
    }
}
