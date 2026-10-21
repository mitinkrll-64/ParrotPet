using System.Windows;
using ParrotPet.Services;
using ParrotPet.ViewModels;
using ParrotPet.Views;

namespace ParrotPet;

/// <summary>Точка входа: создаёт сервисы, главное окно и иконку в трее.</summary>
public partial class App : Application
{
    private readonly SettingsService _settings = new();
    private readonly SaveService _save = new();
    private readonly ScreenService _screens = new();
    private SkinService? _skins;
    private TrayService? _tray;
    private HotkeyService? _hotkey;
    private MainViewModel? _viewModel;
    private SettingsWindow? _settingsWindow;
    private DebugWindow? _debugWindow;
    private FoodWindow? _foodWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings.Load();
        _save.Load();

        // Скин применяем до создания окон: части спрайта берут кисти из ресурсов Skin.*
        _skins = new SkinService(_save.Profile);
        _skins.Initialize();

        var viewModel = new MainViewModel(_settings, _screens, _save.Profile);
        _viewModel = viewModel;
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        // Окно с зерном создаём сразу, но показывается оно только пока зерно на экране
        _foodWindow = new FoodWindow(viewModel);

        // Трей управляет видимостью окна, кормлением, настройками, отладкой и завершением приложения
        _tray = new TrayService(
            onToggle: () => viewModel.ToggleVisibility(),
            onFeed: () => viewModel.Feed(),
            onSettings: ShowSettings,
            onDebug: () => viewModel.ToggleDebug(),
            onExit: () => Shutdown());

        // Подсказка иконки: кличка и сытость
        UpdateTooltip();
        _save.Profile.Changed += UpdateTooltip;

        viewModel.VisibilityChanged += visible =>
        {
            if (visible) window.Show();
            else window.Hide();
        };

        // Debug-окно: пункт меню, глобальный Ctrl+Shift+D и F12 (когда окно питомца или Debug в фокусе)
        viewModel.DebugToggleRequested += ToggleDebug;
        _hotkey = new HotkeyService(() => viewModel.ToggleDebug());
    }

    private void UpdateTooltip()
    {
        var p = _save.Profile;
        _tray?.SetTooltip($"{p.Name} — сытость: {p.Hunger:0}/100");
    }

    /// <summary>Открывает окно настроек (одно на всё приложение).</summary>
    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, _screens, _save.Profile, _skins!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    /// <summary>Открывает Debug-окно или закрывает, если оно уже открыто.</summary>
    private void ToggleDebug()
    {
        if (_debugWindow is not null)
        {
            _debugWindow.Close();
            return;
        }
        if (_viewModel is null) return;

        var debugViewModel = new DebugViewModel(_viewModel, _skins!, _save);
        _debugWindow = new DebugWindow(debugViewModel, _viewModel);
        _debugWindow.Closed += (_, _) =>
        {
            debugViewModel.Dispose();
            _debugWindow = null;
        };
        _debugWindow.Show();
        _debugWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings.Save(); // автосохранение при выходе
        _save.Save();
        _hotkey?.Dispose();
        // Обязательно убираем иконку, иначе она «зависнет» в трее
        _tray?.Dispose();
        base.OnExit(e);
    }
}