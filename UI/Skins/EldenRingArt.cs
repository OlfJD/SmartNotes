using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SmartNotes.Core.Models;
using WpfPen = System.Windows.Media.Pen;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Elden Ring skin in the visual language of the game's menus, worn down: a murky olive
/// panel with grain, mist and grime, scratches and dust, a chipped pale-gold double frame with
/// notched corners, the note's Great Rune glowing in the bottom right (drawn by RuneArt) and
/// drifting motes of light. Every frame line is drawn as a broken, slightly wobbling stroke.
/// All shapes are drawn in code; no game assets or logos are used.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Elden Ring skin.
/// </summary>
public static class EldenRingArt
{
    private const double M = 5;             // outer margin (room for the glow)
    private const double Notch = 9;         // radius of the concave corners
    private const double HeaderBottom = 55; // content top inset (11) + header row (44)

    private static readonly WpfColor Soot = WpfColor.FromRgb(0x0A, 0x09, 0x05);
    private static readonly WpfColor Mist = WpfColor.FromRgb(0xC9, 0xC2, 0xA4);

    public static Drawing Build(double w, double h, NoteColorTheme theme, string tag, string runeKey, double pixelsPerDip)
    {
        WpfColor glow = theme.GlowBrush.Color;
        WpfColor frame = theme.BorderBrush.Color;
        WpfColor top = theme.HeaderBgBrush.Color;
        WpfColor bg = theme.BgBrush.Color;

        int seed = 23;
        foreach (char c in tag) seed = unchecked(seed * 31 + c);

        Geometry panel = NotchedRect(M, M, w - M, h - M, Notch);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Faint aura around the silhouette
            dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.04), 7), panel);
            dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.07), 3.5), panel);

            // Murky panel
            var fill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            fill.GradientStops.Add(new GradientStop(top, 0));
            fill.GradientStops.Add(new GradientStop(bg, 0.38));
            fill.GradientStops.Add(new GradientStop(Blend(bg, Colors.Black, 0.45), 1));
            dc.DrawGeometry(fill, null, panel);

            dc.PushClip(panel);

            // Grain: fine tooth, then the same texture blown up into cloudy mottling
            dc.PushOpacity(0.30);
            dc.DrawRectangle(EldenScrollArt.Grain, null, new Rect(0, 0, w, h));
            dc.Pop();
            dc.PushTransform(new ScaleTransform(3.2, 2.6));
            dc.PushOpacity(0.24);
            dc.DrawRectangle(EldenScrollArt.Grain, null, new Rect(0, 0, w / 3.2 + 1, h / 2.6 + 1));
            dc.Pop();
            dc.Pop();

            DrawMottle(dc, w, h, seed);

            // Light of grace from above, embers from below
            var grace = new RadialGradientBrush { Center = new WpfPoint(0.5, 0), GradientOrigin = new WpfPoint(0.5, 0), RadiusX = 0.85, RadiusY = 0.55 };
            grace.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.15), 0));
            grace.GradientStops.Add(new GradientStop(WithAlpha(glow, 0), 1));
            dc.DrawRectangle(grace, null, new Rect(0, 0, w, h));
            var ember = new RadialGradientBrush { Center = new WpfPoint(0.72, 1), GradientOrigin = new WpfPoint(0.72, 1), RadiusX = 0.7, RadiusY = 0.42 };
            ember.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.10), 0));
            ember.GradientStops.Add(new GradientStop(WithAlpha(glow, 0), 1));
            dc.DrawRectangle(ember, null, new Rect(0, 0, w, h));

            // Vignette
            var vignette = new RadialGradientBrush { RadiusX = 0.78, RadiusY = 0.78 };
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.5));
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.46), 1));
            dc.DrawRectangle(vignette, null, new Rect(0, 0, w, h));

            // The note's Great Rune, glowing in the bottom right (see RuneArt)
            if (w > 200 && h > 230)
            {
                double R = Math.Clamp(Math.Min(w, h) * 0.12, 30, 46);
                double box = R / 0.29;
                var c = new WpfPoint(w - M - 1.6 * R, h - M - 1.9 * R);
                if (c.Y - box / 2 > HeaderBottom - 10)
                {
                    var rune = RuneArt.Render(runeKey, (int)Math.Round(box * Math.Max(1, pixelsPerDip)));
                    dc.DrawImage(rune, new Rect(c.X - box / 2, c.Y - box / 2, box, box));
                }
            }

            DrawScratches(dc, w, h, seed);

            // Header strip and its worn rule
            var strip = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            strip.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.05), 0));
            strip.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.01), 1));
            dc.DrawRectangle(strip, null, new Rect(0, 0, w, HeaderBottom));
            Worn(dc, Line(new WpfPoint(M + 12, HeaderBottom - 0.5), new WpfPoint(w - M - 12, HeaderBottom - 0.5), 3), Pens(frame, 0.72, 1), seed + 430, 0.3, 0.10);
            var ruleStud = Solid(Blend(frame, glow, 0.35), 0.85);
            dc.DrawGeometry(ruleStud, null, Lozenge(new WpfPoint(M + 12, HeaderBottom - 0.5), 2, 2));
            dc.DrawGeometry(ruleStud, null, Lozenge(new WpfPoint(w - M - 12, HeaderBottom - 0.5), 2, 2));

            // Motes of light drifting up from the bottom
            for (int i = 0; i < 11; i++)
            {
                double x = M + 14 + Hash(i, seed) * (w - 2 * M - 28);
                double y = h - M - 10 - Math.Pow(Hash(i, seed + 5), 1.6) * (h * 0.6);
                double size = 0.6 + Hash(i, seed + 11) * 1.1;
                double a = 0.35 + Hash(i, seed + 17) * 0.5;
                dc.DrawEllipse(Solid(glow, a * 0.15), null, new WpfPoint(x, y), size * 3.6, size * 3.6);
                dc.DrawEllipse(Solid(Blend(glow, Colors.White, 0.45), a), null, new WpfPoint(x, y), size, size);
            }

            // Grime gathered along the frame
            dc.DrawGeometry(null, new WpfPen(Solid(Soot, 0.16), 22), panel);
            dc.DrawGeometry(null, new WpfPen(Solid(Soot, 0.30), 7), panel);

            dc.Pop();

            DrawFrame(dc, w, h, frame, glow, seed);
        }

        group.Freeze();
        return group;
    }

    // ------------------------------------------------------------------ surface wear

    /// <summary>Dark blotches and low drifting mist banks.</summary>
    private static void DrawMottle(DrawingContext dc, double w, double h, int seed)
    {
        for (int i = 0; i < 8; i++)
        {
            var p = new WpfPoint(Hash(i, seed + 7) * w, Hash(i, seed + 13) * h);
            double r = 28 + Hash(i, seed + 23) * 70;
            var blotch = new RadialGradientBrush();
            blotch.GradientStops.Add(new GradientStop(WithAlpha(Soot, 0.30), 0));
            blotch.GradientStops.Add(new GradientStop(WithAlpha(Soot, 0.14), 0.6));
            blotch.GradientStops.Add(new GradientStop(WithAlpha(Soot, 0), 1));
            dc.DrawEllipse(blotch, null, p, r, r * (0.55 + Hash(i, seed + 31) * 0.6));
        }

        for (int i = 0; i < 4; i++)
        {
            var p = new WpfPoint(Hash(i, seed + 37) * w, (0.35 + Hash(i, seed + 41) * 0.6) * h);
            double r = 50 + Hash(i, seed + 43) * 90;
            var mist = new RadialGradientBrush();
            mist.GradientStops.Add(new GradientStop(WithAlpha(Mist, 0.055), 0));
            mist.GradientStops.Add(new GradientStop(WithAlpha(Mist, 0), 1));
            dc.DrawEllipse(mist, null, p, r, r * (0.3 + Hash(i, seed + 47) * 0.3));
        }
    }

    /// <summary>Scratches (some doubled, like a blade drawn across), and dust.</summary>
    private static void DrawScratches(DrawingContext dc, double w, double h, int seed)
    {
        var light = new WpfPen(Solid(Mist, 0.09), 0.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var dark = new WpfPen(Solid(Soot, 0.45), 0.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        light.Freeze();
        dark.Freeze();

        for (int i = 0; i < 24; i++)
        {
            var p = new WpfPoint(M + Hash(i, seed + 111) * (w - 2 * M), M + Hash(i, seed + 113) * (h - 2 * M));
            double angle = -0.5 + (Hash(i, seed + 117) - 0.5) * 0.9 + (i % 4 == 0 ? 1.2 : 0);
            double len = 8 + Math.Pow(Hash(i, seed + 119), 1.5) * 60;
            var d = new Vector(Math.Cos(angle), Math.Sin(angle));
            var q = p + d * len;
            var pen = i % 3 == 0 ? dark : light;
            dc.DrawLine(pen, p, q);
            if (Hash(i, seed + 121) > 0.7)
            {
                var n = new Vector(-d.Y, d.X) * 1.6;
                dc.DrawLine(pen, p + n + d * (len * 0.15), q + n - d * (len * 0.2));
            }
        }

        for (int i = 0; i < 40; i++)
        {
            var p = new WpfPoint(M + Hash(i, seed + 161) * (w - 2 * M), M + Hash(i, seed + 163) * (h - 2 * M));
            double s = 0.3 + Math.Pow(Hash(i, seed + 167), 3) * 1.0;
            bool bright = Hash(i, seed + 169) > 0.45;
            dc.DrawEllipse(bright ? Solid(Mist, 0.06 + Hash(i, seed + 171) * 0.12) : Solid(Soot, 0.4), null, p, s, s);
        }
    }

    // ------------------------------------------------------------------ frame

    private static void DrawFrame(DrawingContext dc, double w, double h, WpfColor frame, WpfColor glow, int seed)
    {
        var outer = NotchedOutline(M, M, w - M, h - M, Notch);
        var inner = NotchedOutline(M + 3.5, M + 3.5, w - M - 3.5, h - M - 3.5, Notch);

        // Dark under-stroke so the chipped gold reads as metal on a worn edge
        Worn(dc, outer, Pens(Soot, 0.55, 2.4), seed + 390, 0.3, 0.02);
        Worn(dc, outer, Pens(frame, 0.95, 1.1), seed + 400, 0.3, 0.07);
        Worn(dc, inner, Pens(frame, 0.40, 1.0), seed + 420, 0.3, 0.20);

        // Studs in the notched corners, lozenges on the edge centres
        var stud = Solid(Blend(frame, glow, 0.5));
        foreach (var p in new[] { new WpfPoint(M + 2.2, M + 2.2), new WpfPoint(w - M - 2.2, M + 2.2), new WpfPoint(M + 2.2, h - M - 2.2), new WpfPoint(w - M - 2.2, h - M - 2.2) })
        {
            dc.DrawGeometry(stud, null, Lozenge(p, 2.3, 2.3));
        }

        var edge = Solid(Blend(frame, glow, 0.35));
        var edgePens = Pens(Blend(frame, glow, 0.35), 1, 2);
        foreach (double y in new[] { M, h - M })
        {
            dc.DrawGeometry(edge, null, Lozenge(new WpfPoint(w / 2, y), 4.2, 3.2));
            Worn(dc, Line(new WpfPoint(w / 2 - 40, y), new WpfPoint(w / 2 - 8, y), 3), edgePens, seed + (int)y, 0.2, 0.1);
            Worn(dc, Line(new WpfPoint(w / 2 + 8, y), new WpfPoint(w / 2 + 40, y), 3), edgePens, seed + (int)y + 7, 0.2, 0.1);
            dc.DrawGeometry(edge, null, Lozenge(new WpfPoint(w / 2 - 44, y), 1.8, 1.5));
            dc.DrawGeometry(edge, null, Lozenge(new WpfPoint(w / 2 + 44, y), 1.8, 1.5));
        }
    }

    /// <summary>Points along a notched rectangle outline (concave quarter-circle corners), clockwise from the top-left.</summary>
    private static List<WpfPoint> NotchedOutline(double l, double t, double r, double b, double n)
    {
        var pts = new List<WpfPoint>();
        pts.AddRange(Line(new WpfPoint(l + n, t), new WpfPoint(r - n, t), 3));
        pts.AddRange(Arc(new WpfPoint(r, t), n, 180, 90));
        pts.AddRange(Line(new WpfPoint(r, t + n), new WpfPoint(r, b - n), 3));
        pts.AddRange(Arc(new WpfPoint(r, b), n, 270, 180));
        pts.AddRange(Line(new WpfPoint(r - n, b), new WpfPoint(l + n, b), 3));
        pts.AddRange(Arc(new WpfPoint(l, b), n, 0, -90));
        pts.AddRange(Line(new WpfPoint(l, b - n), new WpfPoint(l, t + n), 3));
        pts.AddRange(Arc(new WpfPoint(l, t), n, 90, 0));
        return pts;
    }

    // ------------------------------------------------------------------ strokes

    /// <summary>
    /// Draws a polyline the way old gilding survives: in short runs of varying strength with small
    /// gaps, every point nudged a little. Deterministic for a given seed.
    /// </summary>
    private static void Worn(DrawingContext dc, List<WpfPoint> pts, WpfPen[] pens, int seed, double wobble, double gapChance)
    {
        int i = 0, run = 0;
        while (i < pts.Count - 1)
        {
            bool gap = Hash(run, seed + 3) < gapChance;
            int len = gap ? 1 : 2 + (int)(Hash(run, seed) * 7);
            int end = Math.Min(pts.Count - 1, i + len);
            if (!gap)
            {
                var pen = pens[(int)(Hash(run, seed + 7) * pens.Length) % pens.Length];
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(Nudge(pts[i], i, seed, wobble), false, false);
                    for (int k = i + 1; k <= end; k++) g.LineTo(Nudge(pts[k], k, seed, wobble), true, true);
                }
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }
            i = end;
            run++;
        }
    }

    private static WpfPoint Nudge(WpfPoint p, int i, int seed, double amount) =>
        new(p.X + (Hash(i, seed + 11) - 0.5) * 2 * amount, p.Y + (Hash(i, seed + 13) - 0.5) * 2 * amount);

    /// <summary>The same pen at four strengths, for <see cref="Worn"/> to pick from.</summary>
    private static WpfPen[] Pens(WpfColor color, double alpha, double thickness)
    {
        var levels = new[] { 1.0, 0.82, 0.62, 0.40 };
        var pens = new WpfPen[levels.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            pens[i] = new WpfPen(Solid(color, alpha * levels[i]), thickness * (0.85 + 0.15 * levels[i]))
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            pens[i].Freeze();
        }
        return pens;
    }

    /// <summary>Points along an arc (degrees, 0 = right, 90 = down), roughly 3 px apart.</summary>
    private static List<WpfPoint> Arc(WpfPoint c, double radius, double from, double to)
    {
        int n = Math.Max(3, (int)Math.Ceiling(Math.Abs(to - from) * Math.PI / 180 * radius / 3));
        var pts = new List<WpfPoint>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double a = (from + (to - from) * i / n) * Math.PI / 180;
            pts.Add(new WpfPoint(c.X + Math.Cos(a) * radius, c.Y + Math.Sin(a) * radius));
        }
        return pts;
    }

    private static List<WpfPoint> Line(WpfPoint a, WpfPoint b, double step)
    {
        int n = Math.Max(1, (int)Math.Ceiling((b - a).Length / step));
        var pts = new List<WpfPoint>(n + 1);
        for (int i = 0; i <= n; i++) pts.Add(a + (b - a) * ((double)i / n));
        return pts;
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

    private static Geometry Lozenge(WpfPoint c, double rx, double ry)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new WpfPoint(c.X, c.Y - ry), true, true);
            g.LineTo(new WpfPoint(c.X + rx, c.Y), true, false);
            g.LineTo(new WpfPoint(c.X, c.Y + ry), true, false);
            g.LineTo(new WpfPoint(c.X - rx, c.Y), true, false);
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
