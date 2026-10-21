using System.Windows;
using System.Windows.Input;

namespace ParrotPet.Views;

/// <summary>Прозрачное окно-оверлей с попугаем, которое можно таскать мышью.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Перетаскивание окна за любую непрозрачную область.</summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}