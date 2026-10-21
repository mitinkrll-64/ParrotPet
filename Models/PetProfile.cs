using System;
using System.Text.Json.Serialization;

namespace ParrotPet.Models;

public enum PetGender
{
    Male,
    Female
}

/// <summary>Профиль питомца: кличка, пол, день рождения и сытость. Сохраняется в save_data.json.</summary>
public class PetProfile
{
    public const int MaxNameLength = 20;
    public const double MaxHunger = 100;

    /// <summary>Популярные клички волнистых попугаев.</summary>
    public static readonly string[] Names = { "Кеша", "Гоша", "Рио", "Чики", "Пико", "Арчи", "Киви", "Лимончик" };

    private string _name = "Кеша";
    private PetGender _gender = PetGender.Male;
    private DateTime _birthday = DateTime.Now;
    private double _hunger = 50;

    /// <summary>Любое изменение профиля (для обновления спрайта, UI и автосохранения).</summary>
    public event Action? Changed;

    /// <summary>Кличка; пустая или слишком длинная обрезается/игнорируется.</summary>
    public string Name
    {
        get => _name;
        set
        {
            string name = (value ?? "").Trim();
            if (name.Length == 0) return;
            if (name.Length > MaxNameLength) name = name[..MaxNameLength];
            if (name == _name) return;
            _name = name;
            Changed?.Invoke();
        }
    }

    /// <summary>Пол влияет на цвет восковицы: самец — синяя, самка — бежево-коричневая.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PetGender Gender
    {
        get => _gender;
        set
        {
            if (value == _gender) return;
            _gender = value;
            Changed?.Invoke();
        }
    }

    public DateTime Birthday
    {
        get => _birthday;
        set
        {
            if (value == _birthday) return;
            _birthday = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Сытость 0–100 (больше — сытее).</summary>
    public double Hunger
    {
        get => _hunger;
        set
        {
            double v = double.IsFinite(value) ? Math.Clamp(value, 0, MaxHunger) : _hunger;
            if (v == _hunger) return;
            _hunger = v;
            Changed?.Invoke();
        }
    }

    /// <summary>Возраст в днях; растёт сам со временем, т.к. считается от дня рождения.</summary>
    [JsonIgnore]
    public int AgeDays
    {
        get => Math.Max(0, (int)(DateTime.Now - _birthday).TotalDays);
        set => Birthday = DateTime.Now.AddDays(-Math.Clamp(value, 0, 36500));
    }

    /// <summary>Новый питомец: случайная кличка и пол, возраст 0, сытость 50.</summary>
    public static PetProfile CreateRandom() => new()
    {
        _name = RandomName(),
        _gender = Random.Shared.Next(2) == 0 ? PetGender.Male : PetGender.Female,
        _birthday = DateTime.Now,
        _hunger = 50
    };

    public static string RandomName() => Names[Random.Shared.Next(Names.Length)];

    /// <summary>Копирует значения (одно событие Changed в конце).</summary>
    public void CopyFrom(PetProfile other)
    {
        _name = other._name;
        _gender = other._gender;
        _birthday = other._birthday;
        _hunger = other._hunger;
        Changed?.Invoke();
    }
}