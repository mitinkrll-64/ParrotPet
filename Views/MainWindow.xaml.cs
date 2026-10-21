using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParrotPet.ViewModels;

namespace ParrotPet.Views;

/// <summary>Прозрачное окно-оверлей с попугаем: рендер кадров и перетаскивание мышью.</summary>
public partial class MainWindow : Window
{
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastRender;
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel?.Start(ActualWidth, ActualHeight);
        ApplyPosition();
        _clock.Start();
        CompositionTarget.Rendering += OnRendering; // синхронно с частотой экрана
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (ViewModel is null) return;

        // RenderingEventArgs.RenderingTime одинаков для нескольких вызовов в одном кадре
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastRender) return;
        double dt = Math.Min((now - _lastRender).TotalSeconds, 0.05); // защита от скачков после лагов
        _lastRender = now;

        ViewModel.Tick(dt);
        ApplyPosition();
    }

    /// <summary>Переносит позицию и разворот из физики в окно.</summary>
    private void ApplyPosition()
    {
        if (ViewModel is null) return;
        var m = ViewModel.Movement;
        Left = m.X;
        Top = m.Y;
        ParrotFlip.ScaleX = m.FacingScale;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null) return;
        var grab = e.GetPosition(this);
        ViewModel.Movement.BeginDrag(grab.X, grab.Y);
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel is null || !IsMouseCaptured) return;
        // Курсор в координатах экрана (DIP) = позиция окна + позиция внутри окна
        var p = e.GetPosition(this);
        ViewModel.Movement.DragTo(Left + p.X, Top + p.Y);
        ApplyPosition();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;
        ReleaseMouseCapture();
        ViewModel?.Movement.EndDrag();
    }
}