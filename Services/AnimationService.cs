using System;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>
/// Процедурные микроанимации: покачивание при ходьбе, взмахи при полёте,
/// сжатие при приземлении и клевание. Только считает позу — рисует окно.
/// </summary>
public class AnimationService
{
    private const double SmoothRate = 14; // скорость сглаживания позы (1/с)

    private double _phase;
    private PetBehaviorState _lastState = PetBehaviorState.Idle;
    private double _stateTime;

    private double _landTime = 99;
    private double _landAmplitude;

    private double _rotation, _scaleX = 1, _scaleY = 1;

    /// <summary>Итоговая поза кадра с учётом пружины приземления.</summary>
    public AnimationPose Pose { get; private set; } = AnimationPose.Neutral;

    /// <summary>Приземление: impactSpeed — скорость удара о землю (px/с).</summary>
    public void TriggerLand(double impactSpeed)
    {
        _landAmplitude = Math.Min(1, impactSpeed / 1000);
        _landTime = 0;
    }

    public void Update(double dt, PetBehaviorState state, double speed)
    {
        if (state != _lastState)
        {
            _lastState = state;
            _stateTime = 0;
        }
        _stateTime += dt;
        _phase += dt;

        double targetRotation = 0, targetScaleX = 1, targetScaleY = 1;

        switch (state)
        {
            case PetBehaviorState.Walk:
                // Переваливание с боку на бок: -5°..+5°
                targetRotation = 5 * Math.Sin(_phase * 2 * Math.PI * 2.2 * speed);
                break;

            case PetBehaviorState.Fly:
                // Быстрый взмах: сжатие-растяжение по Y (по X — наоборот, чтобы объём сохранялся)
                double flap = Math.Sin(_phase * 2 * Math.PI * 6);
                targetScaleY = 1 + 0.06 * flap;
                targetScaleX = 1 - 0.04 * flap;
                break;

            case PetBehaviorState.Eat:
                // Три наклона вниз за время кормления
                double u = Math.Min(1, _stateTime / MovementService.EatDuration);
                targetRotation = 24 * (1 - Math.Cos(2 * Math.PI * 3 * u)) / 2;
                break;
        }

        // Плавный переход между позами без рывков
        double blend = 1 - Math.Exp(-SmoothRate * dt);
        _rotation += (targetRotation - _rotation) * blend;
        _scaleX += (targetScaleX - _scaleX) * blend;
        _scaleY += (targetScaleY - _scaleY) * blend;

        // Приземление накладывается поверх: затухающая пружина (squash & stretch)
        double sx = _scaleX, sy = _scaleY;
        if (_landTime < 1.5)
        {
            _landTime += dt;
            double spring = _landAmplitude * Math.Exp(-7 * _landTime) * Math.Cos(2 * Math.PI * 3.5 * _landTime);
            sy *= 1 - 0.28 * spring;
            sx *= 1 + 0.18 * spring;
        }

        Pose = new AnimationPose(_rotation, sx, sy);
    }
}