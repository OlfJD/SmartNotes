using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartNotes.Core.Models;
using static SmartNotes.UI.Skins.ArtKit;
using Pen = System.Windows.Media.Pen;
using FontFamily = System.Windows.Media.FontFamily;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Deep Sea skin: the abyss as a dark teal gradient with faint light falling from above, drifting
/// marine snow and a few bubbles, a soft rim in the note's bioluminescent colour, and one glowing creature per
/// note in the lower right: a jellyfish, an anglerfish's lure or a nautilus. The creature is the one chosen in the
/// colour popup (NoteItem emblem for this skin), otherwise picked from the note's id.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Deep Sea skin.
/// </summary>
public static class DeepSeaArt
{
    private const double M = 5;              // = NoteSkin.VisualInset
    private const double Radius = 14;
    private const double HeaderBottom = 55;  // content top inset (11) + header row (44)

    public static readonly IReadOnlyList<string> CreatureKeys = new[] { "Jellyfish", "Anglerfish", "Nautilus" };

    private static readonly Dictionary<string, string> NamesDe = new()
    {
        ["Jellyfish"] = "Qualle",
        ["Anglerfish"] = "Anglerfisch",
        ["Nautilus"] = "Nautilus",
    };

    private static readonly Dictionary<string, string> NamesEn = new()
    {
        ["Jellyfish"] = "Jellyfish",
        ["Anglerfish"] = "Anglerfish",
        ["Nautilus"] = "Nautilus",
    };

    public static string DisplayName(string key, string language) =>
        (language.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? NamesDe : NamesEn).TryGetValue(key, out var name) ? name : key;

    /// <summary>The chosen creature, or one picked from the note's id.</summary>
    public static string Resolve(string? key, string tag)
    {
        foreach (var k in CreatureKeys) if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return k;
        return CreatureKeys[(int)(Hash(6, Seed(tag)) * CreatureKeys.Count) % CreatureKeys.Count];
    }

    /// <summary>The creature's box (top left, size), or null when the note is too small for it.</summary>
    public static (WpfPoint Origin, double Box)? CreaturePlacement(double w, double h, double bottomReserve)
    {
        if (w <= 200) return null;
        double floor = h - Math.Max(bottomReserve, M + 8);
        double box = Math.Min(Math.Min(w, h) * 0.46, 150);
        box = Math.Min(box, (floor - HeaderBottom) * 0.85);
        if (box < 70) return null;
        return (new WpfPoint(w - M - 10 - box, floor - 6 - box), box);
    }

    /// <summary>The brightest point of a creature (bell, lure, shell centre): where the living theme's glow breathes.</summary>
    public static WpfPoint GlowPoint(string creature, WpfPoint origin, double box) => creature switch
    {
        "Anglerfish" => new WpfPoint(origin.X + box * 0.14, origin.Y + box * 0.21),
        "Nautilus" => new WpfPoint(origin.X + box * 0.55, origin.Y + box * 0.52),
        _ => new WpfPoint(origin.X + box * 0.55, origin.Y + box * 0.2),
    };

