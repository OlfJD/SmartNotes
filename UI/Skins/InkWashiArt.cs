using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SmartNotes.Core.Models;
using static SmartNotes.UI.Skins.ArtKit;
using Pen = System.Windows.Media.Pen;
using FontFamily = System.Windows.Media.FontFamily;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Ink &amp; Washi skin: a sheet of rice paper with soft deckled edges and fibres, a sumi brush stroke
/// under the header in the note's colour as ink, a faint ink-wash ring (ensō), and a red name seal (hanko) beside the
/// title carrying its first letter (印, "seal", while there is no title).
/// Coordinates must stay in sync with NoteSkin.ContentInset / VisualInset for the Ink &amp; Washi skin.
/// </summary>
public static class InkWashiArt
{
    private const double L = 4, T = 3, R = 6, B = 8;   // sheet edges = NoteSkin.VisualInset
    private const double HeaderBottom = 51;           // content top inset (7) + header row (44)
    private static readonly WpfColor Vermilion = Hex("#C23B22");
    private static readonly FontFamily SealFont = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Cinzel, Yu Mincho, Segoe UI");

    public static Drawing Build(double w, double h, NoteColorTheme theme, string tag, double pixelsPerDip, string? title, Rect? titleBand)
    {
        WpfColor paper = theme.BgBrush.Color;
        WpfColor ink = theme.TextPrimaryBrush.Color;
        WpfColor tint = theme.GlowBrush.Color;           // the note colour as a dark ink
        WpfColor wash = Blend(theme.PrimaryBrush.Color, paper, 0.25);
        int seed = Seed(tag);
        double right = w - R, bottom = h - B;
        var sheet = Deckled(L, T, right, bottom, seed);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Soft shadow under the sheet
            for (int i = 0; i < 3; i++)
            {
                dc.PushTransform(new TranslateTransform(0.6 + i * 0.6, 1.2 + i * 1.6));
                dc.DrawGeometry(Solid(Colors.Black, 0.07 - i * 0.018), null, sheet);
                dc.Pop();
            }

            var fill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0.3, 1) };
            fill.GradientStops.Add(new GradientStop(Blend(paper, Colors.White, 0.35), 0));
            fill.GradientStops.Add(new GradientStop(paper, 0.5));
            fill.GradientStops.Add(new GradientStop(Blend(paper, Hex("#C9BDA4"), 0.25), 1));
            dc.DrawGeometry(fill, null, sheet);
            dc.PushClip(sheet);

            // Fibres: many short pale strands, a few long ones
            int count = (int)Math.Clamp(w * h / 1100, 50, 180);
            var fibre = Line(Blend(paper, Hex("#8C7A5B"), 0.5), 0.10, 0.5);
            var fibreLight = Line(Colors.White, 0.45, 0.6);
            for (int i = 0; i < count; i++)
            {
                double x = L + Hash(i, seed + 11) * (right - L), y = T + Hash(i, seed + 13) * (bottom - T);
                double len = Hash(i, seed + 17) < 0.9 ? 4 + Hash(i, seed + 19) * 10 : 18 + Hash(i, seed + 19) * 26;
                double a = Hash(i, seed + 23) * Math.PI;
                var end = new WpfPoint(x + Math.Cos(a) * len, y + Math.Sin(a) * len);
                var c = new WpfPoint(x + Math.Cos(a + 0.6) * len * 0.5, y + Math.Sin(a + 0.6) * len * 0.5);
                var strand = new StreamGeometry();
                using (var g = strand.Open())
                {
                    g.BeginFigure(new WpfPoint(x, y), false, false);
                    g.QuadraticBezierTo(c, end, true, false);
                }
                strand.Freeze();
                dc.DrawGeometry(null, i % 3 == 0 ? fibreLight : fibre, strand);
            }

            // Ensō: a faint ink-wash ring, open at the top right, lower left of the sheet
            double r = Math.Min(w, h) * 0.34;
            var centre = new WpfPoint(L + r * 0.85, bottom - r * 0.75);
            DrawEnso(dc, centre, r, wash, seed);

            // Brush stroke under the header
            DrawBrushStroke(dc, L + 10, HeaderBottom + 1, Math.Max(L + 60, right - (right - L) * 0.24), Blend(tint, ink, 0.35), seed);

            dc.Pop();
            dc.DrawGeometry(null, Line(Hex("#8C7A5B"), 0.25, 0.6), sheet);

            // Name seal beside the title, where a seal goes next to a name
            if (titleBand is { } band && band.Height > 16 && w > 200)
            {
                double size = Math.Clamp(band.Height - 8, 22, 30);
                var at = new WpfPoint(band.Right - 14 - 18, band.Top + 8 + (band.Height - 12) / 2);
                DrawSeal(dc, at, size, SealLetter(title), paper, seed, pixelsPerDip);
            }
        }

        group.Freeze();
        return group;
    }

    /// <summary>The first letter of the title, upper-case; 印 ("seal") when there is none.</summary>
    public static string SealLetter(string? title)
    {
        foreach (var element in StringInfoElements(title ?? ""))
        {
            if (element.Length > 0 && char.IsLetterOrDigit(element, 0)) return element.ToUpper(CultureInfo.CurrentCulture);
        }
        return "印";
    }

    private static System.Collections.Generic.IEnumerable<string> StringInfoElements(string s)
    {
        var e = StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext()) yield return (string)e.Current;
    }

    /// <summary>A rectangle whose edges wander by a pixel or so, like torn rice paper.</summary>
    private static Geometry Deckled(double l, double t, double r, double b, int seed)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            double J(int i) => (Hash(i, seed + 3) - 0.5) * 1.6;
            g.BeginFigure(new WpfPoint(l, t), true, true);
            int i = 0;
            for (double x = l + 9; x < r; x += 9) g.LineTo(new WpfPoint(x, t + J(i++)), true, true);
            for (double y = t + 9; y < b; y += 9) g.LineTo(new WpfPoint(r + J(i++), y), true, true);
            for (double x = r - 9; x > l; x -= 9) g.LineTo(new WpfPoint(x, b + J(i++)), true, true);
            for (double y = b - 9; y > t; y -= 9) g.LineTo(new WpfPoint(l + J(i++), y), true, true);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>A loaded brush pulled left to right: thick and wet at the start, thinning out, dry streaks at the end.</summary>
    private static void DrawBrushStroke(DrawingContext dc, double x0, double y, double x1, WpfColor ink, int seed)
    {
        const int steps = 40;
        var top = new WpfPoint[steps + 1];
        var bot = new WpfPoint[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            double x = x0 + (x1 - x0) * t;
            double thick = (t < 0.08 ? 2.2 + t / 0.08 * 2.6 : 4.8 * Math.Pow(1 - t, 0.55) + 0.4);
            double wobble = Math.Sin(t * 5.5 + seed % 7) * 0.7 + (Hash(i, seed + 31) - 0.5) * 0.5;
            top[i] = new WpfPoint(x, y + wobble - thick);
            bot[i] = new WpfPoint(x, y + wobble + thick * 0.8 + (Hash(i, seed + 37) - 0.5) * 0.6);
        }
        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(top[0], true, true);
            for (int i = 1; i <= steps; i++) g.LineTo(top[i], true, true);
            for (int i = steps; i >= 0; i--) g.LineTo(bot[i], true, true);
        }
        shape.Freeze();
        var brush = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 0) };
        brush.GradientStops.Add(new GradientStop(WithAlpha(ink, 0.92), 0));
        brush.GradientStops.Add(new GradientStop(WithAlpha(ink, 0.8), 0.6));
        brush.GradientStops.Add(new GradientStop(WithAlpha(ink, 0.35), 1));
        brush.Freeze();
        dc.DrawGeometry(brush, null, shape);

        // dry brush: hairline streaks running out past the end
        for (int k = 0; k < 5; k++)
        {
            double off = (k - 2) * 1.1;
            double start = x0 + (x1 - x0) * (0.55 + Hash(k, seed + 41) * 0.2);
            double end = x1 + 6 + Hash(k, seed + 43) * 18;
            dc.DrawLine(Line(ink, 0.18 + Hash(k, seed + 47) * 0.2, 0.6), new WpfPoint(start, y + off), new WpfPoint(end, y + off + (Hash(k, seed + 53) - 0.5) * 1.5));
        }
    }

    /// <summary>The ensō: the ring painted in one breath, thicker where the brush starts, open where it lifts.</summary>
    private static void DrawEnso(DrawingContext dc, WpfPoint c, double r, WpfColor wash, int seed)
    {
        const int segments = 64;
        double start = -0.25 * Math.PI + (Hash(1, seed) - 0.5) * 0.4;   // brush lands at the top right
        double sweep = 1.82 * Math.PI;
        for (int i = 0; i < segments; i++)
        {
            double t0 = i / (double)segments, t1 = (i + 1) / (double)segments;
            double a0 = start + sweep * t0, a1 = start + sweep * t1;
            double thick = 2 + 7 * Math.Sin(Math.PI * Math.Pow(t0, 0.7)) + (Hash(i, seed + 61) - 0.5) * 1.5;
            var p0 = new WpfPoint(c.X + Math.Cos(a0) * r, c.Y + Math.Sin(a0) * r);
            var p1 = new WpfPoint(c.X + Math.Cos(a1) * r, c.Y + Math.Sin(a1) * r);
            dc.DrawLine(Line(wash, 0.10 * (1 - t0 * 0.6), Math.Max(0.8, thick)), p0, p1);
        }
    }

    /// <summary>The hanko: a vermilion square with a worn edge, a thin inner border and the letter left in paper.</summary>
    private static void DrawSeal(DrawingContext dc, WpfPoint c, double size, string letter, WpfColor paper, int seed, double pixelsPerDip)
    {
        dc.PushTransform(new RotateTransform(-4 + Hash(2, seed) * 3, c.X, c.Y));
        double h = size / 2;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            double J(int i) => (Hash(i, seed + 71) - 0.5) * 1.2;
            g.BeginFigure(new WpfPoint(c.X - h, c.Y - h), true, true);
            int i = 0;
            for (double x = -h + 4; x < h; x += 4) g.LineTo(new WpfPoint(c.X + x, c.Y - h + J(i++)), true, true);
            for (double y = -h + 4; y < h; y += 4) g.LineTo(new WpfPoint(c.X + h + J(i++), c.Y + y), true, true);
            for (double x = h - 4; x > -h; x -= 4) g.LineTo(new WpfPoint(c.X + x, c.Y + h + J(i++)), true, true);
            for (double y = h - 4; y > -h; y -= 4) g.LineTo(new WpfPoint(c.X - h + J(i++), c.Y + y), true, true);
        }
        geo.Freeze();
        dc.DrawGeometry(Solid(Vermilion, 0.9), null, geo);
        dc.DrawRectangle(null, Line(paper, 0.85, 1), new Rect(c.X - h + 2.6, c.Y - h + 2.6, size - 5.2, size - 5.2));

        // centred on the letter's own outline (a font's line box would put it too high)
        var glyph = Text(letter, SealFont, size * 0.62, Solid(paper, 0.95), pixelsPerDip, FontWeights.Bold).BuildGeometry(new WpfPoint(0, 0));
        var bounds = glyph.Bounds;
        if (!bounds.IsEmpty)
        {
            glyph.Transform = new TranslateTransform(c.X - bounds.Left - bounds.Width / 2, c.Y - bounds.Top - bounds.Height / 2);
            dc.DrawGeometry(Solid(paper, 0.95), null, glyph);
        }

        // where the stamp didn't take: a few specks of paper
        for (int i = 0; i < 9; i++)
        {
            double x = c.X + (Hash(i, seed + 81) - 0.5) * size * 0.9, y = c.Y + (Hash(i, seed + 83) - 0.5) * size * 0.9;
            dc.DrawEllipse(Solid(paper, 0.35 + Hash(i, seed + 87) * 0.4), null, new WpfPoint(x, y), 0.4 + Hash(i, seed + 89) * 0.7, 0.4 + Hash(i, seed + 89) * 0.5);
        }
        dc.Pop();
    }
}
