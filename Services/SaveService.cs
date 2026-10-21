using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>Загрузка и сохранение профиля питомца в save_data.json рядом с exe.</summary>
public class SaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "save_data.json");
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _loading;

    /// <summary>Живой профиль; на него ссылаются остальные части приложения.</summary>
    public PetProfile Profile { get; } = new();

    public SaveService()
    {
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Save();
        };
        // Автосохранение с задержкой: ползунок сытости не должен писать файл на каждый пиксель
        Profile.Changed += () =>
        {
            if (_loading) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        };
    }

    /// <summary>Читает сохранение; если его нет или оно повреждено — создаёт нового питомца.</summary>
    public void Load()
    {
        _loading = true;
        try
        {
            var loaded = File.Exists(_path)
                ? JsonSerializer.Deserialize<PetProfile>(File.ReadAllText(_path))
                : null;
            Profile.CopyFrom(loaded ?? PetProfile.CreateRandom());
        }
        catch (Exception)
        {
            Profile.CopyFrom(PetProfile.CreateRandom());
        }
        finally
        {
            _loading = false;
        }

        // Проверяем значения, которые могли испортить ручной правкой
        if (Profile.Birthday > DateTime.Now) Profile.Birthday = DateTime.Now;
        Save();
    }

    public void Save()
    {
        _saveTimer.Stop();
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Profile, JsonOptions));
        }
        catch (Exception)
        {
            // Нет прав на запись рядом с exe — прогресс просто не сохранится
        }
    }

    /// <summary>Удаляет сохранение и заводит нового питомца.</summary>
    public void Reset()
    {
        try { File.Delete(_path); }
        catch (Exception) { /* файла нет или занят — не страшно */ }
        Profile.CopyFrom(PetProfile.CreateRandom()); // Changed запустит автосохранение
    }
}