    public static Drawing Build(double w, double h, NoteColorTheme theme, string tag, double bottomReserve, string? creature)
    {
        WpfColor glow = theme.GlowBrush.Color;
        WpfColor bg = theme.BgBrush.Color;
        WpfColor top = theme.HeaderBgBrush.Color;
        WpfColor rim = theme.BorderBrush.Color;
        int seed = Seed(tag);
        var panel = RoundedRect(M, M, w - M, h - M, Radius);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawGeometry(null, Line(glow, 0.06, 7), panel);

            // Water: lit from far above, black at the bottom
            var water = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            water.GradientStops.Add(new GradientStop(Blend(top, Hex("#2A7F8C"), 0.28), 0));
            water.GradientStops.Add(new GradientStop(top, 0.18));
            water.GradientStops.Add(new GradientStop(bg, 0.5));
            water.GradientStops.Add(new GradientStop(Blend(bg, Colors.Black, 0.7), 1));
            dc.DrawGeometry(water, null, panel);
            dc.PushClip(panel);

            // Light falling from above in slanted beams
            for (int i = 0; i < 4; i++)
            {
                double x = w * (0.1 + Hash(i, seed + 5) * 0.8), spread = 18 + Hash(i, seed + 9) * 34, lean = w * 0.18;
                var beam = new StreamGeometry();
                using (var g = beam.Open())
                {
                    g.BeginFigure(new WpfPoint(x - 6, 0), true, true);
                    g.LineTo(new WpfPoint(x + 6, 0), true, false);
                    g.LineTo(new WpfPoint(x + lean + spread, h * 0.85), true, false);
                    g.LineTo(new WpfPoint(x + lean - spread, h * 0.85), true, false);
                }
                beam.Freeze();
                var light = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
                light.GradientStops.Add(new GradientStop(WithAlpha(Hex("#9FE8F0"), 0.07), 0));
                light.GradientStops.Add(new GradientStop(WithAlpha(Hex("#9FE8F0"), 0), 1));
                light.Freeze();
                dc.DrawGeometry(light, null, beam);
            }

            // A wavering line where the header ends, like a thermocline
            var wave = new StreamGeometry();
            using (var g = wave.Open())
            {
                g.BeginFigure(new WpfPoint(M, HeaderBottom), false, false);
                for (double x = M; x <= w - M; x += 6)
                    g.LineTo(new WpfPoint(x, HeaderBottom + Math.Sin(x / 22 + seed % 5) * 1.3), true, true);
            }
            wave.Freeze();
            dc.DrawGeometry(null, Line(rim, 0.35, 0.8), wave);

            // Marine snow
            int count = (int)Math.Clamp(w * h / 800, 40, 170);
            for (int i = 0; i < count; i++)
            {
                double x = M + Hash(i, seed + 101) * (w - 2 * M), y = M + Hash(i, seed + 211) * (h - 2 * M);
                double s = 0.35 + Math.Pow(Hash(i, seed + 307), 2.5) * 1.1;
                dc.DrawEllipse(Solid(Hex("#CDEFF2"), 0.12 + Hash(i, seed + 401) * 0.25), null, new WpfPoint(x, y), s, s);
            }

            // A few bubbles rising along one side
            double bx = Hash(9, seed) < 0.5 ? M + 14 : w - M - 14;
            for (int i = 0; i < 6; i++)
            {
                double y = HeaderBottom + 20 + i * (h - HeaderBottom - 60) / 6 + Hash(i, seed + 501) * 10;
                double r = 1.4 + Hash(i, seed + 503) * 2.4;
                var c = new WpfPoint(bx + (Hash(i, seed + 507) - 0.5) * 10, y);
                dc.DrawEllipse(Solid(glow, 0.04), Line(Hex("#CDEFF2"), 0.35, 0.6), c, r, r);
                dc.DrawEllipse(Solid(Colors.White, 0.5), null, new WpfPoint(c.X - r * 0.35, c.Y - r * 0.35), r * 0.25, r * 0.25);
            }

            // The creature, lower right, clear of the footer / input row
            if (CreaturePlacement(w, h, bottomReserve) is { } placement)
            {
                var (origin, box) = placement;
                DrawCreature(dc, Resolve(creature, tag), origin, box, glow, bg, seed);
            }

            // Darkness closing in at the edges
            var vignette = new RadialGradientBrush { RadiusX = 0.78, RadiusY = 0.78 };
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.6));
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.45), 1));
            dc.DrawRectangle(vignette, null, new Rect(0, 0, w, h));
            dc.Pop();

            dc.DrawGeometry(null, Line(rim, 0.55, 1), panel);
        }

        group.Freeze();
        return group;
    }

    /// <summary>Picker tile: the creature (or, for an empty key, a few glowing specks) on a small square of deep water.</summary>
    public static ImageSource RenderTile(string? key, int px, WpfColor glow)
    {
        const double S = 32;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(px / S, px / S));
            var water = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            water.GradientStops.Add(new GradientStop(Hex("#0D3442"), 0));
            water.GradientStops.Add(new GradientStop(Hex("#020A10"), 1));
            dc.DrawRectangle(water, null, new Rect(0, 0, S, S));
            string? creature = null;
            foreach (var k in CreatureKeys) if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) creature = k;
            if (creature == null)
            {
                foreach (var (x, y, r) in new[] { (11.0, 12.0, 2.2), (21.0, 19.0, 1.6), (14.0, 23.0, 1.2), (23.0, 9.0, 1.0) })
                {
                    dc.DrawEllipse(Glow(glow, 0.7), null, new WpfPoint(x, y), r * 3.5, r * 3.5);
                    dc.DrawEllipse(Solid(Blend(glow, Colors.White, 0.6)), null, new WpfPoint(x, y), r * 0.6, r * 0.6);
                }
            }
            else
            {
                DrawCreature(dc, creature, new WpfPoint(2, 2), S - 4, glow, Hex("#04161F"), 7);
            }
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    private static void DrawCreature(DrawingContext dc, string creature, WpfPoint o, double box, WpfColor glow, WpfColor bg, int seed)
    {
        switch (creature)
        {
            case "Anglerfish": DrawAnglerfish(dc, o, box, glow, bg); break;
            case "Nautilus": DrawNautilus(dc, o, box, glow); break;
            default: DrawJellyfish(dc, o, box, glow, seed); break;
        }
    }

    /// <summary>A bell with a scalloped rim and a glowing core, oral arms and long trailing tentacles.</summary>
    private static void DrawJellyfish(DrawingContext dc, WpfPoint o, double box, WpfColor glow, int seed)
    {
        double cx = o.X + box * 0.55, top = o.Y + box * 0.06;
        double bw = box * 0.52, bh = box * 0.34;
        var white = Blend(glow, Colors.White, 0.6);
        dc.DrawEllipse(Glow(glow, 0.22), null, new WpfPoint(cx, top + bh * 0.6), bw * 0.95, bh * 1.6);

        // trailing tentacles first, behind the bell
        for (int i = 0; i < 7; i++)
        {
            double sx = cx - bw / 2 + 4 + i * (bw - 8) / 6;
            double len = box * (0.5 + Hash(i, seed + 3) * 0.35);
            var path = new StreamGeometry();
            using (var g = path.Open())
            {
                g.BeginFigure(new WpfPoint(sx, top + bh), false, false);
                double sway = (Hash(i, seed + 5) - 0.5) * 12 - 4;
                g.BezierTo(new WpfPoint(sx + 7 + sway, top + bh + len * 0.33), new WpfPoint(sx - 7 + sway, top + bh + len * 0.66), new WpfPoint(sx + sway * 1.4, top + bh + len), true, false);
            }
            path.Freeze();
            var fade = new LinearGradientBrush(WithAlpha(white, 0.55), WithAlpha(glow, 0), new WpfPoint(0, top + bh), new WpfPoint(0, top + bh + len)) { MappingMode = BrushMappingMode.Absolute };
            fade.Freeze();
            dc.DrawGeometry(null, new Pen(fade, 0.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, path);
        }
        // oral arms: three frilly ribbons
        for (int i = 0; i < 3; i++)
        {
            double sx = cx - bw * 0.14 + i * bw * 0.14, len = box * 0.36;
            var arm = new StreamGeometry();
            using (var g = arm.Open())
            {
                g.BeginFigure(new WpfPoint(sx, top + bh - 2), false, false);
                for (int k = 1; k <= 8; k++)
                    g.LineTo(new WpfPoint(sx + Math.Sin(k * 1.3 + i) * 3.2, top + bh - 2 + len * k / 8), true, true);
            }
            arm.Freeze();
            var fade = new LinearGradientBrush(WithAlpha(glow, 0.6), WithAlpha(glow, 0.05), new WpfPoint(0, top + bh), new WpfPoint(0, top + bh + len)) { MappingMode = BrushMappingMode.Absolute };
            fade.Freeze();
            dc.DrawGeometry(null, new Pen(fade, 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, arm);
        }

        // the bell: a dome closed by a scalloped rim
        var bell = new StreamGeometry();
        using (var g = bell.Open())
        {
            g.BeginFigure(new WpfPoint(cx - bw / 2, top + bh), true, true);
            g.BezierTo(new WpfPoint(cx - bw / 2, top - bh * 0.15), new WpfPoint(cx + bw / 2, top - bh * 0.15), new WpfPoint(cx + bw / 2, top + bh), true, true);
            int scallops = 6;
            for (int k = scallops - 1; k >= 0; k--)
            {
                double x = cx - bw / 2 + k * bw / scallops;
                g.ArcTo(new WpfPoint(x, top + bh), new WpfSize(bw / scallops / 2, 2.6), 0, false, SweepDirection.Clockwise, true, true);
            }
        }
        bell.Freeze();
        var bellFill = new RadialGradientBrush { Center = new WpfPoint(0.5, 0.35), GradientOrigin = new WpfPoint(0.5, 0.3), RadiusX = 0.6, RadiusY = 0.7 };
        bellFill.GradientStops.Add(new GradientStop(WithAlpha(white, 0.55), 0));
        bellFill.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.22), 0.6));
        bellFill.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.08), 1));
        bellFill.Freeze();
        dc.DrawGeometry(bellFill, Line(white, 0.7, 0.9), bell);
        // gonads: four soft rings inside the bell
        for (int k = 0; k < 4; k++)
        {
            double a = Math.PI * (0.2 + k * 0.2);
            var p = new WpfPoint(cx + Math.Cos(Math.PI - a) * bw * 0.22, top + bh * 0.55 - Math.Sin(a) * bh * 0.18);
            dc.DrawEllipse(null, Line(white, 0.45, 0.7), p, bw * 0.07, bh * 0.12);
        }
    }

    /// <summary>Barely visible in the dark: the fish; bright: the lure on its stalk, which is what the eye catches.</summary>
    private static void DrawAnglerfish(DrawingContext dc, WpfPoint o, double box, WpfColor glow, WpfColor bg)
    {
        double s = box / 100.0;
        WpfPoint P(double x, double y) => new(o.X + x * s, o.Y + y * s);
        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(P(22, 58), true, true);                      // tip of the lower jaw
            g.BezierTo(P(30, 40), P(52, 30), P(72, 38), true, true);   // forehead
            g.BezierTo(P(86, 44), P(92, 56), P(96, 52), true, true);   // back to the tail
            g.LineTo(P(99, 62), true, true);
            g.LineTo(P(94, 70), true, true);
            g.BezierTo(P(86, 78), P(62, 88), P(40, 78), true, true);   // belly
            g.BezierTo(P(30, 74), P(24, 68), P(22, 58), true, true);
        }
        body.Freeze();
        var skin = Blend(bg, glow, 0.16);
        dc.DrawGeometry(Solid(skin, 0.55), Line(glow, 0.18, 0.8), body);
        // open mouth and needle teeth
        var mouth = new StreamGeometry();
        using (var g = mouth.Open())
        {
            g.BeginFigure(P(24, 57), false, false);
            g.BezierTo(P(34, 52), P(40, 56), P(44, 60), true, false);
        }
        mouth.Freeze();
        dc.DrawGeometry(null, Line(glow, 0.3, 0.8), mouth);
        var tooth = Solid(Blend(glow, Colors.White, 0.7), 0.4);
        for (int i = 0; i < 6; i++)
        {
            double x = 26 + i * 3.2;
            var t = new StreamGeometry();
            using (var g = t.Open())
            {
                g.BeginFigure(P(x, 56.5 - i * 0.3), true, true);
                g.LineTo(P(x + 1.2, 56.5 - i * 0.3), true, false);
                g.LineTo(P(x + 0.5, 61), true, false);
            }
            t.Freeze();
            dc.DrawGeometry(tooth, null, t);
        }
        // a small pale eye
        dc.DrawEllipse(null, Line(glow, 0.45, 0.8), P(50, 44), 2.4 * s, 2.4 * s);
        dc.DrawEllipse(Solid(glow, 0.6), null, P(50, 44), 0.8 * s, 0.8 * s);
        // the stalk and the lure
        var stalk = new StreamGeometry();
        using (var g = stalk.Open())
        {
            g.BeginFigure(P(48, 33), false, false);
            g.BezierTo(P(44, 10), P(22, 4), P(14, 18), true, false);
        }
        stalk.Freeze();
        dc.DrawGeometry(null, Line(glow, 0.45, 1), stalk);
        var lure = P(14, 21);
        dc.DrawEllipse(Glow(glow, 0.55), null, lure, 18 * s, 18 * s);
        dc.DrawEllipse(Glow(Blend(glow, Colors.White, 0.4), 0.9), null, lure, 7 * s, 7 * s);
        dc.DrawEllipse(Solid(Blend(glow, Colors.White, 0.75)), null, lure, 2.6 * s, 2.6 * s);
    }

    /// <summary>A coiled shell drawn as a logarithmic spiral with its chamber walls, glowing at the rim.</summary>
    private static void DrawNautilus(DrawingContext dc, WpfPoint o, double box, WpfColor glow)
    {
        var c = new WpfPoint(o.X + box * 0.55, o.Y + box * 0.52);
        double a = box * 0.018, b = 0.19;                  // r = a·e^(bθ)
        double maxT = Math.Log(box * 0.42 / a) / b;
        WpfPoint At(double t, double scale = 1) => new(c.X + Math.Cos(t) * a * Math.Exp(b * t) * scale, c.Y + Math.Sin(t) * a * Math.Exp(b * t) * scale);
        var white = Blend(glow, Colors.White, 0.55);

        dc.DrawEllipse(Glow(glow, 0.18), null, c, box * 0.5, box * 0.45);

        // shell body: the outermost whorl filled softly
        var shell = new StreamGeometry();
        using (var g = shell.Open())
        {
            g.BeginFigure(At(maxT - 2 * Math.PI), true, true);
            for (double t = maxT - 2 * Math.PI; t <= maxT; t += 0.08) g.LineTo(At(t), true, true);
            for (double t = maxT; t >= maxT - 2 * Math.PI; t -= 0.08) g.LineTo(At(t - 2 * Math.PI), true, true);
        }
        shell.Freeze();
        var fill = new RadialGradientBrush();
        fill.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.05), 0));
        fill.GradientStops.Add(new GradientStop(WithAlpha(glow, 0.2), 1));
        fill.Freeze();
        dc.DrawGeometry(fill, null, shell);

        // tiger stripes on the outer whorl
        for (double t = maxT - 2 * Math.PI + 0.5; t < maxT - 0.6; t += 0.45)
        {
            dc.DrawLine(Line(glow, 0.22, 1.6), At(t, 0.98), At(t, 0.86));
        }

        // the spiral itself and the chamber walls (septa)
        var spiral = new StreamGeometry();
        using (var g = spiral.Open())
        {
            g.BeginFigure(At(0), false, false);
            for (double t = 0.08; t <= maxT; t += 0.08) g.LineTo(At(t), true, true);
        }
        spiral.Freeze();
        dc.DrawGeometry(null, Line(glow, 0.25, 3), spiral);
        dc.DrawGeometry(null, Line(white, 0.8, 0.9), spiral);
        for (double t = 2 * Math.PI + 0.4; t < maxT - 0.3; t += 0.55)
        {
            var outer = At(t);
            var inner = At(t - 2 * Math.PI);
            var mid = new WpfPoint((outer.X + inner.X) / 2 + (c.X - outer.X) * 0.08, (outer.Y + inner.Y) / 2 + (c.Y - outer.Y) * 0.08);
            var septum = new StreamGeometry();
            using (var g = septum.Open())
            {
                g.BeginFigure(inner, false, false);
                g.QuadraticBezierTo(mid, outer, true, false);
            }
            septum.Freeze();
            dc.DrawGeometry(null, Line(white, 0.35, 0.6), septum);
        }

        // closing wall of the opening and a few short tentacles
        var lip0 = At(maxT);
        var lip1 = At(maxT - 2 * Math.PI);
        dc.DrawLine(Line(white, 0.7, 1), lip0, lip1);
        for (int i = 0; i < 5; i++)
        {
            double f = (i + 0.5) / 5;
            var start = new WpfPoint(lip0.X + (lip1.X - lip0.X) * f, lip0.Y + (lip1.Y - lip0.Y) * f);
            var tip = new WpfPoint(start.X - box * (0.08 + 0.04 * Math.Sin(i * 2.1)), start.Y + box * (0.06 + 0.02 * i));
            dc.DrawLine(Line(glow, 0.4, 1), start, tip);
        }
    }
}
