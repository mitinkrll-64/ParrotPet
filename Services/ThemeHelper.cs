using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ParrotPet.Services;

/// <summary>Светлая или тёмная тема служебных окон — по настройке Windows.</summary>
public static class ThemeHelper
{
    public static void Apply(Window window)
    {
        bool light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is not int value || value != 0;

        window.Background = new SolidColorBrush(light ? Color.FromRgb(0xF3, 0xF3, 0xF3) : Color.FromRgb(0x20, 0x20, 0x20));
        window.Foreground = new SolidColorBrush(light ? Colors.Black : Color.FromRgb(0xF0, 0xF0, 0xF0));
    }
}