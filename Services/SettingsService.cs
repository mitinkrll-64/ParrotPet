using System;
using System.IO;
using System.Text.Json;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>Загрузка, применение и сохранение настроек (settings.json рядом с exe).</summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "settings.json");

    /// <summary>Живые настройки; на этот объект ссылаются остальные сервисы.</summary>
    public SettingsModel Current { get; } = new();

    /// <summary>Вызывается после применения новых настроек.</summary>
    public event Action? Changed;

    public void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var loaded = JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(_path));
            if (loaded is null) return;
            loaded.Normalize();
            Current.CopyFrom(loaded);
        }
        catch (Exception)
        {
            // Битый файл не должен ронять приложение — остаются значения по умолчанию
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (Exception)
        {
            // Нет прав на запись рядом с exe — настройки просто не сохранятся
        }
    }

    /// <summary>Применяет настройки на лету, не записывая файл.</summary>
    public void Apply(SettingsModel model)
    {
        var copy = model.Clone();
        copy.Normalize();
        Current.CopyFrom(copy);
        Changed?.Invoke();
    }
}