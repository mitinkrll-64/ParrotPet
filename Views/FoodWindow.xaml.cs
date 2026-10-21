using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using ParrotPet.Services;
using ParrotPet.ViewModels;

namespace ParrotPet.Views;

/// <summary>Прозрачное окно с зерном и крошками. Не принимает клики и не забирает фокус.</summary>
public partial class FoodWindow : Window
{
    // Положение зерна внутри холста: центр по X и нижняя точка по Y
    private const double SeedCenterX = 60;
    private const double SeedBottomY = 70;

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    private readonly MainViewModel _viewModel;
    private readonly Ellipse[] _crumbs;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    public FoodWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        _crumbs = new[] { Crumb0, Crumb1, Crumb2 };

        // Клики проходят сквозь окно, оно не попадает в Alt+Tab и не активируется
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(handle, GwlExStyle);
            SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
        };

        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var food = _viewModel.Food;
        bool wanted = food.IsVisible && _viewModel.IsPetVisible;

        if (wanted && !IsVisible) Show();
        else if (!wanted && IsVisible) Hide();
        if (!IsVisible) return;

        double s = _viewModel.Scale;
        if (Math.Abs(Width - 120 * s) > 0.01)
        {
            Width = 120 * s;
            Height = 80 * s;
            FoodScale.ScaleX = s;
            FoodScale.ScaleY = s;
        }

        Left = food.X - SeedCenterX * s;
        Top = food.Y - SeedBottomY * s;

        SeedGroup.Visibility = food.Phase is FoodPhase.Falling or FoodPhase.Resting
            ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < FoodService.CrumbCount; i++)
        {
            var c = food.GetCrumb(i);
            _crumbs[i].Opacity = c.Alpha;
            if (c.Alpha <= 0) continue;
            // Крошки хранятся в координатах экрана; в холст переводим относительно зерна, в единицах до масштаба
            Canvas.SetLeft(_crumbs[i], SeedCenterX + (c.X - food.X) / s);
            Canvas.SetTop(_crumbs[i], SeedBottomY + (c.Y - food.Y) / s);
        }
    }
}