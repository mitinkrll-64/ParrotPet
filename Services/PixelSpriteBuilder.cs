using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>Готовые слои пиксель-арт спрайта (по одному на анимируемый контейнер).</summary>
public record SpriteImages(BitmapSource Tail, BitmapSource Body, BitmapSource Wing, BitmapSource Head);

/// <summary>
/// Рисует пиксель-арт попугая (вид сбоку, смотрит вправо) из палитры скина.
/// Сетка 48x48 клеток; клетка = 2 DIP на холсте 96x96. Каждая часть тела — отдельное
/// изображение, чтобы анимации вращали голову, крыло, хвост и тело независимо.
/// Тёмный контур вокруг частей строится автоматически из цвета заливки — как в референсе.
/// </summary>
public static class PixelSpriteBuilder
{
    public const int Size = 48;

    public static SpriteImages Build(PetSkin skin, PetGender gender)
    {
        var body = Parse(skin.ColorBody);
        var shade = Parse(skin.ColorBodyShade);
        var head = Parse(skin.ColorHead);
        var stripes = Parse(skin.ColorStripes);
        var wing = Parse(skin.ColorWing);
        var wingStripes = Parse(skin.ColorWingStripes);
        var beak = Parse(skin.ColorBeak);
        var cheek = Parse(skin.ColorCheeks);
        var cheek2 = Parse(skin.ColorCheeks2);
        var feet = Parse(skin.ColorFeet);

        // Восковица: самец — синяя, самка — бежево-коричневая
        var cere = gender == PetGender.Male ? Color.FromRgb(0x2E, 0x86, 0xFF) : Color.FromRgb(0xCF, 0xA8, 0x80);
        var nostril = gender == PetGender.Male ? Color.FromRgb(0x1B, 0x4F, 0xB0) : Color.FromRgb(0x7A, 0x5A, 0x3A);

        return new SpriteImages(
            BuildTail(Parse(skin.ColorTail1), Parse(skin.ColorTail2), Parse(skin.ColorTail3)),
            BuildBody(body, shade, feet),
            BuildWing(wing, wingStripes),
            BuildHead(head, stripes, beak, cheek, cheek2, cere, nostril));
    }

    // ---- Хвост: три пера, самое длинное сзади, короткие поверх (ступенчатый хвост)
    private static BitmapSource BuildTail(Color t1, Color t2, Color t3)
    {
        var l = new Layer();
        l.Capsule(16, 31, 2, 40, 3.8, 2.0, (_, _) => t1);
        l.Capsule(18, 30.5, 7, 37.5, 3.0, 1.8, (_, _) => t2);
        l.Capsule(19, 30, 11, 35.2, 2.4, 1.5, (_, _) => t3);
        l.Outline();
        return l.ToBitmap();
    }

    // ---- Тело с тремя полосами штриховки, лапками и «подложкой» под шеей
    private static BitmapSource BuildBody(Color body, Color shade, Color feet)
    {
        var l = new Layer();
        const double top = 20.5, bottom = 35.5;
        l.Ellipse(23, 28, 10, 7.5, (_, y) =>
        {
            double t = (y + 0.5 - top) / (bottom - top);
            return t < 0.33 ? body : t < 0.66 ? Lerp(body, shade, 0.5) : shade;
        });
        // Светлый блик на груди
        l.Set(30, 26, Lighten(body, 0.35));
        l.Set(31, 26, Lighten(body, 0.35));
        l.Set(31, 27, Lighten(body, 0.2));
        l.Outline();

        // Тонкие лапки с пальцами (без контура)
        var legColor = Darken(feet, 0.85);
        foreach (int x in new[] { 21, 26 })
        {
            for (int y = 36; y <= 38; y++) l.Set(x, y, feet);
            for (int dx = -1; dx <= 1; dx++) l.Set(x + dx, 39, legColor);
        }

        // Подложка под головой: чтобы при наклоне головы на шее не было дыры
        l.Ellipse(28, 22, 5.5, 4, (_, _) => body, onlyEmpty: true);
        return l.ToBitmap();
    }

    // ---- Крыло: вытянутая капля от плеча к кончику, волнистые полосы поперёк
    private static BitmapSource BuildWing(Color wing, Color stripes)
    {
        var l = new Layer();
        const double x0 = 25, y0 = 25, x1 = 14, y1 = 33.5;
        double ax = x1 - x0, ay = y1 - y0, len = Math.Sqrt(ax * ax + ay * ay);
        ax /= len; ay /= len;

        l.Capsule(x0, y0, x1, y1, 8.5, 1.8, (x, y) =>
        {
            double px = x + 0.5 - x0, py = y + 0.5 - y0;
            double along = px * ax + py * ay;                 // вдоль крыла
            double across = -px * ay + py * ax;               // поперёк
            double t = along / len;
            if (t > 0.86) return Darken(stripes, 0.9);        // маховые перья на кончике
            double wave = 0.9 * Math.Sin(across * 1.1);
            bool band = ((int)Math.Floor((along + wave) / 2.2) & 1) == 0;
            if (band && t > 0.12) return stripes;
            return t < 0.4 ? Lighten(wing, 0.1) : wing;
        });
        l.Outline();
        return l.ToBitmap();
    }

