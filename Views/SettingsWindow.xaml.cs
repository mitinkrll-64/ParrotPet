using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.Views;

/// <summary>Окно настроек: монитор, размер и скорость. «Применить» — на лету, «Сохранить» — ещё и в файл.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly SettingsModel _draft;

    public SettingsWindow(SettingsService settings, ScreenService screens)
    {
        _settings = settings;
        _draft = settings.Current.Clone();

        ApplySystemTheme();
        InitializeComponent();

        ScreenCombo.ItemsSource = screens.GetScreens();
        DataContext = _draft;
    }

    /// <summary>Тёмная или светлая тема окна — по настройке Windows.</summary>
    private void ApplySystemTheme()
    {
        bool light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is not int value || value != 0;

        Background = new SolidColorBrush(light ? Color.FromRgb(0xF3, 0xF3, 0xF3) : Color.FromRgb(0x20, 0x20, 0x20));
        Foreground = new SolidColorBrush(light ? Colors.Black : Color.FromRgb(0xF0, 0xF0, 0xF0));
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => _settings.Apply(_draft);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Apply(_draft);
        _settings.Save();
        Close();
    }
}