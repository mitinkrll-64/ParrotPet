using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParrotPet.Models;

namespace ParrotPet.Services;

/// <summary>Готовые слои пиксель-арт спрайта (по одному на анимируемый контейнер).</summary>
public record SpriteImages(BitmapSource Tail, BitmapSource FarFoot, BitmapSource Body, BitmapSource Foot,
    BitmapSource Wing, BitmapSource Head, BitmapSource Beak,
    BitmapSource FrontTail, BitmapSource FrontFootL, BitmapSource FrontFootR, BitmapSource FrontBody,
    BitmapSource FrontWingL, BitmapSource FrontWingR, BitmapSource FrontHead, BitmapSource FrontBeak);

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
    private const int FrontOffsetX = 18;   // фронтальный арт: ось симметрии x = 30 попадает в центр холста (клетка 48)

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
        var footLayer = BuildFoot(feet);
        var farFootLayer = new Layer();
        DrawLeg(farFootLayer, 20, Darken(feet, 0.82)); // дальняя лапка — за телом, чуть темнее
        var wingLayer = BuildWing(wing, wingStripes);
        var headLayer = BuildHead(head, body, stripes, beak, cheek, cheek2, cere, bodyLayer, wingLayer);
        var beakLayer = BuildBeak(beak);

        // Фронтальный вид (птица смотрит на пользователя)
        var frontBody = BuildFrontBody(body, shade, head);
        var frontHead = BuildFrontHead(head, body, stripes, beak, cheek, cheek2, cere, frontBody);

        return new SpriteImages(tailLayer.ToBitmap(), farFootLayer.ToBitmap(), bodyLayer.ToBitmap(), footLayer.ToBitmap(),
            wingLayer.ToBitmap(), headLayer.ToBitmap(), beakLayer.ToBitmap(),
            BuildFrontTail(tail1, tail2, tail3).ToBitmap(), BuildFrontFoot(26, feet).ToBitmap(),
            BuildFrontFoot(34, Darken(feet, 0.9)).ToBitmap(), frontBody.ToBitmap(),
            BuildFrontWing(wing, wingStripes, left: true).ToBitmap(), BuildFrontWing(wing, wingStripes, left: false).ToBitmap(),
            frontHead.ToBitmap(), BuildFrontBeak(beak).ToBitmap());
    }

    // ---- Хвост: длинный и широкий, продолжает линию спины и сужается к острому концу.
    // Два-три пера: светлое верхнее, средние, тёмное нижнее; между перьями тёмные бороздки.
    // Корень хвоста (центр вращения анимации) — арт-точка (36, 29).
    private static Layer BuildTail(Color t1, Color t2, Color t3)
    {
        var l = new Layer();
        l.Polygon(new (double, double)[]
        {
            (31, 24.5), (38, 26), (46, 28.8), (54, 32.3), (60, 35.2), (65, 37.8), (67.5, 39.2),
            (65.5, 40.8), (60, 39.8), (54, 38.2), (46, 36), (38, 33.8), (32, 31.8)
        }, (x, y) =>
        {
            // v — смещение от оси хвоста; отрицательное — верхняя кромка
            double v = y - (28.3 + (x - 33) * 0.34);
            if (x > 63.5) return Darken(t1, 0.85);                       // заострённый кончик
            if (x > 40 && v >= 0.55 && v < 1.15) return Darken(t2, 0.78); // бороздка между перьями
            if (x > 44 && v >= -2.6 && v < -2.0) return Darken(t3, 0.88);
            if (v < -2.0) return t3;
            if (v < 0.55) return t2;
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
            // Светлый блик на груди и редкие «перышки» по корпусу
            if (x < 9.5 && y > 18 && y < 25) c = Lighten(c, 0.18);
            int h = Hash((int)Math.Floor(x), (int)Math.Floor(y));
            if (x < 20 && y > 21 && h < 9) c = Darken(c, 0.93);
            else if (x < 20 && h > 94) c = Lighten(c, 0.12);
            return c;
        });
        l.Outline();

        return l;
    }

    // ---- Ближняя лапка отдельным слоем: поворачивается вокруг бедра (арт-точка 27.5, 33.5)
    private static Layer BuildFoot(Color feet)
    {
        var l = new Layer();
        DrawLeg(l, 26, feet);
        return l;
    }

    /// <summary>
    /// Лапка попугая (зигодактиль): пёстрая цевка с коленцем, два пальца вперёд и один-два назад
    /// с тёмными коготками. ax — арт-x цевки; пальцы касаются y = 41 (низ хитбокса).
    /// </summary>
    private static void DrawLeg(Layer l, int ax, Color feet)
    {
        var leg = Darken(feet, 0.94);
        var scale = Lighten(feet, 0.16);
        var toe = feet;
        var claw = Darken(feet, 0.5);

        // Цевка: чешуйки через строку, лёгкий наклон вперёд к голени
        int[] legX = { ax + 1, ax + 1, ax, ax, ax };
        for (int i = 0; i < 5; i++)
        {
            int y = 35 + i;
            var c = i % 2 == 0 ? leg : scale;
            l.SetArt(legX[i], y, c);
            l.SetArt(legX[i] + 1, y, Darken(c, 0.88));
        }

        // Пальцы: два вперёд (к голове), два назад; сустав чуть светлее, коготь — тёмная точка
        for (int x = ax - 4; x <= ax + 1; x++) l.SetArt(x, 40, x % 2 == 0 ? toe : scale);
        for (int x = ax - 3; x <= ax; x++) l.SetArt(x, 41, Darken(toe, 0.9));  // второй передний палец
        for (int x = ax + 2; x <= ax + 4; x++) l.SetArt(x, 40, toe);
        for (int x = ax + 2; x <= ax + 3; x++) l.SetArt(x, 41, Darken(toe, 0.9)); // второй задний палец
        l.SetArt(ax - 5, 41, claw);
        l.SetArt(ax - 4, 41, claw);
        l.SetArt(ax + 5, 41, claw);
    }

    // ---- Крыло по референсу: короткий пухлый «лист» с округлым плечом за затылком; кончик заострён и
    // лежит над основанием хвоста, под крылом виден бок птицы. У плеча — чешуйки кроющих перьев
    // (волнистые дуги), дальше — длинные маховые перья отдельными полосами; блик на плече,
    // светлая верхняя кромка, тёмный контур. Плечо (центр вращения анимации) — арт-точка (22, 12).
    private static Layer BuildWing(Color wing, Color stripes)
    {
        var l = new Layer();
        l.Polygon(new (double, double)[]
        {
            (17.5, 14), (17.8, 11.5), (19.5, 9.2), (22.5, 7.6), (26, 7.8), (30, 9.8), (35, 13), (40, 16.5),
            (45, 20), (49, 23.2), (52.2, 26.2), (50.2, 26.9), (51, 28.2), (47.5, 28), (44, 28.7), (39, 29.8), (35, 31),
            (30, 32), (25.5, 31.5), (21.5, 29), (19, 25), (17.8, 20)
        }, (x, y) =>
        {
            // Ось крыла от плеча (22,10) к кончику (51.8,25.8): along 0..1 вдоль, across — поперёк (плюс = вниз)
            double ox = x - 22, oy = y - 10;
            double along = (ox * 0.883 + oy * 0.469) / 33.4;
            double across = -ox * 0.469 + oy * 0.883;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);

            Color c = wing;
            double sx = x - 24, sy = y - 12.5;
            if (sx * sx + sy * sy < 18) c = Lighten(c, 0.22);               // блик на плече
            if (across < -4.2) c = Lighten(c, 0.12);                        // светлая верхняя кромка
            if (across > 5.5) c = Darken(c, 0.88);                          // нижняя кромка в тени

            if (along < 0.5)
            {
                // Кроющие перья: ряды волнистых дуг (чешуйки), ряды сдвинуты через один
                int row = iy / 3;
                int cx = ((ix + (row % 2) * 2) % 4 + 4) % 4;
                int ry = iy % 3;
                bool mark = (ry == 0 && cx == 0) || (ry == 1 && (cx == 1 || cx == 2)) || (ry == 2 && cx == 3);
                if (mark) c = Lerp(c, stripes, 0.6);
            }
            else
            {
                // Маховые перья: длинные полосы вдоль оси, к кончику темнее
                if (Math.Abs(across + 1.6) < 0.5 || Math.Abs(across - 1.8) < 0.5 || Math.Abs(across - 4.6) < 0.5)
                    c = Lerp(c, stripes, 0.55);
                if (along > 0.85) c = Lerp(c, stripes, 0.3);
            }
            return c;
        });
        l.Outline();
        return l;
    }

    // ---- Голова по референсу: круглая сверху, спереди почти вертикальная (плоское лицо), светлая,
    // с мягким серым контуром; крошечный клюв-«носик» с небольшой восковицей, глаз-овал 2x3 с бликом
    // (в XAML), у лица — маленькое щёчное пятно и три точки на горле. Затылок с мелкой рябью.
    // Нижняя половина клюва и глаз — отдельные детали в MainWindow.xaml (клюв открывается, глаз моргает).
    private static Layer BuildHead(Color head, Color body, Color stripes, Color beak, Color cheek, Color cheek2,
        Color cere, Layer bodyLayer, Layer wingLayer)
    {
        var l = new Layer();
        l.Polygon(new (double, double)[]
        {
            (9, 0.3), (15, 0.3), (19.5, 1.5), (22.5, 4), (24, 7.5), (23.5, 13), (20.5, 18.5), (16.5, 21.5),
            (11, 22), (7, 20.6), (5, 17.6), (4.2, 13.5), (4, 9.5), (4.8, 6), (6.5, 3.2), (9, 1.2)
        }, (x, y) =>
        {
            if (y >= 20) return ((int)x + (int)y) % 2 == 0 ? body : Lerp(head, body, 0.5);
            if (y >= 18) return Lerp(head, body, 0.18);
            if (y <= 3.5 && x < 17) return Lighten(head, 0.14);   // блик на макушке
            return head;
        });

        // Затылок: мелкая рябь у границы с крылом
        for (int y = 3; y <= 13; y += 2)
        for (int x = 16; x <= 22; x++)
            if (l.GetArt(x, y) is not null && (x + (y / 2) % 2 * 2) % 4 != 3)
                l.SetArt(x, y, Lerp(head, stripes, 0.5));

        // Щёчное пятно под глазом (4-5 пикселей по диагонали) и три точки на горле, как на референсе
        foreach (var (x, y) in new[] { (8, 12), (9, 12), (9, 13), (10, 13), (8, 13) }) l.SetArt(x, y, cheek);
        l.SetArt(9, 12, Lerp(cheek, cheek2, 0.5));
        foreach (int dx in new[] { 6, 9, 12 }) l.SetArt(dx, 16, cheek2);

        // Клюв: маленький «носик» с крючком, над ним небольшая восковица с ноздрёй
        var beakShade = Darken(beak, 0.85);
        foreach (var (x, y) in new[] { (1, 9), (2, 9), (3, 9), (4, 9), (0, 10), (1, 10), (2, 10), (3, 10), (4, 10),
                                       (0, 11), (1, 11), (2, 11), (3, 11), (1, 12), (2, 12) })
            l.SetArt(x, y, y >= 11 ? beakShade : beak);
        foreach (var (x, y) in new[] { (4, 8), (5, 8), (5, 9) }) l.SetArt(x, y, cere);
        l.SetArt(4, 8, Darken(cere, 0.6)); // ноздря

        // Контур не рисуем там, где под головой тело или крыло: так голова «врастает» в тело
        l.Outline(skip: (ix, iy) => bodyLayer.Has(ix, iy) || wingLayer.Has(ix, iy));
        return l;
    }
    // ---- Нижняя половина клюва: поворачивается вокруг шарнира (арт-точка 4.5, 12) при открывании
    private static Layer BuildBeak(Color beak)
    {
        var l = new Layer();
        var shade = Darken(beak, 0.78);
        foreach (var (x, y) in new[] { (3, 12), (4, 12), (2, 13), (3, 13), (4, 13), (3, 14) })
            l.SetArt(x, y, shade);
        l.SetArt(4, 12, Color.FromRgb(0xE0, 0x6A, 0x7A)); // язычок
        return l;
    }

    // ================= Фронтальный вид (птица сидит и смотрит на пользователя) =================
    // Арт-координаты фронтального вида не зеркалятся: ось симметрии x = 30, лапки касаются y = 41.
    // Холст 96x96 как у бокового вида, поэтому те же слои-картинки и тот же хитбокс:
    // x_хитбокса = x_арта + 2, y_хитбокса = y_арта + 22.

    // ---- Короткий хвост, выглядывающий из-под живота между лапками
    private static Layer BuildFrontTail(Color t1, Color t2, Color t3)
    {
        var l = new Layer(mirror: false);
        l.Polygon(new (double, double)[] { (27, 34), (33, 34), (34.5, 39.5), (31, 41.6), (29, 41.6), (25.5, 39.5) },
            (x, y) => y > 40 ? Darken(t1, 0.85) : Math.Abs(x - 30) < 1.3 ? t3 : Math.Abs(x - 30) < 3 ? t2 : t1);
        l.Outline();
        return l;
    }

    // ---- Тело спереди: широкая грудка, сужается к животу; «подложка» под головой
    private static Layer BuildFrontBody(Color body, Color shade, Color head)
    {
        var l = new Layer(mirror: false);
        l.Polygon(new (double, double)[]
        {
            (21, 19.5), (30, 18.8), (39, 19.5), (42.5, 23), (42, 28), (40, 33), (36, 36), (30, 37),
            (24, 36), (20, 33), (18, 28), (17.5, 23)
        }, (x, y) =>
        {
            double t = (y - 19) / 18;
            Color c = t < 0.3 ? body : t < 0.65 ? Lerp(body, shade, 0.5) : shade;
            if (Math.Abs(x - 30) < 3.5 && y > 21 && y < 29) c = Lighten(c, 0.12);   // светлая грудка
            int h = Hash((int)Math.Floor(x), (int)Math.Floor(y));
            if (h < 8 && y > 22) c = Darken(c, 0.93);
            else if (h > 94) c = Lighten(c, 0.12);
            return c;
        });
        l.Outline();
        // Подложка под головой, чтобы при наклоне головы на шее не было дыры
        l.Ellipse(30, 19, 8, 4, (_, _) => Lerp(head, body, 0.4), onlyEmpty: true);
        return l;
    }

    // ---- Крыло сбоку от тела (левое/правое — зеркально): листок вдоль тела, кончик внизу
    private static Layer BuildFrontWing(Color wing, Color stripes, bool left)
    {
        var l = new Layer(mirror: false);
        double Mx(double x) => left ? x : 60 - x;
        var pts = new (double, double)[]
        {
            (19.5, 20.2), (17.6, 23), (16, 28), (15.6, 34), (16.4, 38.5), (18.2, 39.8), (19.8, 37),
            (20.8, 31), (21.2, 25), (21, 21.5)
        };
        for (int i = 0; i < pts.Length; i++) pts[i] = (Mx(pts[i].Item1), pts[i].Item2);
        l.Polygon(pts, (x, y) =>
        {
            double along = (y - 20) / 20;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            Color c = wing;
            if (along < 0.2) c = Lighten(c, 0.2);                       // блик на плече
            if (along < 0.5)
            {
                int row = iy / 3, cx = ((ix + (row % 2) * 2) % 4 + 4) % 4, ry = iy % 3;
                bool mark = (ry == 0 && cx == 0) || (ry == 1 && (cx == 1 || cx == 2)) || (ry == 2 && cx == 3);
                if (mark) c = Lerp(c, stripes, 0.6);                    // чешуйки кроющих перьев
            }
            else if (Math.Abs(Mx(x) - 18.4) < 0.5) c = Lerp(c, stripes, 0.55); // линия маховых перьев
            if (along > 0.85) c = Lerp(c, stripes, 0.3);
            return c;
        });
        l.Outline();
        return l;
    }

    // ---- Лапка спереди: ножка и пальцы веером (два вперёд-в стороны, коготки)
    private static Layer BuildFrontFoot(int cx, Color feet)
    {
        var l = new Layer(mirror: false);
        var scale = Lighten(feet, 0.16);
        var claw = Darken(feet, 0.5);
        for (int y = 35; y <= 38; y++)
        {
            l.SetArt(cx, y, y % 2 == 0 ? feet : scale);
            l.SetArt(cx + 1, y, Darken(feet, 0.88));
        }
        for (int x = cx - 3; x <= cx + 4; x++) l.SetArt(x, 39, x % 2 == 0 ? feet : scale);
        foreach (int x in new[] { cx - 3, cx, cx + 4 }) l.SetArt(x, 40, feet);
        foreach (int x in new[] { cx - 3, cx + 4 }) l.SetArt(x, 41, claw);
        l.SetArt(cx, 41, Darken(feet, 0.9));
        return l;
    }

    // ---- Голова спереди: круглая, светлое лицо; щёчные пятна и точки на горле с двух сторон,
    // восковица по центру над маленьким клювом. Глаза и нижняя половина клюва — в MainWindow.xaml.
    private static Layer BuildFrontHead(Color head, Color body, Color stripes, Color beak, Color cheek, Color cheek2,
        Color cere, Layer bodyLayer)
    {
        var l = new Layer(mirror: false);
        l.Ellipse(30, 10.5, 11, 10.4, (x, y) =>
        {
            if (y >= 19.5) return ((int)x + (int)y) % 2 == 0 ? body : Lerp(head, body, 0.5);
            if (y >= 17.5) return Lerp(head, body, 0.18);
            if (y <= 3.5 && Math.Abs(x - 27) < 5) return Lighten(head, 0.14);   // блик на макушке
            return head;
        });

        // Мелкие чёрточки на лбу и макушке (рябь затылка видна по краям головы)
        for (int y = 2; y <= 6; y += 2)
        foreach (int x in new[] { 21, 22, 38, 39 })
            if (l.GetArt(x, y) is not null) l.SetArt(x, y, Lerp(head, stripes, 0.45));

        // Щёчные пятна под глазами и точки на горле: симметрично
        foreach (var (x, y) in new[] { (23, 12), (24, 12), (24, 13), (25, 13), (23, 13) })
        {
            l.SetArt(x, y, cheek);
            l.SetArt(60 - x - 1, y, cheek);
        }
        foreach (var (x, y) in new[] { (24, 16), (27, 17) })
        {
            l.SetArt(x, y, cheek2);
            l.SetArt(60 - x - 1, y, cheek2);
        }

        // Восковица между глазами с двумя ноздрями; сверху клюва маленький крючок
        foreach (var (x, y) in new[] { (29, 10), (30, 10), (29, 11), (30, 11) }) l.SetArt(x, y, cere);
        l.SetArt(29, 10, Darken(cere, 0.6));
        l.SetArt(30, 10, Darken(cere, 0.6));
        var beakShade = Darken(beak, 0.85);
        foreach (var (x, y) in new[] { (29, 12), (30, 12), (29, 13), (30, 13), (29, 14), (30, 14) })
            l.SetArt(x, y, y >= 13 ? beakShade : beak);

        // Контур не рисуем там, где под головой тело
        l.Outline(skip: (ix, iy) => bodyLayer.Has(ix, iy));
        return l;
    }

    // ---- Нижняя половина клюва спереди
    private static Layer BuildFrontBeak(Color beak)
    {
        var l = new Layer(mirror: false);
        var shade = Darken(beak, 0.78);
        foreach (var (x, y) in new[] { (29, 15), (30, 15), (29, 16), (30, 16) }) l.SetArt(x, y, shade);
        l.SetArt(29, 15, Color.FromRgb(0xE0, 0x6A, 0x7A)); // язычок
        l.SetArt(30, 15, Color.FromRgb(0xE0, 0x6A, 0x7A));
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
        private readonly bool _mirror;

        /// <param name="mirror">true — боковой вид (арт смотрит влево и зеркалится вправо); false — фронтальный (без зеркала).</param>
        public Layer(bool mirror = true) => _mirror = mirror;

        private int ImgX(int artX) => _mirror ? OffsetX + ArtWidth - 1 - artX : FrontOffsetX + artX;
        private static int ImgY(int artY) => OffsetY + artY;

        // Центр клетки картинки в арт-координатах
        private double ArtCenterX(int ix) => _mirror ? OffsetX + ArtWidth - (ix + 0.5) : ix + 0.5 - FrontOffsetX;
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

        public void Ellipse(double cx, double cy, double rx, double ry, Func<double, double, Color> color, bool onlyEmpty = false)
        {
            for (int iy = 0; iy < Size; iy++)
            for (int ix = 0; ix < Size; ix++)
            {
                double x = ArtCenterX(ix), y = ArtCenterY(iy);
                double dx = (x - cx) / rx, dy = (y - cy) / ry;
                if (dx * dx + dy * dy > 1 || (onlyEmpty && _cells[iy * Size + ix] is not null)) continue;
                _cells[iy * Size + ix] = color(x, y);
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