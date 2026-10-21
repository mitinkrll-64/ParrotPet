using System.Windows;
using System.Windows.Input;
using ParrotPet.Services;
using ParrotPet.ViewModels;

namespace ParrotPet.Views;

/// <summary>Служебное окно разработчика (тест анимаций, профиля и механик).</summary>
public partial class DebugWindow : Window
{
    private readonly MainViewModel _main;

    public DebugWindow(DebugViewModel viewModel, MainViewModel main)
    {
        _main = main;
        ThemeHelper.Apply(this);
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>F12 в окне закрывает его (Ctrl+Shift+D работает глобально).</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            e.Handled = true;
            _main.ToggleDebug();
        }
    }
}