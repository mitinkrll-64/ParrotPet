using System.Windows;
using ParrotPet.Services;
using ParrotPet.ViewModels;
using ParrotPet.Views;

namespace ParrotPet;

/// <summary>Точка входа: создаёт главное окно и иконку в трее.</summary>
public partial class App : Application
{
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        // Трей управляет видимостью окна и завершением приложения
        _tray = new TrayService(
            onToggle: () => viewModel.ToggleVisibility(),
            onExit: () => Shutdown());

        viewModel.VisibilityChanged += visible =>
        {
            if (visible) window.Show();
            else window.Hide();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Обязательно убираем иконку, иначе она «зависнет» в трее
        _tray?.Dispose();
        base.OnExit(e);
    }
}