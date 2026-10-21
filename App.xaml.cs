using System.Windows;
using ParrotPet.Services;
using ParrotPet.ViewModels;
using ParrotPet.Views;

namespace ParrotPet;

/// <summary>Точка входа: создаёт сервисы, главное окно и иконку в трее.</summary>
public partial class App : Application
{
    private readonly SettingsService _settings = new();
    private readonly ScreenService _screens = new();
    private TrayService? _tray;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings.Load();

        var viewModel = new MainViewModel(_settings, _screens);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        // Трей управляет видимостью окна, кормлением, настройками и завершением приложения
        _tray = new TrayService(
            onToggle: () => viewModel.ToggleVisibility(),
            onFeed: () => viewModel.Feed(),
            onSettings: ShowSettings,
            onExit: () => Shutdown());

        viewModel.VisibilityChanged += visible =>
        {
            if (visible) window.Show();
            else window.Hide();
        };
    }

    /// <summary>Открывает окно настроек (одно на всё приложение).</summary>
    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, _screens);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings.Save(); // автосохранение при выходе
        // Обязательно убираем иконку, иначе она «зависнет» в трее
        _tray?.Dispose();
        base.OnExit(e);
    }
}