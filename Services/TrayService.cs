using System;
using System.Drawing;
using System.Windows.Forms;

namespace ParrotPet.Services;

/// <summary>Иконка в системном трее с контекстным меню.</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;

    public TrayService(Action onToggle, Action onFeed, Action onSettings, Action onExit)
    {
        _icon = CreateIcon();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Показать/Скрыть", null, (_, _) => onToggle());
        menu.Items.Add("Покормить", null, (_, _) => onFeed());
        menu.Items.Add("Настройки...", null, (_, _) => onSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => onExit());

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "ParrotPet",
            ContextMenuStrip = menu,
            Visible = true
        };

        // Двойной клик по иконке тоже переключает видимость
        _notifyIcon.DoubleClick += (_, _) => onToggle();
    }

    /// <summary>Рисует простую зелёную иконку, чтобы не хранить бинарный .ico в репозитории.</summary>
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.LimeGreen, 2, 2, 28, 28);
            g.FillEllipse(Brushes.White, 18, 9, 7, 7);
            g.FillEllipse(Brushes.Black, 21, 11, 3, 3);
        }
        // Icon.FromHandle не владеет дескриптором — клонируем и освобождаем исходный
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}