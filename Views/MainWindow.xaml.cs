using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParrotPet.ViewModels;

namespace ParrotPet.Views;

/// <summary>Прозрачное окно-оверлей с попугаем: рендер кадров и перетаскивание мышью.</summary>
public partial class MainWindow : Window
{
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
        ViewModel?.Start();
        ApplyPosition();
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

    /// <summary>Переносит размер, позицию, разворот и позу анимации из сервисов в окно.</summary>
    private void ApplyPosition()
    {
        if (ViewModel is null) return;
        var m = ViewModel.Movement;

        // Масштаб применяется на лету: окно остаётся квадратным
        double size = m.Size;
        if (Math.Abs(Width - size) > 0.01)
        {
            Width = size;
            Height = size;
            ContentScale.ScaleX = ViewModel.Scale;
            ContentScale.ScaleY = ViewModel.Scale;
        }

        Left = m.X;
        Top = m.Y;

        var pose = ViewModel.Animator.Pose;
        PoseScale.ScaleX = pose.ScaleX;
        PoseScale.ScaleY = pose.ScaleY;
        PoseRotate.Angle = pose.Rotation;
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