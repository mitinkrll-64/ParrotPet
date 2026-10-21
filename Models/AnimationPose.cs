namespace ParrotPet.Models;

/// <summary>Поза спрайта на текущий кадр (для персонажа, смотрящего вправо).</summary>
/// <param name="Rotation">Поворот в градусах (по часовой стрелке).</param>
/// <param name="ScaleX">Растяжение по горизонтали.</param>
/// <param name="ScaleY">Растяжение по вертикали.</param>
public readonly record struct AnimationPose(double Rotation, double ScaleX, double ScaleY)
{
    public static AnimationPose Neutral => new(0, 1, 1);
}