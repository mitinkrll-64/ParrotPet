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

    /// <summary>Правый клик по попугаю: карточка с именем, полом, возрастом и сытостью.</summary>
    private void PetMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var p = ViewModel.Profile;
        MenuName.Text = p.Name;
        MenuGender.Text = p.Gender == Models.PetGender.Male ? "Пол: самец ♂" : "Пол: самка ♀";
        MenuAge.Text = $"Возраст: {p.AgeDays} {DaysWord(p.AgeDays)}";
        MenuHunger.Value = p.Hunger;
        MenuHungerText.Text = $"{p.Hunger:0} / 100";
    }

    private static string DaysWord(int n)
    {
        int last = n % 100 is >= 11 and <= 14 ? 0 : n % 10;
        return last == 1 ? "день" : last is >= 2 and <= 4 ? "дня" : "дней";
    }

    private void MenuFeed_Click(object sender, RoutedEventArgs e) => ViewModel?.Feed();

    private void MenuSettings_Click(object sender, RoutedEventArgs e) => ViewModel?.RequestSettings();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12) ViewModel?.ToggleDebug();
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
        double size = m.WindowSize;
        if (Math.Abs(Width - size) > 0.01)
        {
            Width = size;
            Height = size;
            ContentScale.ScaleX = ViewModel.Scale;
            ContentScale.ScaleY = ViewModel.Scale;
        }

        // Окно больше хитбокса на поле Pad, поэтому сдвигаем его на это поле
        Left = m.X - m.Pad;
        Top = m.Y - m.Pad;

        var pose = ViewModel.Animator.Pose;
        PoseScale.ScaleX = pose.ScaleX;
        PoseScale.ScaleY = pose.ScaleY;
        PoseRotate.Angle = pose.Rotation;
        ParrotFlip.ScaleX = m.FacingScale;

        HeadRot.Angle = pose.HeadRotation;
        WingScale.ScaleY = pose.WingScaleY;
        WingRot.Angle = pose.WingRotation;
        TailRot.Angle = pose.TailRotation;
        BreathScale.ScaleY = pose.Breath;
        BreathScale.ScaleX = 1 + (pose.Breath - 1) * 0.5;
        EyeScale.ScaleY = Math.Max(0.05, pose.EyeOpen);

        ApplyHeart(pose.Heart);
    }

    /// <summary>Сердечко: появляется, всплывает вверх и гаснет.</summary>
    private void ApplyHeart(double t)
    {
        if (t < 0)
        {
            HeartPath.Opacity = 0;
            return;
        }
        HeartPath.Opacity = t < 0.15 ? t / 0.15 : 1 - (t - 0.15) / 0.85;
        HeartMove.Y = -14 * t;
        double pop = 0.6 + 0.4 * Math.Min(1, t / 0.2);
        HeartScale.ScaleX = pop;
        HeartScale.ScaleY = pop;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null) return;
        // Точка захвата считается от угла хитбокса, а не от угла окна с полями
        var grab = e.GetPosition(this);
        double pad = ViewModel.Movement.Pad;
        ViewModel.Movement.BeginDrag(grab.X - pad, grab.Y - pad);
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