using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ParrotPet.Services;

/// <summary>Мониторы для списка в настройках: идентификатор и подпись.</summary>
public record ScreenInfo(string Id, string DisplayName);

/// <summary>
/// Информация о мониторах. Все границы возвращаются в WPF-единицах (DIP),
/// чтобы их можно было напрямую присваивать Left/Top окна.
/// </summary>
public class ScreenService
{
    public const string PrimaryId = "Primary";
    public const string AllId = "All";

    private record ScreenData(Rect Bounds, Rect Work, bool IsPrimary, int PixelWidth, int PixelHeight);

    private List<ScreenData> _cache = new();
    private long _cacheStamp = -100000;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    /// <summary>Список для ComboBox: основной, каждый монитор по номеру и «Все экраны».</summary>
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var data = GetData();
        var result = new List<ScreenInfo>();

        var primary = data.Find(s => s.IsPrimary) ?? data[0];
        result.Add(new ScreenInfo(PrimaryId, $"Основной монитор — {primary.PixelWidth}×{primary.PixelHeight}"));

        for (int i = 0; i < data.Count; i++)
        {
            string mark = data[i].IsPrimary ? ", основной" : "";
            result.Add(new ScreenInfo($"Screen {i + 1}", $"Экран {i + 1} — {data[i].PixelWidth}×{data[i].PixelHeight}{mark}"));
        }

        var virt = Forms.SystemInformation.VirtualScreen;
        result.Add(new ScreenInfo(AllId, $"Все экраны — {virt.Width}×{virt.Height}"));
        return result;
    }

    /// <summary>Область перемещения: рабочая область монитора или весь виртуальный экран.</summary>
    public Rect GetBounds(string id)
    {
        if (id == AllId)
        {
            return new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        }
        return Find(id).Work;
    }

    /// <summary>
    /// Нижняя граница «земли» (верх панели задач). В режиме «Все экраны» берётся
    /// монитор, над которым находится центр персонажа.
    /// </summary>
    public double GetGroundBottom(string id, double centerX)
    {
        if (id != AllId) return Find(id).Work.Bottom;

        var data = GetData();
        ScreenData best = data[0];
        double bestDistance = double.MaxValue;
        foreach (var s in data)
        {
            double distance = centerX < s.Work.Left ? s.Work.Left - centerX
                : centerX > s.Work.Right ? centerX - s.Work.Right : 0;
            if (distance < bestDistance) { best = s; bestDistance = distance; }
        }
        return best.Work.Bottom;
    }

    private ScreenData Find(string id)
    {
        var data = GetData();
        if (id.StartsWith("Screen ", StringComparison.Ordinal)
            && int.TryParse(id.AsSpan(7), out int n) && n >= 1 && n <= data.Count)
        {
            return data[n - 1];
        }
        // Неизвестный/отключённый монитор — откатываемся на основной
        return data.Find(s => s.IsPrimary) ?? data[0];
    }

    /// <summary>Кэш на 1 секунду: границы запрашиваются каждый кадр, а панель задач могут двигать.</summary>
    private List<ScreenData> GetData()
    {
        long now = Environment.TickCount64;
        if (_cache.Count > 0 && now - _cacheStamp < 1000) return _cache;

        double k = GetDpiForSystem() / 96.0;
        var list = new List<ScreenData>();
        foreach (var s in Forms.Screen.AllScreens)
        {
            list.Add(new ScreenData(ToDip(s.Bounds, k), ToDip(s.WorkingArea, k), s.Primary,
                s.Bounds.Width, s.Bounds.Height));
        }
        _cache = list;
        _cacheStamp = now;
        return list;
    }

    private static Rect ToDip(System.Drawing.Rectangle r, double k) =>
        new(r.X / k, r.Y / k, r.Width / k, r.Height / k);
}