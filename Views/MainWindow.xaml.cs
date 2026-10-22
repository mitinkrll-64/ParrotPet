using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParrotPet.Models;
using ParrotPet.Services;
using ParrotPet.ViewModels;

namespace ParrotPet.Views;

/// <summary>Прозрачное окно-оверлей с попугаем: рендер кадров и перетаскивание мышью.</summary>
public partial class MainWindow : Window
{
    private TimeSpan _lastRender;

    // Разворот: какой вид показан сейчас и ход перехода (сжатие, смена спрайта, расширение)
    private PetView _shownView = PetView.Side;
    private double _shownFacing = 1;
    private double _turnTime = -1, _turnDt;
    private bool _turnSwapped;
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

        _turnDt = dt;
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
        double squeeze = UpdateTurn(m);
        bool front = _shownView == PetView.Front;
        SideRig.Visibility = front ? Visibility.Collapsed : Visibility.Visible;
        FrontRig.Visibility = front ? Visibility.Visible : Visibility.Collapsed;

        PoseOffset.X = pose.OffsetX;
        PoseOffset.Y = pose.OffsetY;
        PoseScale.ScaleX = pose.ScaleX;
        PoseScale.ScaleY = pose.ScaleY;
        // Спереди наклон «вперёд» превратился бы в крен вбок, поэтому гасим его (кроме покачивания при ходьбе)
        PoseRotate.Angle = front ? pose.Rotation * (m.State == PetBehaviorState.Fly ? 0.25 : 0.8) : pose.Rotation;
        // Разворот: сжатие по X до 45%, смена спрайта, расширение. Фронтальный вид не зеркалится
        ParrotFlip.ScaleX = (front ? 1 : _shownFacing) * squeeze;

