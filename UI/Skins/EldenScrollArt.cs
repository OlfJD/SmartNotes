using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartNotes.Core.Models;
using WpfPen = System.Windows.Media.Pen;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Elden Ring note body: an aged parchment scroll with torn side edges, rolled rods with
/// gilded finials top and bottom, an inked frame and a wax seal in the note's colour.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Elden Ring skin.
/// </summary>
public static class EldenScrollArt
{
    private const double RodTop = 2;
    private const double RodHeight = 20;
    private const double RollInset = 14;   // rolled paper starts here (finials sit outside)
    private const double PaperInset = 18;  // flat sheet edge
    private const double EdgeStep = 7;

    private static readonly Lazy<ImageBrush> GrainBrush = new(CreateGrainBrush);

    public static Drawing Build(double w, double h, NoteColorTheme theme, string tag)
    {
        WpfColor paper = theme.BgBrush.Color;
        WpfColor ink = theme.BorderBrush.Color;
        WpfColor deep = theme.GlowBrush.Color;
        WpfColor wax = theme.PrimaryBrush.Color;
        WpfColor burn = WpfColor.FromRgb(0x5E, 0x3C, 0x14);

        int seed = 17;
        foreach (char c in tag) seed = unchecked(seed * 31 + c);

        double paperL = PaperInset, paperR = w - PaperInset;
        double paperTop = RodTop + RodHeight - 5;
        double paperBottom = h - RodTop - RodHeight + 5;

        // Sheet with torn, uneven side edges (stable while resizing: jitter depends on row index only)
        var sheet = new StreamGeometry();
        using (var g = sheet.Open())
        {
            g.BeginFigure(new WpfPoint(paperL, paperTop), true, true);
            g.LineTo(new WpfPoint(paperR, paperTop), true, false);
            int rows = (int)((paperBottom - paperTop) / EdgeStep);
            for (int i = 1; i < rows; i++)
            {
                g.LineTo(new WpfPoint(paperR - Tear(i, seed), paperTop + i * EdgeStep), true, false);
            }
            g.LineTo(new WpfPoint(paperR, paperBottom), true, false);
            g.LineTo(new WpfPoint(paperL, paperBottom), true, false);
            for (int i = rows - 1; i >= 1; i--)
            {
                g.LineTo(new WpfPoint(paperL + Tear(i, seed + 101), paperTop + i * EdgeStep), true, false);
            }
        }
        sheet.Freeze();

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Sheet shadow
            dc.PushTransform(new TranslateTransform(1.5, 2));
            dc.DrawGeometry(Solid(Colors.Black, 0.22), null, sheet);
            dc.Pop();

            // Parchment base
            var baseFill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 0) };
            baseFill.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.30), 0));
            baseFill.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.04), 0.14));
            baseFill.GradientStops.Add(new GradientStop(Blend(paper, Colors.White, 0.10), 0.5));
            baseFill.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.06), 0.86));
            baseFill.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.32), 1));
            dc.DrawGeometry(baseFill, null, sheet);

            dc.PushClip(sheet);

            // Rough fibre / blotch grain
            dc.DrawRectangle(GrainBrush.Value, null, new Rect(0, 0, w, h));

            // Age stains
            for (int i = 0; i < 5; i++)
            {
                double sx = paperL + Hash(i, seed + 7) * (paperR - paperL);
                double sy = paperTop + Hash(i, seed + 13) * (paperBottom - paperTop);
                double sr = 26 + Hash(i, seed + 23) * 54;
                var stain = new RadialGradientBrush();
                stain.GradientStops.Add(new GradientStop(WithAlpha(burn, 0.13), 0));
                stain.GradientStops.Add(new GradientStop(WithAlpha(burn, 0.07), 0.6));
                stain.GradientStops.Add(new GradientStop(WithAlpha(burn, 0), 1));
                dc.DrawEllipse(stain, null, new WpfPoint(sx, sy), sr, sr * (0.6 + Hash(i, seed + 31) * 0.5));
            }

            // Old fold creases: a dark valley with a light ridge beneath it
            for (int i = 0; i < 3; i++)
            {
                double cyFold = paperTop + (0.22 + 0.27 * i + Hash(i, seed + 41) * 0.12) * (paperBottom - paperTop);
                var crease = new StreamGeometry();
                using (var g = crease.Open())
                {
                    g.BeginFigure(new WpfPoint(paperL, cyFold), false, false);
                    for (double x = paperL + 22; x <= paperR; x += 22)
                    {
                        g.LineTo(new WpfPoint(x, cyFold + (Hash((int)x, seed + 60 + i) - 0.5) * 3.2), true, true);
                    }
                }
                crease.Freeze();
                dc.DrawGeometry(null, new WpfPen(Solid(burn, 0.16), 1.1), crease);
                dc.PushTransform(new TranslateTransform(0, 1.2));
                dc.DrawGeometry(null, new WpfPen(Solid(Colors.White, 0.22), 1), crease);
                dc.Pop();
            }

            // Scorched, darkened edges
            dc.DrawGeometry(null, new WpfPen(Solid(burn, 0.06), 34), sheet);
            dc.DrawGeometry(null, new WpfPen(Solid(burn, 0.09), 16), sheet);
            dc.DrawGeometry(null, new WpfPen(Solid(burn, 0.20), 5), sheet);

            // Shadow cast by the rolls
            var topShade = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            topShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.34), 0));
            topShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 1));
            dc.DrawRectangle(topShade, null, new Rect(0, RodTop + RodHeight - 1, w, 11));
            var bottomShade = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            bottomShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0));
            bottomShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.24), 1));
            dc.DrawRectangle(bottomShade, null, new Rect(0, h - RodTop - RodHeight - 8, w, 9));

            // Inked double frame with lozenge corners
            var frame = new Rect(paperL + 7.5, RodTop + RodHeight + 5.5, paperR - paperL - 15, h - 2 * (RodTop + RodHeight) - 11);
            if (frame.Width > 20 && frame.Height > 20)
            {
                dc.DrawRectangle(null, new WpfPen(Solid(ink, 0.42), 1), frame);
                var inner = frame;
                inner.Inflate(-2.5, -2.5);
                dc.DrawRectangle(null, new WpfPen(Solid(ink, 0.18), 1), inner);
                foreach (var p in new[] { frame.TopLeft, frame.TopRight, frame.BottomLeft, frame.BottomRight })
                {
                    dc.DrawGeometry(Solid(deep), null, Lozenge(p, 3.6));
                }
                // Mid-edge flourishes
                dc.DrawGeometry(Solid(deep, 0.85), null, Lozenge(new WpfPoint(frame.Left + frame.Width / 2, frame.Bottom), 2.6));
                dc.DrawGeometry(Solid(deep, 0.85), null, Lozenge(new WpfPoint(frame.Left, frame.Top + frame.Height / 2), 2.2));
                dc.DrawGeometry(Solid(deep, 0.85), null, Lozenge(new WpfPoint(frame.Right, frame.Top + frame.Height / 2), 2.2));
            }

            dc.Pop();

            // Sheet outline
            dc.DrawGeometry(null, new WpfPen(Solid(Blend(ink, burn, 0.5), 0.55), 0.9), sheet);

            DrawRod(dc, w, RodTop, paper, burn);
            DrawRod(dc, w, h - RodTop - RodHeight, paper, burn);

            DrawSeal(dc, new WpfPoint(paperR - 21, h - RodTop - RodHeight + 1), wax);
        }

        group.Freeze();
        return group;
    }

    private static void DrawRod(DrawingContext dc, double w, double y, WpfColor paper, WpfColor burn)
    {
        double left = RollInset, right = w - RollInset;
        var roll = new Rect(left, y, right - left, RodHeight);

        // Rolled parchment cylinder
        var cyl = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
        cyl.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.62), 0));
        cyl.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.14), 0.2));
        cyl.GradientStops.Add(new GradientStop(Blend(paper, Colors.White, 0.28), 0.38));
        cyl.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.30), 0.72));
        cyl.GradientStops.Add(new GradientStop(Blend(paper, burn, 0.78), 1));
        dc.DrawRoundedRectangle(cyl, new WpfPen(Solid(Blend(burn, Colors.Black, 0.45), 0.85), 1), roll, 3.5, 3.5);

        dc.PushClip(new RectangleGeometry(roll, 3.5, 3.5));
        dc.DrawRectangle(GrainBrush.Value, null, roll);
        // Layer lines of the rolled sheet + darker ends
        dc.DrawRectangle(Solid(burn, 0.30), null, new Rect(left, y + RodHeight * 0.70, right - left, 1));
        dc.DrawRectangle(Solid(Colors.White, 0.22), null, new Rect(left, y + RodHeight * 0.30, right - left, 1));
        var endShade = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 0) };
        endShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.38), 0));
        endShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.06));
        endShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.94));
        endShade.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.38), 1));
        dc.DrawRectangle(endShade, null, roll);
        dc.Pop();

        // Gilded finials
        var gold = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
        gold.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0x8A, 0x63, 0x18), 0));
        gold.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0xF7, 0xE2, 0x9C), 0.3));
        gold.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0xC9, 0x9A, 0x35), 0.6));
        gold.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0x5E, 0x40, 0x0C), 1));
        var goldPen = new WpfPen(Solid(WpfColor.FromRgb(0x3A, 0x27, 0x06), 0.9), 0.9);
        double cy = y + RodHeight / 2;

        dc.DrawRoundedRectangle(gold, goldPen, new Rect(left - 5, y + 2.5, 5.5, RodHeight - 5), 1.5, 1.5);
        dc.DrawEllipse(gold, goldPen, new WpfPoint(left - 8.5, cy), 4, 6);
        dc.DrawRoundedRectangle(gold, goldPen, new Rect(right - 0.5, y + 2.5, 5.5, RodHeight - 5), 1.5, 1.5);
        dc.DrawEllipse(gold, goldPen, new WpfPoint(right + 8.5, cy), 4, 6);
    }

    private static void DrawSeal(DrawingContext dc, WpfPoint c, WpfColor wax)
    {
        WpfColor dark = Blend(wax, Colors.Black, 0.45);
        WpfColor light = Blend(wax, Colors.White, 0.30);

        dc.DrawEllipse(Solid(Colors.Black, 0.32), null, new WpfPoint(c.X + 1.2, c.Y + 1.8), 12.5, 12);

        // Irregular pressed-wax rim
        var rim = new GeometryGroup { FillRule = FillRule.Nonzero };
        rim.Children.Add(new EllipseGeometry(c, 12, 11.5));
        rim.Children.Add(new EllipseGeometry(new WpfPoint(c.X - 8, c.Y + 6.5), 4.5, 4));
        rim.Children.Add(new EllipseGeometry(new WpfPoint(c.X + 8.5, c.Y - 5.5), 4.2, 4.4));
        rim.Children.Add(new EllipseGeometry(new WpfPoint(c.X + 5, c.Y + 9), 3.6, 3.2));
        dc.DrawGeometry(Solid(Blend(wax, Colors.Black, 0.22)), null, rim);

        var face = new RadialGradientBrush { GradientOrigin = new WpfPoint(0.35, 0.3), Center = new WpfPoint(0.5, 0.5), RadiusX = 0.6, RadiusY = 0.6 };
        face.GradientStops.Add(new GradientStop(light, 0));
        face.GradientStops.Add(new GradientStop(wax, 0.55));
        face.GradientStops.Add(new GradientStop(Blend(wax, Colors.Black, 0.30), 1));
        dc.DrawEllipse(face, new WpfPen(Solid(dark, 0.9), 1), c, 8.6, 8.4);

        // Embossed ring emblem
        var emboss = new WpfPen(Solid(dark, 0.85), 1.2);
        dc.DrawEllipse(null, emboss, new WpfPoint(c.X, c.Y - 0.6), 3.9, 3.9);
        dc.DrawLine(emboss, new WpfPoint(c.X, c.Y - 6.2), new WpfPoint(c.X, c.Y + 6.2));
        dc.DrawLine(new WpfPen(Solid(dark, 0.85), 1), new WpfPoint(c.X - 4.2, c.Y + 3.4), new WpfPoint(c.X + 4.2, c.Y + 3.4));
        dc.DrawEllipse(null, new WpfPen(Solid(light, 0.35), 0.8), new WpfPoint(c.X - 0.5, c.Y - 1), 7.4, 7.2);
    }

    private static Geometry Lozenge(WpfPoint c, double r)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new WpfPoint(c.X, c.Y - r), true, true);
            g.LineTo(new WpfPoint(c.X + r, c.Y), true, false);
            g.LineTo(new WpfPoint(c.X, c.Y + r), true, false);
            g.LineTo(new WpfPoint(c.X - r, c.Y), true, false);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>Inward offset of the torn edge at a given row (0..~4.5 px, with the odd deeper nick).</summary>
    private static double Tear(int row, int seed)
    {
        double jitter = Hash(row, seed) * 2.8;
        double wave = (Math.Sin(row * 0.55 + seed % 7) + 1) * 0.5;
        double nick = Hash(row, seed + 57) > 0.88 ? 2.7 : 0;
        return jitter + wave + nick;
    }

    private static double Hash(int i, int seed)
    {
        unchecked
        {
            uint x = (uint)(i * 374761393 + seed * 668265263);
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }

    /// <summary>Tileable fractal-noise bitmap: dark blotches, light fibres and specks of rough parchment.</summary>
    private static ImageBrush CreateGrainBrush()
    {
        const int size = 160;
        var pixels = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double v = 0.38 * ValueNoise(x, y, size, 5, 1)
                         + 0.26 * ValueNoise(x, y, size, 10, 2)
                         + 0.20 * ValueNoise(x, y, size, 32, 3)
                         + 0.16 * ValueNoise(x, y, size, 80, 5);
                double fibre = ValueNoise(x * 6 % size, y, size, 40, 4); // stretched = horizontal fibres

                double darkA = Math.Clamp((v - 0.49) * 2.4, 0, 1) * 0.44 + Math.Clamp((fibre - 0.68) * 2, 0, 1) * 0.14;
                double lightA = Math.Clamp((0.45 - v) * 2.2, 0, 1) * 0.30;
                darkA += Math.Max(0, Hash(x + y * size, 77) - 0.55) * 0.10; // fine tooth
                if (Hash(x + y * size, 91) > 0.988) darkA += 0.26;
                darkA = Math.Min(darkA, 0.75);

                // Composite light (cream) then dark (umber) into one premultiplied pixel
                double a = lightA + darkA * (1 - lightA);
                double r = (255 * lightA * (1 - darkA) + 78 * darkA);
                double gC = (244 * lightA * (1 - darkA) + 50 * darkA);
                double b = (214 * lightA * (1 - darkA) + 18 * darkA);

                int o = (y * size + x) * 4;
                pixels[o] = (byte)Math.Clamp(b, 0, 255);
                pixels[o + 1] = (byte)Math.Clamp(gC, 0, 255);
                pixels[o + 2] = (byte)Math.Clamp(r, 0, 255);
                pixels[o + 3] = (byte)Math.Clamp(a * 255, 0, 255);
            }
        }

        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size * 1.25, size * 1.25),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        brush.Freeze();
        return brush;
    }

    /// <summary>Smooth lattice noise that wraps at <paramref name="size"/> so the bitmap tiles seamlessly.</summary>
    private static double ValueNoise(int x, int y, int size, int cells, int seed)
    {
        double fx = x / (double)size * cells, fy = y / (double)size * cells;
        int x0 = (int)fx, y0 = (int)fy;
        double tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
        x0 %= cells; y0 %= cells;
        double a = Hash(x0 + y0 * 131, seed), b = Hash(x1 + y0 * 131, seed);
        double c = Hash(x0 + y1 * 131, seed), d = Hash(x1 + y1 * 131, seed);
        return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
    }

    private static SolidColorBrush Solid(WpfColor c, double alpha = 1)
    {
        var b = new SolidColorBrush(alpha >= 1 ? c : WithAlpha(c, alpha));
        b.Freeze();
        return b;
    }

    private static WpfColor WithAlpha(WpfColor c, double alpha) => NoteSkin.WithAlpha(c, alpha);

    private static WpfColor Blend(WpfColor a, WpfColor b, double t) => WpfColor.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
