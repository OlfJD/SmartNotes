using System;
using System.Windows;
using System.Windows.Media;
using SmartNotes.Core.Models;
using WpfPen = System.Windows.Media.Pen;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Astral skin: a night-sky panel (deep navy, a soft nebula in the note's colour, a seeded
/// field of faint stars and a dotted ecliptic arc), one constellation drawn as linked, flaring stars in the
/// lower right, and a thin silver double frame with notched corners and small four-point sparkles.
/// The constellation is picked from the note's id, so every note keeps its own figure.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Astral skin.
/// </summary>
public static class AstralArt
{
    private const double M = 5;            // outer margin (room for the glow)
    private const double Notch = 9;        // radius of the concave corners
    private const double HeaderBottom = 55; // content top inset (11) + header row (44)

    /// <summary>A constellation: stars in a 0..1 box (x, y, brightness 0..1) and the lines between them.</summary>
    private sealed record Figure(string Name, (double x, double y, double mag)[] Stars, (int a, int b)[] Lines);

    // Real star patterns (public sky), simplified.
    private static readonly Figure[] Figures =
    {
        new("Cassiopeia",
            new[] { (0.04, 0.34, 0.7), (0.27, 0.62, 0.85), (0.47, 0.36, 1.0), (0.70, 0.58, 0.75), (0.95, 0.24, 0.8) },
            new[] { (0, 1), (1, 2), (2, 3), (3, 4) }),
        new("Ursa Major",
            new[] { (0.02, 0.28, 0.8), (0.20, 0.34, 0.85), (0.36, 0.44, 0.8), (0.52, 0.54, 0.6), (0.56, 0.84, 0.85), (0.90, 0.88, 0.9), (0.95, 0.58, 1.0) },
            new[] { (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 3) }),
        new("Cygnus",
            new[] { (0.50, 0.04, 1.0), (0.50, 0.42, 0.85), (0.50, 0.96, 0.75), (0.10, 0.26, 0.6), (0.30, 0.35, 0.75), (0.70, 0.50, 0.75), (0.90, 0.62, 0.6) },
            new[] { (0, 1), (1, 2), (3, 4), (4, 1), (1, 5), (5, 6) }),
        new("Orion",
            new[] { (0.22, 0.10, 1.0), (0.78, 0.16, 0.8), (0.41, 0.50, 0.75), (0.50, 0.47, 0.8), (0.59, 0.44, 0.75), (0.28, 0.90, 0.8), (0.74, 0.86, 1.0), (0.50, 0.00, 0.55) },
            new[] { (7, 0), (7, 1), (0, 2), (1, 4), (2, 3), (3, 4), (2, 5), (4, 6) }),
        new("Lyra",
            new[] { (0.30, 0.06, 1.0), (0.40, 0.34, 0.65), (0.60, 0.30, 0.65), (0.68, 0.66, 0.7), (0.47, 0.70, 0.7) },
            new[] { (0, 1), (0, 2), (1, 2), (2, 3), (3, 4), (4, 1) }),
    };

    public static Drawing Build(double w, double h, NoteColorTheme theme, string tag, double bottomReserve = 0)
    {
        WpfColor glow = theme.GlowBrush.Color;
        WpfColor frame = theme.BorderBrush.Color;
        WpfColor top = theme.HeaderBgBrush.Color;
        WpfColor bg = theme.BgBrush.Color;
        WpfColor starWhite = WpfColor.FromRgb(0xEE, 0xF2, 0xFF);
        WpfColor starBlue = WpfColor.FromRgb(0xBF, 0xD2, 0xFF);

        int seed = 23;
        foreach (char c in tag) seed = unchecked(seed * 31 + c);

        Geometry panel = NotchedRect(M, M, w - M, h - M, Notch);
        Geometry inner = NotchedRect(M + 3.5, M + 3.5, w - M - 3.5, h - M - 3.5, Notch);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Faint halo around the silhouette
            dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.05), 7), panel);
            dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.08), 3.5), panel);

            // Night sky
            var fill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            fill.GradientStops.Add(new GradientStop(top, 0));
            fill.GradientStops.Add(new GradientStop(bg, 0.35));
            fill.GradientStops.Add(new GradientStop(Blend(bg, Colors.Black, 0.45), 1));
            dc.DrawGeometry(fill, null, panel);

            dc.PushClip(panel);

            // Nebula: one cloud in the note colour, one cool blue, placed by the seed
            double nx = 0.25 + Hash(1, seed) * 0.5, ny = 0.45 + Hash(2, seed) * 0.35;
            DrawCloud(dc, w, h, nx, ny, 0.75, 0.45, WithAlpha(glow, 0.13));
            DrawCloud(dc, w, h, 1 - nx * 0.8, 0.25 + Hash(3, seed) * 0.3, 0.6, 0.35, WithAlpha(starBlue, 0.07));

            // Star field: many faint points, a few with a soft glow
            int count = (int)Math.Clamp(w * h / 900, 40, 160);
            for (int i = 0; i < count; i++)
            {
                double x = M + 4 + Hash(i, seed + 101) * (w - 2 * M - 8);
                double y = M + 4 + Hash(i, seed + 211) * (h - 2 * M - 8);
                double mag = Math.Pow(Hash(i, seed + 307), 3);           // most stars are dim
                double r = 0.35 + mag * 0.9;
                WpfColor col = Hash(i, seed + 401) < 0.3 ? starBlue : starWhite;
                if (mag > 0.55) dc.DrawEllipse(Solid(col, 0.08 + mag * 0.08), null, new WpfPoint(x, y), r * 3.2, r * 3.2);
                dc.DrawEllipse(Solid(col, 0.18 + mag * 0.6), null, new WpfPoint(x, y), r, r);
            }

            // Ecliptic: a faint dotted arc sweeping across the sky
            var ecliptic = new StreamGeometry();
            using (var g = ecliptic.Open())
            {
                g.BeginFigure(new WpfPoint(-w * 0.1, h * (0.78 + Hash(4, seed) * 0.1)), false, false);
                g.ArcTo(new WpfPoint(w * 1.1, h * (0.42 + Hash(5, seed) * 0.15)), new WpfSize(w * 1.1, w * 1.1), 0, false, SweepDirection.Clockwise, true, false);
            }
            ecliptic.Freeze();
            dc.DrawGeometry(null, new WpfPen(Solid(frame, 0.22), 0.8) { DashStyle = new DashStyle(new[] { 1.0, 5.0 }, 0), DashCap = PenLineCap.Round }, ecliptic);

            // Vignette
            var vignette = new RadialGradientBrush { RadiusX = 0.8, RadiusY = 0.8 };
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.55));
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.4), 1));
            dc.DrawRectangle(vignette, null, new Rect(0, 0, w, h));

            // The note's constellation, lower right, clear of the footer / input row
            if (w > 200)
            {
                var fig = Figures[(int)(Hash(6, seed) * Figures.Length) % Figures.Length];
                double floor = h - Math.Max(bottomReserve, M + 8);
                double box = Math.Min(Math.Min(w, h) * 0.46, 150);
                box = Math.Min(box, (floor - HeaderBottom) * 0.8);
                if (box >= 70)
                {
                    var origin = new WpfPoint(w - M - 14 - box, floor - 10 - box);
                    DrawFigure(dc, fig, origin, box, glow, starWhite);
                }
            }

            // Header strip and its rule
            var strip = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            strip.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.05), 0));
            strip.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.01), 1));
            dc.DrawRectangle(strip, null, new Rect(0, 0, w, HeaderBottom));
            var rule = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 0) };
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0), 0));
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0.7), 0.15));
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0.7), 0.85));
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0), 1));
            dc.DrawRectangle(rule, null, new Rect(M + 4, HeaderBottom - 0.5, w - 2 * M - 8, 1));

            dc.Pop();

            // Silver double frame
            dc.DrawGeometry(null, new WpfPen(Solid(frame, 0.85), 1), panel);
            dc.DrawGeometry(null, new WpfPen(Solid(frame, 0.3), 1), inner);

            // Sparkles in the notched corners and on the top / bottom edge centres
            var spark = Solid(Blend(frame, starWhite, 0.5));
            foreach (var p in new[] { new WpfPoint(M + 2.2, M + 2.2), new WpfPoint(w - M - 2.2, M + 2.2), new WpfPoint(M + 2.2, h - M - 2.2), new WpfPoint(w - M - 2.2, h - M - 2.2) })
                dc.DrawGeometry(spark, null, Sparkle(p, 3.2));
            dc.DrawGeometry(spark, null, Sparkle(new WpfPoint(w / 2, M), 5));
            dc.DrawGeometry(spark, null, Sparkle(new WpfPoint(w / 2, h - M), 5));
        }

        group.Freeze();
        return group;
    }

    private static void DrawCloud(DrawingContext dc, double w, double h, double cx, double cy, double rx, double ry, WpfColor color)
    {
        var b = new RadialGradientBrush { Center = new WpfPoint(cx, cy), GradientOrigin = new WpfPoint(cx, cy), RadiusX = rx, RadiusY = ry };
        b.GradientStops.Add(new GradientStop(color, 0));
        b.GradientStops.Add(new GradientStop(WithAlpha(color, 0), 1));
        dc.DrawRectangle(b, null, new Rect(0, 0, w, h));
    }

    /// <summary>Lines first (faint, slightly shortened so they don't touch the stars), then glowing stars;
    /// the brightest get a four-point flare.</summary>
    private static void DrawFigure(DrawingContext dc, Figure fig, WpfPoint origin, double box, WpfColor glow, WpfColor white)
    {
        WpfPoint P(int i) => new(origin.X + fig.Stars[i].x * box, origin.Y + fig.Stars[i].y * box);

        var lineGlow = new WpfPen(Solid(glow, 0.10), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var line = new WpfPen(Solid(Blend(glow, white, 0.5), 0.45), 0.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        foreach (var (a, b) in fig.Lines)
        {
            WpfPoint pa = P(a), pb = P(b);
            Vector d = pb - pa;
            if (d.Length < 8) continue;
            d.Normalize();
            WpfPoint s = pa + d * 4, e = pb - d * 4;
            dc.DrawLine(lineGlow, s, e);
            dc.DrawLine(line, s, e);
        }

        for (int i = 0; i < fig.Stars.Length; i++)
        {
            var p = P(i);
            double mag = fig.Stars[i].mag;
            double r = 1.1 + mag * 1.5;
            var halo = new RadialGradientBrush();
            halo.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.55 * mag), 0));
            halo.GradientStops.Add(new GradientStop(WithAlpha(glow, 0), 1));
            dc.DrawEllipse(halo, null, p, r * 5, r * 5);
            if (mag >= 0.85)
            {
                var flare = Solid(Blend(glow, white, 0.6), 0.7);
                dc.DrawGeometry(flare, null, Sparkle(p, r * 4.2, 0.12));
            }
            dc.DrawEllipse(Solid(Blend(glow, white, 0.75)), null, p, r, r);
        }
    }

    /// <summary>Four-point star; <paramref name="waist"/> is the width of the arms relative to the radius.</summary>
    private static Geometry Sparkle(WpfPoint c, double r, double waist = 0.22)
    {
        double k = r * waist;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new WpfPoint(c.X, c.Y - r), true, true);
            g.LineTo(new WpfPoint(c.X + k, c.Y - k), true, false);
            g.LineTo(new WpfPoint(c.X + r, c.Y), true, false);
            g.LineTo(new WpfPoint(c.X + k, c.Y + k), true, false);
            g.LineTo(new WpfPoint(c.X, c.Y + r), true, false);
            g.LineTo(new WpfPoint(c.X - k, c.Y + k), true, false);
            g.LineTo(new WpfPoint(c.X - r, c.Y), true, false);
            g.LineTo(new WpfPoint(c.X - k, c.Y - k), true, false);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>Rectangle whose four corners are cut by concave quarter circles.</summary>
    private static Geometry NotchedRect(double l, double t, double r, double b, double n)
    {
        var size = new WpfSize(n, n);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new WpfPoint(l + n, t), true, true);
            g.LineTo(new WpfPoint(r - n, t), true, false);
            g.ArcTo(new WpfPoint(r, t + n), size, 0, false, SweepDirection.Counterclockwise, true, false);
            g.LineTo(new WpfPoint(r, b - n), true, false);
            g.ArcTo(new WpfPoint(r - n, b), size, 0, false, SweepDirection.Counterclockwise, true, false);
            g.LineTo(new WpfPoint(l + n, b), true, false);
            g.ArcTo(new WpfPoint(l, b - n), size, 0, false, SweepDirection.Counterclockwise, true, false);
            g.LineTo(new WpfPoint(l, t + n), true, false);
            g.ArcTo(new WpfPoint(l + n, t), size, 0, false, SweepDirection.Counterclockwise, true, false);
        }
        geo.Freeze();
        return geo;
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