        if (front) ApplyFront(pose, m);
        else ApplySide(pose);
        ApplyHeart(pose.Heart);
    }

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// Плавный разворот: старый вид сжимается по горизонтали до 45%, затем показывается новый
    /// (сбоку в другую сторону или спереди) и расширяется до 100%. Возвращает текущее сжатие.
    /// </summary>
    private double UpdateTurn(MovementService m)
    {
        bool changed = m.View != _shownView || (m.View == PetView.Side && m.FacingScale != _shownFacing);
        if (_turnTime < 0 && changed)
        {
            _turnTime = 0;
            _turnSwapped = false;
        }
        if (_turnTime < 0) return 1;

        const double half = 0.13;
        _turnTime += _turnDt;
        _turnDt = 0; // время кадра учитываем один раз, даже если ApplyPosition вызван дважды
        if (_turnTime < half) return 1 - 0.55 * Smooth(_turnTime / half);

        if (!_turnSwapped)
        {
            _shownView = m.View;
            _shownFacing = m.FacingScale;
            _turnSwapped = true;
        }
        double u = (_turnTime - half) / half;
        if (u >= 1)
        {
            _turnTime = -1;
            return 1;
        }
        return 0.45 + 0.55 * Smooth(u);
    }

    /// <summary>Шея: тянется от основания (bx, by) к сдвинутой голове; пока голова на месте — невидима.</summary>
    private static void UpdateNeck(System.Windows.Shapes.Line fill, double bx, double by, double dx, double dy)
    {
        double len = Math.Sqrt(dx * dx + dy * dy);
        fill.Opacity = Math.Clamp((len - 1.5) / 4, 0, 1);
        if (fill.Opacity <= 0) return;
        // Шея не доходит до центра головы: голова закрывает её конец, и «колпачок» не выглядывает из-за контура
        fill.X2 = bx + dx * 0.7;
        fill.Y2 = by + dy * 0.7;
    }

    /// <summary>Поза в трансформации частей бокового вида.</summary>
    private void ApplySide(AnimationPose pose)
    {
        HeadRot.Angle = pose.HeadRotation;
        HeadMove.X = pose.HeadOffsetX;
        HeadMove.Y = pose.HeadOffsetY;
        UpdateNeck(NeckFill, 48, 41, pose.HeadOffsetX, pose.HeadOffsetY);
        WingScale.ScaleY = pose.WingScaleY;
        WingRot.Angle = pose.WingRotation;
        TailRot.Angle = pose.TailRotation;
        FootRot.Angle = pose.FootRotation;
        double nearTuck = Math.Max(pose.FootTuck, pose.LegsTuck);  // подобранная лапка прячется в перья
        FootTuckScale.ScaleY = 1 - 0.85 * nearTuck;
        FootContainer.Opacity = 1 - nearTuck;
        FarFootRot.Angle = pose.FarFootRotation;
        FarFootTuckScale.ScaleY = 1 - 0.85 * pose.LegsTuck;
        FarFootContainer.Opacity = 1 - pose.LegsTuck;
        BeakRot.Angle = pose.BeakOpen;
        BreathScale.ScaleY = pose.Breath;
        BreathScale.ScaleX = 1 + (pose.Breath - 1) * 0.5;
        EyeScale.ScaleY = Math.Max(0.05, pose.EyeOpen);
    }

    /// <summary>
    /// Та же поза анимации в трансформации частей фронтального вида: поклон — голова опускается и
    /// сплющивается, сдвиг головы назад — крен вбок к крылу, взмахи — крылья расходятся по бокам,
    /// шаги — лапки поочерёдно приподнимаются. Так работают все анимации без отдельного кода под каждую.
    /// </summary>
    private void ApplyFront(AnimationPose pose, MovementService m)
    {
        bool air = m.State is PetBehaviorState.Fly or PetBehaviorState.Fall;
        bool idle = m.State == PetBehaviorState.Idle;

        FBreathScale.ScaleY = pose.Breath;
        FBreathScale.ScaleX = 1 + (pose.Breath - 1) * 0.5;

        // Голова
        double nod = Math.Clamp(pose.HeadRotation, -40, 130);
        FHeadScale.ScaleY = 1 - 0.10 * Math.Max(0, nod) / 130;
        FHeadRot.Angle = Math.Clamp(pose.HeadOffsetX * 1.8, -32, 32);
        FHeadMove.X = pose.HeadOffsetX * 0.45;
        FHeadMove.Y = nod / 130 * 9 + pose.HeadOffsetY * 0.5;
        UpdateNeck(FNeckFill, 32, 41, FHeadMove.X, FHeadMove.Y);

        // Крылья: наружу «+» у левого, «−» у правого; в покое тянется в основном одно (потягивание)
        double spread = air ? 30 : 0;
        FWingLRot.Angle = pose.WingRotation * 0.9 + spread;
        FWingRRot.Angle = -((idle ? pose.WingRotation * 0.3 : pose.WingRotation * 0.9) + spread);
        FWingLScale.ScaleY = pose.WingScaleY;
        FWingRScale.ScaleY = idle ? 1 + (pose.WingScaleY - 1) * 0.3 : pose.WingScaleY;
        FTailRot.Angle = pose.TailRotation;

        // Лапки: шаг вперёд (отрицательный поворот) поднимает лапку; подобранные прячутся в перья
        FFootLMove.Y = -Math.Max(0, -pose.FootRotation) * 0.3;
        FFootRMove.Y = -Math.Max(0, -pose.FarFootRotation) * 0.3;
        double tuckL = Math.Max(pose.FootTuck, pose.LegsTuck);
        FFootLScale.ScaleY = 1 - 0.85 * tuckL;
        FFootLContainer.Opacity = 1 - tuckL;
        FFootRScale.ScaleY = 1 - 0.85 * pose.LegsTuck;
        FFootRContainer.Opacity = 1 - pose.LegsTuck;

        FBeakMove.Y = pose.BeakOpen / 34.0 * 4;
        double eye = Math.Max(0.05, pose.EyeOpen);
        FEyeLScale.ScaleY = eye;
        FEyeRScale.ScaleY = eye;
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