    // ---- Голова: полоски на затылке, щёки, горло, восковица и изогнутый клюв
    private static BitmapSource BuildHead(Color head, Color stripes, Color beak, Color cheek, Color cheek2,
        Color cere, Color nostril)
    {
        var l = new Layer();
        l.Ellipse(30, 17, 7, 6.5, (_, y) => y >= 21 ? Darken(head, 0.9) : y <= 12 ? Lighten(head, 0.12) : head);

        // Волнистые полоски на затылке
        for (int row = 0; row < 4; row++)
        {
            int y = 13 + 2 * row;
            int start = row % 2 == 0 ? 25 : 24;
            for (int x = start; x < start + 3; x++)
                if (l.Get(x, y) is not null) l.Set(x, y, stripes);
        }

        // Щёчное пятно и три точки на горле
        foreach (int x in new[] { 32, 33, 34 }) l.Set(x, 19, cheek);
        l.Set(33, 20, cheek2);
        l.Set(34, 20, cheek2);
        l.Set(36, 20, stripes);
        l.Set(37, 19, stripes);
        l.Set(35, 21, stripes);

        // Клюв: изогнутый вниз крючок
        var beakShade = Darken(beak, 0.85);
        foreach (var (x, y) in new[] { (38, 16), (39, 16), (40, 16), (38, 17), (39, 17), (40, 17), (41, 17),
                                       (39, 18), (40, 18), (41, 18), (40, 19), (41, 19) })
            l.Set(x, y, y >= 18 ? beakShade : beak);
        l.Set(41, 20, beakShade);

        // Восковица над клювом
        foreach (var (x, y) in new[] { (37, 14), (38, 14), (39, 14), (37, 15), (38, 15), (39, 15) })
            l.Set(x, y, cere);
        l.Set(38, 14, nostril);

        l.Outline();
        return l.ToBitmap();
    }

    // ---- Вспомогательные функции цвета

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static Color Darken(Color c, double factor) => Color.FromRgb(
        (byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));

    private static Color Lighten(Color c, double amount) => Lerp(c, Colors.White, amount);

    /// <summary>Один слой: сетка клеток с растеризацией фигур, автоконтуром и выводом в картинку.</summary>
    private sealed class Layer
    {
        private readonly Color?[] _cells = new Color?[Size * Size];

        public Color? Get(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size ? _cells[y * Size + x] : null;

        public void Set(int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < Size && y < Size) _cells[y * Size + x] = color;
        }

        public void Ellipse(double cx, double cy, double rx, double ry, Func<int, int, Color> color, bool onlyEmpty = false)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                if (dx * dx + dy * dy > 1) continue;
                if (onlyEmpty && Get(x, y) is not null) continue;
                Set(x, y, color(x, y));
            }
        }

        /// <summary>Конусный отрезок со скруглёнными концами (ширина w0 у начала, w1 у конца).</summary>
        public void Capsule(double x0, double y0, double x1, double y1, double w0, double w1, Func<int, int, Color> color)
        {
            double vx = x1 - x0, vy = y1 - y0, len2 = vx * vx + vy * vy;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                double px = x + 0.5 - x0, py = y + 0.5 - y0;
                double t = Math.Clamp((px * vx + py * vy) / len2, 0, 1);
                double dx = px - t * vx, dy = py - t * vy;
                double radius = (w0 + (w1 - w0) * t) / 2;
                if (dx * dx + dy * dy <= radius * radius) Set(x, y, color(x, y));
            }
        }

        /// <summary>Контур в одну клетку вокруг заливки: тёмная версия цвета соседней клетки.</summary>
        public void Outline(double factor = 0.58)
        {
            var add = new List<(int x, int y, Color c)>();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (Get(x, y) is not null) continue;
                Color? n = Get(x + 1, y) ?? Get(x - 1, y) ?? Get(x, y + 1) ?? Get(x, y - 1);
                if (n is { } c) add.Add((x, y, Darken(c, factor)));
            }
            foreach (var (x, y, c) in add) Set(x, y, c);
        }

        public BitmapSource ToBitmap()
        {
            var pixels = new byte[Size * Size * 4];
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] is not { } c) continue;
                pixels[i * 4] = c.B;
                pixels[i * 4 + 1] = c.G;
                pixels[i * 4 + 2] = c.R;
                pixels[i * 4 + 3] = 255;
            }
            var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, pixels, Size * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}