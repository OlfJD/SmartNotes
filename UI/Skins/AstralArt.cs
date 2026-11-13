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
/// Paints the Astral skin: a murky dark panel with a vignette, a thin pale-gold double frame with
/// notched (concave) corners, a header strip with a ruled line, a faint glowing ring in the note's
/// colour and a few drifting motes of light. This was the first "Elden Ring" look; it is kept as the
/// starting point for an astrology / starry-sky skin (ideas: constellations, star chart rings).
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Astral skin.
/// </summary>
public static class AstralArt
{
    private const double M = 5;            // outer margin (room for the glow)
    private const double Notch = 9;        // radius of the concave corners
    private const double HeaderBottom = 55; // content top inset (11) + header row (44)

    public static Drawing Build(double w, double h, NoteColorTheme theme, string tag)
    {
        WpfColor glow = theme.GlowBrush.Color;
        WpfColor frame = theme.BorderBrush.Color;
        WpfColor top = theme.HeaderBgBrush.Color;
        WpfColor bg = theme.BgBrush.Color;

        int seed = 23;
        foreach (char c in tag) seed = unchecked(seed * 31 + c);

        Geometry panel = NotchedRect(M, M, w - M, h - M, Notch);
        Geometry inner = NotchedRect(M + 3.5, M + 3.5, w - M - 3.5, h - M - 3.5, Notch);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Faint aura around the silhouette
            dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.05), 7), panel);
            dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.09), 3.5), panel);

            // Murky panel
            var fill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            fill.GradientStops.Add(new GradientStop(top, 0));
            fill.GradientStops.Add(new GradientStop(bg, 0.38));
            fill.GradientStops.Add(new GradientStop(Blend(bg, Colors.Black, 0.42), 1));
            dc.DrawGeometry(fill, null, panel);

            dc.PushClip(panel);

            // Mist: soft rough texture, very low contrast
            dc.PushOpacity(0.22);
            dc.DrawRectangle(EldenScrollArt.Grain, null, new Rect(0, 0, w, h));
            dc.Pop();

            // Light of grace from above, embers from below
            var grace = new RadialGradientBrush { Center = new WpfPoint(0.5, 0), GradientOrigin = new WpfPoint(0.5, 0), RadiusX = 0.85, RadiusY = 0.55 };
            grace.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.17), 0));
            grace.GradientStops.Add(new GradientStop(WithAlpha(glow, 0), 1));
            dc.DrawRectangle(grace, null, new Rect(0, 0, w, h));
            var ember = new RadialGradientBrush { Center = new WpfPoint(0.72, 1), GradientOrigin = new WpfPoint(0.72, 1), RadiusX = 0.7, RadiusY = 0.42 };
            ember.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.10), 0));
            ember.GradientStops.Add(new GradientStop(WithAlpha(glow, 0), 1));
            dc.DrawRectangle(ember, null, new Rect(0, 0, w, h));

            // Vignette
            var vignette = new RadialGradientBrush { RadiusX = 0.78, RadiusY = 0.78 };
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.55));
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.38), 1));
            dc.DrawRectangle(vignette, null, new Rect(0, 0, w, h));

            // Glowing ring, bottom right (our own simple ring-and-stroke motif)
            if (w > 230 && h > 250)
            {
                var c = new WpfPoint(w - M - 62, h - M - 78);
                double r = 36;
                dc.DrawEllipse(null, new WpfPen(Solid(glow, 0.035), 12), c, r, r);
                dc.DrawEllipse(null, new WpfPen(Solid(glow, 0.07), 5), c, r, r);
                dc.DrawEllipse(null, new WpfPen(Solid(glow, 0.26), 1.3), c, r, r);
                dc.DrawLine(new WpfPen(Solid(glow, 0.06), 5), new WpfPoint(c.X, c.Y - r - 20), new WpfPoint(c.X, c.Y + r + 16));
                dc.DrawLine(new WpfPen(Solid(glow, 0.24), 1.1), new WpfPoint(c.X, c.Y - r - 20), new WpfPoint(c.X, c.Y + r + 16));
                // a second, larger arc crossing the ring
                var arc = new StreamGeometry();
                using (var g = arc.Open())
                {
                    g.BeginFigure(new WpfPoint(c.X - r - 16, c.Y - 18), false, false);
                    g.ArcTo(new WpfPoint(c.X + r + 16, c.Y - 18), new WpfSize(r + 22, r + 22), 0, false, SweepDirection.Counterclockwise, true, false);
                }
                arc.Freeze();
                dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.05), 4), arc);
                dc.DrawGeometry(null, new WpfPen(Solid(glow, 0.17), 1), arc);
            }

            // Header strip and its rule
            var strip = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            strip.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.055), 0));
            strip.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.012), 1));
            dc.DrawRectangle(strip, null, new Rect(0, 0, w, HeaderBottom));
            var rule = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 0) };
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0), 0));
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0.75), 0.12));
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0.75), 0.88));
            rule.GradientStops.Add(new GradientStop(WithAlpha(frame, 0), 1));
            dc.DrawRectangle(rule, null, new Rect(M + 4, HeaderBottom - 0.5, w - 2 * M - 8, 1));

            // Motes of light drifting up from the bottom
            for (int i = 0; i < 9; i++)
            {
                double x = M + 14 + Hash(i, seed) * (w - 2 * M - 28);
                double y = h - M - 10 - Math.Pow(Hash(i, seed + 5), 1.6) * (h * 0.55);
                double size = 0.7 + Hash(i, seed + 11) * 1.1;
                double a = 0.35 + Hash(i, seed + 17) * 0.5;
                dc.DrawEllipse(Solid(glow, a * 0.16), null, new WpfPoint(x, y), size * 3.4, size * 3.4);
                dc.DrawEllipse(Solid(Blend(glow, Colors.White, 0.45), a), null, new WpfPoint(x, y), size, size);
            }

            dc.Pop();

            // Double frame
            dc.DrawGeometry(null, new WpfPen(Solid(frame, 0.9), 1), panel);
            dc.DrawGeometry(null, new WpfPen(Solid(frame, 0.32), 1), inner);

            // Studs sitting in the notched corners, lozenges on the edge centres
            foreach (var p in new[] { new WpfPoint(M + 2.2, M + 2.2), new WpfPoint(w - M - 2.2, M + 2.2), new WpfPoint(M + 2.2, h - M - 2.2), new WpfPoint(w - M - 2.2, h - M - 2.2) })
            {
                dc.DrawGeometry(Solid(Blend(frame, glow, 0.5)), null, Lozenge(p, 2.3));
            }
            var edge = Solid(Blend(frame, glow, 0.35));
            dc.DrawGeometry(edge, null, Lozenge(new WpfPoint(w / 2, M), 3.4));
            dc.DrawGeometry(edge, null, Lozenge(new WpfPoint(w / 2, h - M), 3.4));
            dc.DrawRectangle(edge, null, new Rect(w / 2 - 34, M - 1, 26, 2));
            dc.DrawRectangle(edge, null, new Rect(w / 2 + 8, M - 1, 26, 2));
            dc.DrawRectangle(edge, null, new Rect(w / 2 - 34, h - M - 1, 26, 2));
            dc.DrawRectangle(edge, null, new Rect(w / 2 + 8, h - M - 1, 26, 2));
        }

        group.Freeze();
        return group;
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
