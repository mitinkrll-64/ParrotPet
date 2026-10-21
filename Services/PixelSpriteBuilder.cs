using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>Готовые слои пиксель-арт спрайта (по одному на анимируемый контейнер).</summary>
public record SpriteImages(BitmapSource Tail, BitmapSource Body, BitmapSource Wing, BitmapSource Head);

/// <summary>
/// Рисует пиксель-арт попугая в стиле референса (Assets/Skins/skins.jpg): вытянутое тело без шеи,
/// крупная круглая голова, лист-крыло на спине, длинный хвост, тонкие лапки. Силуэты заданы
/// многоугольниками в «арт-координатах» (62x42 пикселя, птица смотрит влево, как на референсе)
/// и при растеризации зеркалятся вправо. 1 пиксель арта = 1 DIP; слои — картинки 96x96.
/// Контур строится автоматически тёмным оттенком заливки, как у птиц на референсе.
/// </summary>
public static class PixelSpriteBuilder
{
    public const int Size = 96;

    // Размер арта и его положение на холсте 96x96 (хитбокс 64x64 лежит в клетках 16..79):
    // лапки касаются низа хитбокса, птица по ширине занимает хитбокс.
    private const int ArtWidth = 62;
    private const int OffsetX = 17;
    private const int OffsetY = 38;

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
        var tail1 = Parse(skin.ColorTail1);
        var tail2 = Parse(skin.ColorTail2);
        var tail3 = Parse(skin.ColorTail3);

        // Восковица: самец — синяя, самка — бежево-коричневая
        var cere = gender == PetGender.Male ? Color.FromRgb(0x2E, 0x86, 0xFF) : Color.FromRgb(0xCF, 0xA8, 0x80);

        var tailLayer = BuildTail(tail1, tail2, tail3);
        var bodyLayer = BuildBody(body, shade, feet);
        var wingLayer = BuildWing(wing, wingStripes);
        var headLayer = BuildHead(head, body, stripes, beak, cheek, cheek2, cere, bodyLayer, wingLayer);

