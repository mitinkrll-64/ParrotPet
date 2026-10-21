using System;
using ParrotPet.Models;

namespace ParrotPet.Services;

public enum FoodPhase
{
    None,
    /// <summary>Зерно падает сверху.</summary>
    Falling,
    /// <summary>Лежит на земле и ждёт попугая.</summary>
    Resting,
    /// <summary>Склёвано: разлетаются крошки.</summary>
    Crumbs
}

/// <summary>Одна крошка для отрисовки (координаты экрана, прозрачность).</summary>
public readonly record struct Crumb(double X, double Y, double Alpha);

/// <summary>Зерно: падение с отскоком, лежание на земле и крошки после клевания.</summary>
public class FoodService
{
    public const int CrumbCount = 3;

    private const double Gravity = 1800;
    private const double Bounce = 0.4;
    private const double MinBounceSpeed = 110;
    private const double CrumbLife = 0.8;
    private const double DropHeight = 240;

    private readonly Random _random = new();
    private readonly ScreenService _screens;
    private readonly SettingsModel _settings;

    private double _velocityY;
    private double _crumbTime;
    private readonly double[] _cx = new double[CrumbCount];
    private readonly double[] _cy = new double[CrumbCount];
    private readonly double[] _cvx = new double[CrumbCount];
    private readonly double[] _cvy = new double[CrumbCount];

    public FoodService(ScreenService screens, SettingsModel settings)
    {
        _screens = screens;
        _settings = settings;
    }

    public FoodPhase Phase { get; private set; } = FoodPhase.None;

    /// <summary>Центр зерна по X.</summary>
    public double X { get; private set; }

    /// <summary>Нижняя точка зерна (касается «земли»).</summary>
    public double Y { get; private set; }

    public bool IsVisible => Phase != FoodPhase.None;
    public bool IsLanded => Phase == FoodPhase.Resting;

    /// <summary>Бросает зерно на 50–100 px от центра попугая; сторону выбирает с учётом границ.</summary>
    public void Drop(double petCenterX)
    {
        if (Phase != FoodPhase.None) return;

        var bounds = _screens.GetBounds(_settings.SelectedScreen);
        double distance = 50 + _random.NextDouble() * 50;
        double sign = _random.Next(2) == 0 ? -1 : 1;

        double x = petCenterX + sign * distance;
        if (x < bounds.Left + 10 || x > bounds.Right - 10)
            x = petCenterX - sign * distance; // с этой стороны нет места — кидаем на другую
        X = Math.Clamp(x, bounds.Left + 10, Math.Max(bounds.Left + 10, bounds.Right - 10));

        double ground = _screens.GetGroundBottom(_settings.SelectedScreen, X);
        Y = Math.Max(bounds.Top + 10, ground - DropHeight);
        _velocityY = 0;
        Phase = FoodPhase.Falling;
    }

    /// <summary>Склёвано: зерно исчезает, летят крошки.</summary>
    public void Consume()
    {
        if (Phase != FoodPhase.Resting) return;
        double[] vx = { -45, 10, 48 };
        double[] vy = { -150, -190, -140 };
        for (int i = 0; i < CrumbCount; i++)
        {
            _cx[i] = X;
            _cy[i] = Y - 3;
            _cvx[i] = vx[i];
            _cvy[i] = vy[i];
        }
        _crumbTime = 0;
        Phase = FoodPhase.Crumbs;
    }

    public void Cancel() => Phase = FoodPhase.None;

    /// <summary>Настройки изменились: зерно за пределами новой области убираем.</summary>
    public void OnSettingsChanged()
    {
        if (Phase == FoodPhase.None) return;
        var b = _screens.GetBounds(_settings.SelectedScreen);
        if (X < b.Left || X > b.Right) Cancel();
    }

    public Crumb GetCrumb(int index)
    {
        double alpha = Phase == FoodPhase.Crumbs ? Math.Clamp(1 - _crumbTime / CrumbLife, 0, 1) : 0;
        return new Crumb(_cx[index], _cy[index], alpha);
    }

    public void Update(double dt)
    {
        switch (Phase)
        {
            case FoodPhase.Falling: UpdateFalling(dt); break;
            case FoodPhase.Crumbs: UpdateCrumbs(dt); break;
            case FoodPhase.Resting:
                // Панель задач могли сдвинуть — зерно остаётся на ней
                Y = _screens.GetGroundBottom(_settings.SelectedScreen, X);
                break;
        }
    }

    private void UpdateFalling(double dt)
    {
        _velocityY += Gravity * dt;
        Y += _velocityY * dt;

        double ground = _screens.GetGroundBottom(_settings.SelectedScreen, X);
        if (Y < ground) return;

        Y = ground;
        if (_velocityY > MinBounceSpeed)
        {
            _velocityY = -_velocityY * Bounce; // лёгкий отскок
        }
        else
        {
            _velocityY = 0;
            Phase = FoodPhase.Resting;
        }
    }

    private void UpdateCrumbs(double dt)
    {
        _crumbTime += dt;
        for (int i = 0; i < CrumbCount; i++)
        {
            _cvy[i] += Gravity * 0.5 * dt;
            _cx[i] += _cvx[i] * dt;
            _cy[i] = Math.Min(Y, _cy[i] + _cvy[i] * dt); // крошки не уходят под землю
        }
        if (_crumbTime >= CrumbLife) Phase = FoodPhase.None;
    }
}