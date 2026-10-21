using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParrotPet.Models;
using ParrotPet.ViewModels;

namespace ParrotPet.Views;

/// <summary>Прозрачное окно-оверлей с попугаем: рендер кадров и перетаскивание мышью.</summary>
public partial class MainWindow : Window
{
    private static readonly Brush CereMale = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x86, 0xFF)));
    private static readonly Brush NostrilMale = Freeze(new SolidColorBrush(Color.FromRgb(0x1B, 0x4F, 0xB0)));
    private static readonly Brush CereFemale = Freeze(new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x7A)));
    private static readonly Brush NostrilFemale = Freeze(new SolidColorBrush(Color.FromRgb(0x7A, 0x5A, 0x3A)));

    private TimeSpan _lastRender;
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            if (ViewModel is not null) ViewModel.Profile.Changed -= ApplyGender;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel?.Start();
        if (ViewModel is not null) ViewModel.Profile.Changed += ApplyGender;
        ApplyGender();
        ApplyPosition();
        CompositionTarget.Rendering += OnRendering; // синхронно с частотой экрана
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    /// <summary>Цвет восковицы по полу: самец — синяя, самка — бежево-коричневая.</summary>
    private void ApplyGender()
    {
        if (ViewModel is null) return;
        bool male = ViewModel.Profile.Gender == PetGender.Male;
        Cere.Fill = male ? CereMale : CereFemale;
        Nostril.Fill = male ? NostrilMale : NostrilFemale;
    }

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