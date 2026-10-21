using System;

namespace ParrotPet.Models;

/// <summary>
/// Поза спрайта на текущий кадр (для персонажа, смотрящего вправо).
/// Углы — в градусах по часовой стрелке.
/// </summary>
public readonly record struct AnimationPose(
    double Rotation = 0,       // наклон всего тела (вокруг лап)
    double ScaleX = 1,         // сжатие/растяжение всего тела по X
    double ScaleY = 1,         // сжатие/растяжение всего тела по Y
    double HeadRotation = 0,   // поворот головы вокруг шеи
    double WingScaleY = 1,     // взмах крыла (масштаб по Y)
    double WingRotation = 0,   // качание крыла вокруг плеча
    double TailRotation = 0,   // качание хвоста
    double Breath = 1,         // дыхание (масштаб груди)
    double EyeOpen = 1,        // 1 — глаз открыт, 0 — закрыт
    double Heart = -1)         // прогресс сердечка 0..1, -1 — не показывать
{
    public static AnimationPose Neutral => new();

    /// <summary>Плавное смешивание поз; EyeOpen и Heart берутся из второй позы.</summary>
    public static AnimationPose Lerp(AnimationPose a, AnimationPose b, double t)
    {
        static double L(double x, double y, double k) => x + (y - x) * k;
        return new AnimationPose(
            L(a.Rotation, b.Rotation, t), L(a.ScaleX, b.ScaleX, t), L(a.ScaleY, b.ScaleY, t),
            L(a.HeadRotation, b.HeadRotation, t), L(a.WingScaleY, b.WingScaleY, t),
            L(a.WingRotation, b.WingRotation, t), L(a.TailRotation, b.TailRotation, t),
            L(a.Breath, b.Breath, t), b.EyeOpen, b.Heart);
    }
}