        return new SpriteImages(tailLayer.ToBitmap(), bodyLayer.ToBitmap(), wingLayer.ToBitmap(), headLayer.ToBitmap());
    }

    // ---- Хвост: длинный, сужается к концу; светлая кромка сверху, тёмный низ
    private static Layer BuildTail(Color t1, Color t2, Color t3)
    {
        var l = new Layer();
        l.Polygon(new (double, double)[]
        {
            (33, 28), (38, 26), (44, 28), (51, 30.8), (58, 32.2), (59.3, 33.8),
            (58, 35.8), (54, 37.2), (50, 37.6), (45, 35.2), (40, 33), (34, 32)
        }, (x, y) =>
        {
            // Расстояние от оси хвоста (36,29)->(58,34.5): отрицательное — верхняя кромка
            double v = (y - 29) - (x - 36) * 0.25;
            double u = (x - 36) / 22;
            if (u > 0.92) return Darken(t1, 0.9);
            if (v < -1.6) return t3;
            if (v < 0.7) return t2;
            return t1;
        });
        l.Outline();
        return l;
    }

    // ---- Тело: вытянутая «капля» с грудкой, штриховка полосами, лапки с пальцами
    private static Layer BuildBody(Color body, Color shade, Color feet)
    {
        var l = new Layer();
        l.Polygon(new (double, double)[]
        {
            (4.5, 17), (5, 20), (6.5, 24), (8.5, 27.5), (11.5, 30.5), (15, 32.5), (19, 33.8),
            (26, 33.8), (30, 32.8), (36, 31), (37, 28), (46, 24), (42, 20.5), (37, 17.5),
            (31, 14.5), (25, 11), (20, 11.5), (14, 15)
        }, (x, y) =>
        {
            double t = (y - 17) / (33.8 - 17);
            Color c = t < 0.35 ? body : t < 0.7 ? Lerp(body, shade, 0.5) : shade;
            // Светлый блик на груди
            if (x < 9.5 && y > 18 && y < 25) c = Lighten(c, 0.18);
            return c;
        });
        l.Outline();

        // Тонкие лапки с тремя пальцами (без контура), как на референсе
        var leg = Darken(feet, 0.9);
        var toe = Lighten(feet, 0.15);
        foreach (int ax in new[] { 17, 27 })
        {
            for (int y = 34; y <= 37; y++) l.SetArt(ax, y, leg);
            l.SetArt(ax, 38, leg);
            for (int x = ax - 5; x <= ax + 2; x++) l.SetArt(x, 39, toe);
            foreach (int x in new[] { ax - 5, ax - 2, ax + 1 }) l.SetArt(x, 40, toe);
            foreach (int x in new[] { ax - 5, ax - 2 }) l.SetArt(x, 41, toe);
        }
        return l;
    }

    // ---- Крыло: лист от затылка к основанию хвоста, с рябью из перьев
    private static Layer BuildWing(Color wing, Color stripes)
    {
        var l = new Layer();
        l.Polygon(new (double, double)[]
        {
            (18, 17), (18.3, 13), (20.5, 10), (24, 8.2), (28.5, 9.8), (34, 13.2), (39, 16.5),
            (44.5, 20.5), (50, 24.8), (51, 26.5), (47, 27.2), (42, 28), (38, 30), (34, 31.2),
            (29, 32), (25, 32.2), (21.5, 30), (19.5, 26), (18.3, 21.5)
        }, (x, y) =>
        {
            double dx = x - 22, dy = y - 12.5, d = Math.Sqrt(dx * dx + dy * dy);
            int h = Hash((int)Math.Floor(x), (int)Math.Floor(y));
            bool line = (((int)Math.Floor(x - y) % 6) + 6) % 6 == 0;

            Color c = d < 7 ? Lighten(wing, 0.18) : wing;
            if (d > 5 && line) c = Lerp(c, stripes, 0.5);
            else if (d > 5 && h < 10) c = Lerp(c, stripes, 0.4);
            else if (h > 90) c = Lighten(c, 0.22);
            if (x > 44) c = Lerp(c, stripes, 0.3);        // кончик крыла темнее
            if (y > 29.5) c = Darken(c, 0.9);              // нижняя кромка
            return c;
        });
        l.Outline();
        return l;
    }

    // ---- Голова: круглая, светлая; переход к груди «шахматкой», крошечный клюв и восковица
    private static Layer BuildHead(Color head, Color body, Color stripes, Color beak, Color cheek, Color cheek2,
        Color cere, Layer bodyLayer, Layer wingLayer)
    {
        var l = new Layer();
        l.Ellipse(13, 10.5, 9.5, 10.5, (x, y) =>
        {
            if (y >= 20) return ((int)x + (int)y) % 2 == 0 ? body : Lerp(head, body, 0.5);
            if (y >= 18) return Lerp(head, body, 0.18);
            if (y <= 2) return Lighten(head, 0.08);
            return head;
        });

        // Рябь на затылке у края крыла
        for (int y = 3; y <= 12; y++)
        for (int x = 17; x <= 21; x++)
            if (l.GetArt(x, y) is not null && (x * 3 + y * 5) % 7 < 2)
                l.SetArt(x, y, Lerp(head, stripes, 0.55));

        // Щёчное пятно под глазом и три точки на горле
        foreach (var (x, y) in new[] { (9, 12), (10, 12), (10, 13), (11, 13) }) l.SetArt(x, y, cheek);
        foreach (int x in new[] { 6, 9, 12 }) l.SetArt(x, 16, cheek2);

        // Крошечный клюв крючком и восковица над ним
        var beakShade = Darken(beak, 0.82);
        foreach (var (x, y) in new[] { (1, 9), (2, 9), (3, 9), (0, 10), (1, 10), (2, 10), (3, 10),
                                       (0, 11), (1, 11), (2, 11), (3, 11), (1, 12), (2, 12), (3, 12), (2, 13) })
            l.SetArt(x, y, y >= 11 ? beakShade : beak);
        foreach (var (x, y) in new[] { (2, 7), (3, 7), (2, 8), (3, 8) }) l.SetArt(x, y, cere);

        // Контур не рисуем там, где под головой тело или крыло: так голова «врастает» в тело
        l.Outline(skip: (ix, iy) => bodyLayer.Has(ix, iy) || wingLayer.Has(ix, iy));
        return l;
    }

    // ---- Вспомогательные функции

    private static int Hash(int x, int y) => Math.Abs((x * 73856093) ^ (y * 19349663)) % 100;

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Clamp(a.R + (b.R - a.R) * t, 0, 255),
        (byte)Math.Clamp(a.G + (b.G - a.G) * t, 0, 255),
        (byte)Math.Clamp(a.B + (b.B - a.B) * t, 0, 255));

    private static Color Darken(Color c, double factor) => Color.FromRgb(
        (byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));

    private static Color Lighten(Color c, double amount) => Lerp(c, Colors.White, amount);

    /// <summary>
    /// Слой 96x96. Фигуры задаются в арт-координатах (птица смотрит влево) и зеркалятся вправо:
    /// x_картинки = OffsetX + ArtWidth - x_арта, y_картинки = OffsetY + y_арта.
    /// </summary>
    private sealed class Layer
    {
        private readonly Color?[] _cells = new Color?[Size * Size];

        private static int ImgX(int artX) => OffsetX + ArtWidth - 1 - artX;
        private static int ImgY(int artY) => OffsetY + artY;

        // Центр клетки картинки в арт-координатах
        private static double ArtCenterX(int ix) => OffsetX + ArtWidth - (ix + 0.5);
        private static double ArtCenterY(int iy) => iy + 0.5 - OffsetY;

        public bool Has(int ix, int iy) => ix >= 0 && iy >= 0 && ix < Size && iy < Size && _cells[iy * Size + ix] is not null;

        public Color? GetArt(int ax, int ay)
        {
            int ix = ImgX(ax), iy = ImgY(ay);
            return ix >= 0 && iy >= 0 && ix < Size && iy < Size ? _cells[iy * Size + ix] : null;
        }

        public void SetArt(int ax, int ay, Color color)
        {
            int ix = ImgX(ax), iy = ImgY(ay);
            if (ix >= 0 && iy >= 0 && ix < Size && iy < Size) _cells[iy * Size + ix] = color;
        }

        public void Ellipse(double cx, double cy, double rx, double ry, Func<double, double, Color> color)
        {
            for (int iy = 0; iy < Size; iy++)
            for (int ix = 0; ix < Size; ix++)
            {
                double x = ArtCenterX(ix), y = ArtCenterY(iy);
                double dx = (x - cx) / rx, dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1) _cells[iy * Size + ix] = color(x, y);
            }
        }

        /// <summary>Заливка многоугольника (чётно-нечётное правило по центрам клеток).</summary>
        public void Polygon(IReadOnlyList<(double x, double y)> points, Func<double, double, Color> color)
        {
            for (int iy = 0; iy < Size; iy++)
            for (int ix = 0; ix < Size; ix++)
            {
                double x = ArtCenterX(ix), y = ArtCenterY(iy);
                if (Inside(points, x, y)) _cells[iy * Size + ix] = color(x, y);
            }
        }

        private static bool Inside(IReadOnlyList<(double x, double y)> p, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) &&
                    x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Контур в одну клетку: тёмный оттенок цвета соседней клетки заливки.</summary>
        public void Outline(double factor = 0.6, Func<int, int, bool>? skip = null)
        {
            var add = new List<(int x, int y, Color c)>();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (_cells[y * Size + x] is not null || (skip?.Invoke(x, y) ?? false)) continue;
                Color? n = Cell(x + 1, y) ?? Cell(x - 1, y) ?? Cell(x, y + 1) ?? Cell(x, y - 1);
                if (n is { } c) add.Add((x, y, Darken(c, factor)));
            }
            foreach (var (x, y, c) in add) _cells[y * Size + x] = c;
        }

        private Color? Cell(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size ? _cells[y * Size + x] : null;

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