using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Draws the Great Runes of the Elden Ring skin as glowing embers into a transparent bitmap.
/// A C# port of the renderer in docs/references/great-runes-board.html (keep the two in step):
/// every rune is a few paths (rings, lines, arcs) whose points carry a weight; particles, glow,
/// streaks, flames, petals and star flares are scattered along them and added up (additive
/// blending, like canvas "lighter"), so overlapping light gets brighter instead of covering.
/// The shapes are our own drawings of the in-game runes; no game images are used.
/// </summary>
public static class RuneArt
{
    private static readonly ConcurrentDictionary<(string, int), BitmapSource> Cache = new();

    /// <summary>The rune fitted into a square of <paramref name="sizePx"/> device pixels (cached, frozen).</summary>
    public static BitmapSource Render(string runeKey, int sizePx)
    {
        sizePx = Math.Clamp(sizePx, 16, 1024);
        var def = Defs.TryGetValue(runeKey, out var d) ? d : Defs[GreatRune.DefaultKey];
        return Cache.GetOrAdd((def.Key, sizePx), _ => Draw(def, sizePx));
    }

    /// <summary>Radius of the rune's main ring for a square box of the given size (same fit as the board).</summary>
    public static double RingRadius(double box) => box * 0.29;

    // ------------------------------------------------------------------ definitions

    private sealed class RPath
    {
        public List<P> Pts = new();
        public double W = 1;          // 0 = draw no particles (core-only path)
        public double Spread = 1;
        public bool Streak, Filament, Accent, Smoke, Petals, Thick, Core;
        public Func<double, double>? Flames;   // angle (deg) -> flame strength
        public Func<double, double>? Spill;    // angle (deg) -> dust spilling inward
    }

    private readonly record struct P(double X, double Y, double Wt);

    private sealed class Def
    {
        public string Key = "";
        public Rgb Col, Hot, Glow;
        public Rgb? AccentC, SmokeC, Petal, Petal2, Wisp;
        public (double X, double Y, double S)[] Stars = Array.Empty<(double, double, double)>();
        public double Scale = 0;
        public bool Tall, Broken, Dust, Hatch;
        public Func<List<RPath>> Paths = () => new();
    }

    private readonly record struct Rgb(float R, float G, float B)
    {
        public static Rgb Hex(string hex)
        {
            int n = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Rgb(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f);
        }
    }

    private static double Band(double d, double a, double b, double soft)
    {
        d = ((d % 360) + 360) % 360;
        double mid = (a + b) / 2, half = (b - a) / 2;
        double diff = Math.Abs(((d - mid + 540) % 360) - 180);
        return diff <= half ? 1 : Math.Max(0, 1 - (diff - half) / soft);
    }

    private static double Vary(double d, double s) => 0.5 + 0.27 * Math.Sin(d * 0.085 + s) + 0.23 * Math.Sin(d * 0.21 + s * 2.3);
    private static double Wobble(double a) => 1 + 0.018 * Math.Sin(a * 2 + 1.3) + 0.012 * Math.Sin(a * 3 + 0.4);

    private static List<P> Arc(double cx, double cy, double r, double a0, double a1, Func<double, double>? mod = null, Func<double, double>? wfn = null)
    {
        int n = Math.Max(8, (int)Math.Ceiling(Math.Abs(a1 - a0) / 1.5));
        var pts = new List<P>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double d = a0 + (a1 - a0) * i / n, a = d * Math.PI / 180;
            double rr = r * (mod?.Invoke(a) ?? 1);
            pts.Add(new P(cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr, wfn?.Invoke(d) ?? 1));
        }
        return pts;
    }

    private static List<P> Line(double x0, double y0, double x1, double y1, Func<double, double>? wfnY = null)
    {
        var pts = new List<P>(61);
        for (int i = 0; i <= 60; i++)
        {
            double t = i / 60.0, y = y0 + (y1 - y0) * t;
            pts.Add(new P(x0 + (x1 - x0) * t, y, wfnY?.Invoke(y) ?? 1));
        }
        return pts;
    }

    private static List<P> Curve(P p0, (double X, double Y) p1, (double X, double Y) p2, double w)
    {
        var pts = new List<P>(19);
        for (int i = 0; i <= 18; i++)
        {
            double u = i / 18.0, v = 1 - u;
            pts.Add(new P(v * v * p0.X + 2 * v * u * p1.X + u * u * p2.X, v * v * p0.Y + 2 * v * u * p1.Y + u * u * p2.Y, w));
        }
        return pts;
    }

    /// <summary>A full circle whose weight fades once it leaves radius fadeFrom, split into its visible runs.</summary>
    private static List<List<P>> ClippedCircleRuns(double cx, double cy, double r, double fadeFrom, double fadeLen)
    {
        var pts = Arc(cx, cy, r, 0, 360);
        for (int i = 0; i < pts.Count; i++)
        {
            double dd = Math.Sqrt(pts[i].X * pts[i].X + pts[i].Y * pts[i].Y);
            pts[i] = pts[i] with { Wt = dd <= fadeFrom ? 1 : Math.Max(0, 1 - (dd - fadeFrom) / fadeLen) };
        }
        int k = 0;
        for (int i = 0; i < pts.Count; i++) if (pts[i].Wt < 0.03) { k = i; break; }
        var seq = new List<P>();
        seq.AddRange(pts.GetRange(k, pts.Count - k));
        seq.AddRange(pts.GetRange(1, k));
        var runs = new List<List<P>>();
        var cur = new List<P>();
        foreach (var q in seq)
        {
            if (q.Wt > 0.03) cur.Add(q);
            else { if (cur.Count > 1) runs.Add(cur); cur = new List<P>(); }
        }
        if (cur.Count > 1) runs.Add(cur);
        return runs;
    }

    private static readonly Dictionary<string, Def> Defs = BuildDefs();

    private static Dictionary<string, Def> BuildDefs()
    {
        var list = new List<Def>
        {
            new Def
            {
                Key = "Godrick", Col = Rgb.Hex("#e9d356"), Hot = Rgb.Hex("#fffbe0"), Glow = Rgb.Hex("#b9b23a"), AccentC = Rgb.Hex("#a9e06a"),
                Stars = new[] { (0.0, 1.0, 0.35), (-0.866, -0.5, 0.35), (0.866, -0.5, 0.35) },
                Paths = () =>
                {
                    var p = new List<RPath> { new() { Pts = Arc(0, 0, 1, 0, 360, Wobble), W = 0.6, Spread = 0.7, Streak = true, Filament = true } };
                    foreach (double deg in new[] { 30.0, 150.0, 270.0 })
                    {
                        double a = deg * Math.PI / 180;
                        foreach (var r in ClippedCircleRuns(Math.Cos(a) * 0.4, Math.Sin(a) * 0.4, 0.872, 1.0, 0.24))
                            p.Add(new RPath { Pts = r, W = 0.5, Spread = 0.6, Streak = true, Filament = true, Accent = true });
                    }
                    return p;
                }
            },
            new Def
            {
                Key = "Radahn", Col = Rgb.Hex("#f2683a"), Hot = Rgb.Hex("#ffd35a"), Glow = Rgb.Hex("#c63c1c"), Dust = true,
                Paths = () => new List<RPath>
                {
                    new() { Pts = Arc(0, 0, 1, 0, 360, Wobble, d => 0.85 + 0.9 * Band(d, -70, 95, 35)), Streak = true,
                            Flames = d => Math.Max(Band(d, -75, 15, 25), Math.Max(0.55 * Band(d, 25, 70, 15), 0.35 * Band(d, 165, 200, 15))) },
                    new() { Pts = Line(-0.5, -1.42, -0.5, 1.42), W = 0.7, Spread = 0.6, Streak = true }
                }
            },
            new Def
            {
                Key = "Rykard", Col = Rgb.Hex("#f0923a"), Hot = Rgb.Hex("#ffd9a0"), Glow = Rgb.Hex("#7f78c4"), SmokeC = Rgb.Hex("#aaa6dc"),
                Paths = () => new List<RPath>
                {
                    new() { Pts = Arc(0, 0, 1, -90, 90), Flames = d => 0.6 * Band(d, -60, 40, 20) },
                    new() { Pts = Arc(0, 0, 1, 90, 270, a => 1 + 0.09 * Math.Sin(a * 10)), Smoke = true, W = 1.3 },
                    new() { Pts = Line(-0.6, -1.25, -0.6, 1.3), W = 0.9 }
                }
            },
            new Def
            {
                Key = "Morgott", Col = Rgb.Hex("#f4a23a"), Hot = Rgb.Hex("#ffe8b0"), Glow = Rgb.Hex("#d07a1c"),
                Stars = new[] { (0.0, -1.0, 1.0), (0.0, 1.0, 1.0) },
                Paths = () => new List<RPath>
                {
                    new() { Pts = Arc(0, 0.04, 1, 0, 360, Wobble, d => 0.35 + 1.25 * Math.Max(Band(d, 252, 288, 38), Band(d, 72, 108, 38))),
                            Thick = true, Streak = true, Flames = d => 0.35 * Band(d, 150, 220, 20) + 0.3 * Band(d, -40, 30, 20) },
                    new() { Pts = Line(0, -1.55, 0, 1.3, y => 0.45 + 0.9 * Math.Max(0, Math.Max(1 - Math.Abs(y + 1) / 0.35, 1 - Math.Abs(y - 1) / 0.35))),
                            W = 0.8, Thick = true, Streak = true }
                }
            },
            new Def
            {
                Key = "Mohg", Col = Rgb.Hex("#e01a30"), Hot = Rgb.Hex("#ffb0bc"), Glow = Rgb.Hex("#9a0a1a"),
                Petal = Rgb.Hex("#ff3c50"), Petal2 = Rgb.Hex("#ffc4cc"), Wisp = Rgb.Hex("#c8142a"),
                Paths = () =>
                {
                    var p = new List<RPath>();
                    foreach (int s in new[] { 1, -1 })
                    {
                        double mid = s > 0 ? 180 : 0, seedV = s > 0 ? 1.7 : 4.1;
                        var side = Arc(0, 0, 1, s > 0 ? 105 : -75, s > 0 ? 255 : 75, Wobble, d =>
                        {
                            double off = Math.Abs(((d - mid + 540) % 360) - 180);
                            double t = Math.Min(1, off / 62), clot = t * t * (3 - 2 * t);
                            return 0.5 + 1.3 * clot * (0.75 + 0.5 * Vary(d, seedV));
                        });
                        p.Add(new RPath { Pts = side, Petals = true, Streak = true });
                        var top = side[s > 0 ? side.Count - 1 : 0];
                        var bot = side[s > 0 ? 0 : side.Count - 1];
                        p.Add(new RPath { Pts = Curve(top, (top.X * 0.75, top.Y - 0.2), (top.X * 1.45, top.Y - 0.24), 0.8), W = 0.9, Thick = true });
                        p.Add(new RPath { Pts = Curve(bot, (bot.X * 0.75, bot.Y + 0.2), (bot.X * 1.45, bot.Y + 0.24), 0.8), W = 0.9, Thick = true });
                    }
                    p.Add(new RPath { Pts = Line(0, -0.68, 0, 0.68, y => 0.25 + 1.9 * Math.Pow(Math.Max(0, Math.Cos(y / 0.68 * Math.PI / 2)), 1.5)), W = 1, Thick = true, Streak = true });
                    return p;
                }
            },
            new Def
            {
                Key = "Malenia", Col = Rgb.Hex("#f39c86"), Hot = Rgb.Hex("#ffe2a8"), Glow = Rgb.Hex("#e0604c"),
                Petal = Rgb.Hex("#e8452e"), Petal2 = Rgb.Hex("#f7a898"), Wisp = Rgb.Hex("#f0cf8e"),
                Paths = () => new List<RPath>
                {
                    new() { Pts = Arc(0, 0, 1, 0, 360, Wobble, d => 0.3 + 1.5 * Math.Max(Band(d, 200, 278, 16), Math.Max(Band(d, -24, 14, 14), Band(d, 42, 108, 16)))), Petals = true },
                    new() { Pts = Line(0.4, -1.32, 0.4, 0.64, y => y > -0.32 ? 1.6 * (y < 0.48 ? 1 : Math.Max(0, 1 - (y - 0.48) / 0.14)) : 0.12), Petals = true, W = 0.8 },
                    new() { Pts = Line(0.4, -1.32, 0.4, -0.25), Core = true, W = 0 }
                }
            },
            new Def
            {
                Key = "Unborn", Col = Rgb.Hex("#efb53e"), Hot = Rgb.Hex("#fff3c4"), Glow = Rgb.Hex("#c08a14"),
                Stars = new[] { (0.3, -0.282, 0.55) }, Scale = 1.25,
                Paths = () => new List<RPath>
                {
                    new() { Pts = Arc(0, 0.35, 0.7, 0, 360, Wobble, d => 0.2 + 1.5 * Math.Max(Band(d, 205, 268, 16), 0.9 * Band(d, 58, 90, 10))),
                            Thick = true, Spill = d => Math.Max(1.6 * Band(d, 205, 268, 16), 0.9 * Band(d, 58, 90, 10)) },
                    new() { Pts = Line(0.3, -1.15, 0.3, 0.86, y => y > 0.36 && y < 0.46 ? 0
                                : 0.5 + 0.8 * Math.Max(0, 1 - Math.Abs(y + 1.0) / 0.25) + 0.4 * Math.Max(0, 1 - Math.Abs(y + 0.28) / 0.2)),
                            W = 0.8, Streak = true }
                }
            },
            new Def
            {
                Key = "Miquella", Col = Rgb.Hex("#ecc8c0"), Hot = Rgb.Hex("#ffffff"), Glow = Rgb.Hex("#8a2a34"), Broken = true,
                Paths = () => new List<RPath>
                {
                    new() { Pts = Arc(0, 0.15, 0.95, 120, 205) },
                    new() { Pts = Arc(0, 0.15, 0.95, 70, 112), W = 0.7 }
                }
            },
            new Def
            {
                Key = "Elden", Col = Rgb.Hex("#e8902c"), Hot = Rgb.Hex("#ffd88a"), Glow = Rgb.Hex("#b8561a"), Tall = true, Hatch = true,
                Paths = () =>
                {
                    var p = new List<RPath>
                    {
                        new() { Pts = Line(0, -1.9, 0, 2.32, y => y > 2.05 ? Math.Max(0.15, 1 - (y - 2.05) / 0.3) : 1), W = 0.9, Streak = true }
                    };
                    var bar = new List<P>();
                    for (int i = 0; i <= 40; i++) { double x = -0.78 + 1.56 * i / 40; bar.Add(new P(x, -1.8 + 0.2 * Math.Pow(x / 0.78, 2), 1)); }
                    p.Add(new RPath { Pts = bar, W = 0.9, Streak = true });
                    p.Add(new RPath { Pts = Arc(0, 0, 1, 0, 360, Wobble), Streak = true });
                    foreach (double deg in new[] { -90.0, 30.0, 150.0 })
                    {
                        double a = deg * Math.PI / 180;
                        p.Add(new RPath { Pts = Arc(Math.Cos(a) * 0.34, Math.Sin(a) * 0.34 + 0.02, 0.6, 0, 360), W = 0.8 });
                    }
                    double rr = 4.1, cyB = 1.86 - rr;
                    var bow = new List<P>();
                    for (int j = 0; j <= 80; j++) { double bx = -1.95 + 3.9 * j / 80; bow.Add(new P(bx, cyB + Math.Sqrt(rr * rr - bx * bx), 1 - 0.5 * Math.Pow(Math.Abs(bx) / 1.95, 3))); }
                    p.Add(new RPath { Pts = bow, W = 0.9, Streak = true });
                    return p;
                }
            }
        };
        var dict = new Dictionary<string, Def>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in list) dict[d.Key] = d;
        return dict;
    }

    // ------------------------------------------------------------------ drawing

    private static BitmapSource Draw(Def rune, int size)
    {
        var acc = new Accum(size, size);
        var rand = new Mulberry(11u + (uint)rune.Key.Length * 7u + (uint)rune.Key[0]);

        double cx, cy, R;
        if (rune.Tall) { cx = size / 2.0; cy = size * 0.43; R = size * 0.205; }
        else if (rune.Scale > 0) { cx = size / 2.0; cy = size / 2.0 + size * 0.02; R = size * 0.29 * rune.Scale; }
        else { cx = size / 2.0; cy = size / 2.0 + (rune.Broken ? -size * 0.05 : 0); R = size * 0.29; }

        double k = R / 60;
        double km = Math.Max(0.7, k);
        var paths = rune.Paths();

        (double, double)[] Px(List<P> pts)
        {
            var o = new (double, double)[pts.Count];
            for (int i = 0; i < pts.Count; i++) o[i] = (cx + pts[i].X * R, cy + pts[i].Y * R);
            return o;
        }

        // soft glow under everything
        foreach (var p in paths)
        {
            if (p.Pts.Count == 0 || p.Core) continue;
            var pp = Px(p.Pts);
            foreach (var (gw, ga) in new[] { (26.0, 0.025), (17.0, 0.03), (10.0, 0.04), (5.0, 0.07) })
                acc.Stroke(pp, gw * k, rune.Glow, s => ga);
        }

        // crosshatch (title-screen emblem): diagonal lattice between the ring and the bow
        if (rune.Hatch)
        {
            var poly = new[] { (cx - 1.25 * R, cy + 0.15 * R), (cx + 1.25 * R, cy + 0.15 * R), (cx + 0.35 * R, cy + 1.75 * R), (cx - 0.35 * R, cy + 1.75 * R) };
            bool Inside(double x, double y) => PointInPolygon(poly, x, y);
            double lw = 0.7 * Math.Max(0.8, k);
            for (double o = -3; o <= 3; o += 0.19)
            {
                double a1 = 0.2 + rand.Next() * 0.22;
                acc.Stroke(new[] { (cx + (o - 2) * R, cy - R), (cx + (o + 2) * R, cy + 3 * R) }, lw, rune.Col, s => a1, Inside);
                acc.Stroke(new[] { (cx + (o + 2) * R, cy - R), (cx + (o - 2) * R, cy + 3 * R) }, lw, rune.Col, s => a1, Inside);
            }
        }

        // bright thin core (Malenia's streak)
        foreach (var p in paths)
        {
            if (!p.Core) continue;
            var pp = Px(p.Pts);
            acc.Stroke(pp, 6 * k, rune.Glow, s => 0.25);
            acc.Stroke(pp, 1.3 * km, rune.Hot, s => 0.75);
        }

        // embers along every path
        foreach (var p in paths)
        {
            if (p.Core || p.W == 0) continue;
            for (int i = 0; i < p.Pts.Count - 1; i++)
            {
                var a = p.Pts[i];
                var b = p.Pts[i + 1];
                double wgt = (a.Wt + b.Wt) / 2;
                if (wgt <= 0.03) continue;
                double seg = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * R;
                int n = (int)Math.Ceiling(seg * 2.2 * p.W * (p.Smoke ? 1.6 : 1) * wgt);
                double nx = -(b.Y - a.Y), ny = b.X - a.X, nl = Math.Sqrt(nx * nx + ny * ny);
                if (nl == 0) nl = 1;
                nx /= nl; ny /= nl;
                double spread = (p.Smoke ? 5.5 : 2.2) * k * p.Spread * (p.Petals ? 0.55 + 0.6 * wgt : 1) * (p.Thick ? 0.55 + 0.55 * wgt : 1);
                double petalChance = p.Petals ? Math.Min(0.7, Math.Max(0, (wgt - 0.5) * 0.6)) : 0;
                if (p.Filament) n = (int)Math.Ceiling(n * 0.55);

                for (int m = 0; m < n; m++)
                {
                    double t = rand.Next();
                    double off = (rand.Next() + rand.Next() + rand.Next() - 1.5) * spread;
                    double x = cx + (a.X + (b.X - a.X) * t) * R + nx * off;
                    double y = cy + (a.Y + (b.Y - a.Y) * t) * R + ny * off;
                    double hotness = Math.Max(0, 1 - Math.Abs(off) / spread);
                    Rgb c = p.Smoke && rand.Next() < 0.65 ? rune.SmokeC!.Value : (rand.Next() < 0.35 * hotness ? rune.Hot : rune.Col);
                    if (p.Accent && rune.AccentC.HasValue && rand.Next() < 0.18) c = rune.AccentC.Value;
                    bool isPetal = rand.Next() < petalChance;
                    if (isPetal) c = rand.Next() < 0.6 ? rune.Petal!.Value : rune.Petal2!.Value;
                    else if (p.Petals && wgt < 0.8 && rand.Next() < 0.7) c = rune.Wisp!.Value;
                    double sz = (0.35 + Math.Pow(rand.Next(), 2.2) * 1.5) * km;
                    double al = (0.25 + rand.Next() * 0.6) * (p.Petals ? 0.7 : 1);
                    if (isPetal)
                    {
                        double rot = rand.Next() * 6.28;
                        double pl = sz * (1.5 + rand.Next() * 1.3);
                        acc.Ellipse(x, y, pl, pl * 0.45, rot, c, al);
                    }
                    else acc.Disc(x, y, sz, c, al);
                }

                // dust spilling inward from the ring (Rennala)
                if (p.Spill != null)
                {
                    double smx = (a.X + b.X) / 2, smy = (a.Y + b.Y) / 2;
                    double hl = Math.Sqrt(smx * smx + (smy - 0.35) * (smy - 0.35));
                    if (hl == 0) hl = 1;
                    double sw = p.Spill(Math.Atan2(smy - 0.35, smx) * 180 / Math.PI);
                    int count = (int)Math.Round(sw * seg * 0.8);
                    for (int sp = 0; sp < count; sp++)
                    {
                        double inward = Math.Pow(rand.Next(), 1.8) * R * 0.28;
                        double sxp = cx + smx * R - smx / hl * inward + (rand.Next() - 0.5) * 3 * k;
                        double syp = cy + smy * R - (smy - 0.35) / hl * inward + (rand.Next() - 0.5) * 3 * k;
                        var cc = rand.Next() < 0.3 ? rune.Hot : rune.Col;
                        double al = (0.15 + rand.Next() * 0.4) * (1 - inward / (R * 0.3));
                        acc.Disc(sxp, syp, (0.3 + Math.Pow(rand.Next(), 3) * 0.9) * km, cc, al);
                    }
                }

                // flame tongues: short curved licks leaving the ring outward and drifting up
                if (p.Flames != null)
                {
                    double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
                    double fw = p.Flames(Math.Atan2(my, mx) * 180 / Math.PI);
                    if (fw > 0.02 && rand.Next() < 0.22 * fw)
                    {
                        double ml = Math.Sqrt(mx * mx + my * my);
                        if (ml == 0) ml = 1;
                        double ox = mx / ml, oy = my / ml;
                        double up = 0.4 + rand.Next() * 0.5;
                        double dx = ox * 0.6, dy = oy * 0.6 - up, dl = Math.Sqrt(dx * dx + dy * dy);
                        if (dl == 0) dl = 1;
                        dx /= dl; dy /= dl;
                        double fl = R * (0.09 + Math.Pow(rand.Next(), 1.5) * 0.24) * (0.6 + 0.4 * fw);
                        double sx0 = cx + mx * R + (rand.Next() - 0.5) * 2 * k, sy0 = cy + my * R + (rand.Next() - 0.5) * 2 * k;
                        double bend = (rand.Next() - 0.5) * fl * 1.1;
                        double ex = sx0 + dx * fl - dy * bend * 0.4, ey = sy0 + dy * fl + dx * bend * 0.4;
                        double qx = sx0 + dx * fl * 0.5 - dy * bend, qy = sy0 + dy * fl * 0.5 + dx * bend;
                        var curve = new (double, double)[13];
                        for (int q = 0; q <= 12; q++)
                        {
                            double u = q / 12.0, v = 1 - u;
                            curve[q] = (v * v * sx0 + 2 * v * u * qx + u * u * ex, v * v * sy0 + 2 * v * u * qy + u * u * ey);
                        }
                        double a0 = 0.5 + rand.Next() * 0.25;
                        acc.Stroke(curve, (1.6 + rand.Next() * 1.6) * km, rune.Col, s => s < 0.55 ? a0 + (0.3 - a0) * s / 0.55 : 0.3 * (1 - (s - 0.55) / 0.45));
                        double h0 = 0.5 + rand.Next() * 0.3;
                        acc.Stroke(curve, 0.7 * km, rune.Hot, s => s < 0.7 ? h0 * (1 - s / 0.7) : 0);
                    }
                }

                // bright filament streaks along the stroke
                if (p.Streak && rand.Next() < (p.Filament ? 0.6 : 0.22) * wgt)
                {
                    int j2 = Math.Min(p.Pts.Count - 1, i + 1 + (int)Math.Floor(rand.Next() * 3));
                    double sa = 0.35 + rand.Next() * 0.45;
                    var st = new (double, double)[j2 - i + 1];
                    for (int s2 = i; s2 <= j2; s2++)
                    {
                        double o2 = (rand.Next() - 0.5) * 1.6 * k;
                        st[s2 - i] = (cx + p.Pts[s2].X * R + nx * o2, cy + p.Pts[s2].Y * R + ny * o2);
                    }
                    acc.Stroke(st, 0.8 * km, rune.Hot, s => sa);
                }
            }
        }

        // four-pointed star flares where lines cross
        foreach (var (stx, sty, sts) in rune.Stars)
        {
            double sx = cx + stx * R, sy = cy + sty * R, ss = sts * R;
            acc.Halo(sx, sy, ss * 0.55, rune.Hot, rune.Col);
            foreach (var (rx, ry, rl) in new[] { (0.0, -1.0, 0.62), (0.0, 1.0, 0.62), (-1.0, 0.0, 0.42), (1.0, 0.0, 0.42) })
            {
                var ray = new[] { (sx, sy), (sx + rx * ss * rl, sy + ry * ss * rl) };
                foreach (double lw in new[] { 2.6 * Math.Max(0.6, k) * Math.Max(0.5, sts), 0.9 * Math.Max(0.6, k) })
                {
                    acc.Stroke(ray, lw, rune.Hot, s => s < 0.4 ? 0.95 * (1 - s / 0.4) : 0);
                    acc.Stroke(ray, lw, rune.Col, s => s < 0.4 ? 0.5 * s / 0.4 : 0.5 * (1 - (s - 0.4) / 0.6));
                }
            }
        }

        // ash drifting inside the ring
        if (rune.Dust)
        {
            for (int dI = 0; dI < R * 0.9; dI++)
            {
                double dr = Math.Sqrt(rand.Next()) * R * 0.9, da = rand.Next() * 6.2832;
                double al = 0.15 + rand.Next() * 0.3;
                acc.Disc(cx + Math.Cos(da) * dr, cy + Math.Sin(da) * dr, (0.3 + Math.Pow(rand.Next(), 3) * 1.1) * km, rune.Col, al);
            }
        }

        // a few sparks thrown off
        int sparks = (int)Math.Round(R * 0.5);
        for (int s = 0; s < sparks; s++)
        {
            double ang = rand.Next() * 6.2832, rr = R * (1.05 + rand.Next() * 0.45);
            double al = 0.2 + rand.Next() * 0.5;
            acc.Disc(cx + Math.Cos(ang) * rr, cy + Math.Sin(ang) * rr - rand.Next() * R * 0.3, 0.4 + rand.Next() * 0.8, rune.Hot, al);
        }

        // Miquella: the shattered rest of the ring in a pile at the bottom
        if (rune.Broken)
        {
            for (int f = 0; f < R * 6; f++)
            {
                double fx = cx + (rand.Next() - 0.5) * R * 1.5 * (0.4 + rand.Next() * 0.6);
                double fy = cy + R * (1.0 + Math.Pow(rand.Next(), 2) * 0.16) - rand.Next() * R * 0.1;
                var cc = rand.Next() < 0.5 ? rune.Col : rune.Glow;
                double al = 0.3 + rand.Next() * 0.6;
                double rot = rand.Next() * 6.28;
                double fl = (0.6 + rand.Next() * 2.6) * k;
                acc.Ellipse(fx, fy, fl, 0.45, rot, cc, al);
            }
            for (int sh = 0; sh < 7; sh++)
            {
                double sx = cx + (rand.Next() - 0.5) * R, sy = cy + R * 1.04;
                double sa = -Math.PI / 2 + (rand.Next() - 0.5) * 1.6, sl = R * (0.12 + rand.Next() * 0.22);
                acc.Stroke(new[] { (sx, sy), (sx + Math.Cos(sa) * sl, sy + Math.Sin(sa) * sl) }, 1, rune.Hot, s => 0.55);
            }
        }

        return acc.ToBitmap();
    }

    private static bool PointInPolygon((double X, double Y)[] poly, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > y) != (poly[j].Y > y) &&
                x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>mulberry32, the same generator the board uses.</summary>
    private sealed class Mulberry
    {
        private uint _s;
        public Mulberry(uint seed) { _s = seed; }
        public double Next()
        {
            unchecked
            {
                _s += 0x6D2B79F5;
                uint t = (_s ^ (_s >> 15)) * (1 | _s);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    /// <summary>Float RGBA buffer with additive blending (premultiplied, like canvas "lighter").</summary>
    private sealed class Accum
    {
        private readonly int _w, _h;
        private readonly float[] _r, _g, _b, _a, _mask;

        public Accum(int w, int h)
        {
            _w = w; _h = h;
            _r = new float[w * h]; _g = new float[w * h]; _b = new float[w * h]; _a = new float[w * h]; _mask = new float[w * h];
        }

        private void Add(int i, Rgb c, double alpha)
        {
            float al = (float)alpha;
            _r[i] += c.R * al; _g[i] += c.G * al; _b[i] += c.B * al; _a[i] += al;
        }

        /// <summary>Filled anti-aliased disc. Tiny discs keep their area so faint dust stays faint.</summary>
        public void Disc(double x, double y, double rad, Rgb c, double alpha)
        {
            if (alpha <= 0) return;
            if (rad < 0.7)
            {
                // spread the disc's area over the four nearest pixels
                double area = Math.PI * rad * rad;
                double fx = x - 0.5, fy = y - 0.5;
                int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                double tx = fx - x0, ty = fy - y0;
                Splat(x0, y0, (1 - tx) * (1 - ty) * area, c, alpha);
                Splat(x0 + 1, y0, tx * (1 - ty) * area, c, alpha);
                Splat(x0, y0 + 1, (1 - tx) * ty * area, c, alpha);
                Splat(x0 + 1, y0 + 1, tx * ty * area, c, alpha);
                return;
            }
            int xa = Math.Max(0, (int)Math.Floor(x - rad - 1)), xb = Math.Min(_w - 1, (int)Math.Ceiling(x + rad + 1));
            int ya = Math.Max(0, (int)Math.Floor(y - rad - 1)), yb = Math.Min(_h - 1, (int)Math.Ceiling(y + rad + 1));
            for (int py = ya; py <= yb; py++)
            {
                double dy = py + 0.5 - y;
                for (int px = xa; px <= xb; px++)
                {
                    double dx = px + 0.5 - x;
                    double cov = rad + 0.5 - Math.Sqrt(dx * dx + dy * dy);
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    Add(py * _w + px, c, alpha * cov);
                }
            }
        }

        private void Splat(int px, int py, double cov, Rgb c, double alpha)
        {
            if (px < 0 || py < 0 || px >= _w || py >= _h || cov <= 0) return;
            Add(py * _w + px, c, alpha * cov);
        }

        /// <summary>Filled anti-aliased ellipse rotated by <paramref name="rot"/> radians.</summary>
        public void Ellipse(double x, double y, double rx, double ry, double rot, Rgb c, double alpha)
        {
            if (alpha <= 0) return;
            if (rx < 0.7 && ry < 0.7) { Disc(x, y, Math.Sqrt(rx * ry), c, alpha); return; }
            double ext = Math.Max(rx, ry) + 1;
            double cs = Math.Cos(rot), sn = Math.Sin(rot);
            double minR = Math.Max(0.5, Math.Min(rx, ry));
            int xa = Math.Max(0, (int)Math.Floor(x - ext)), xb = Math.Min(_w - 1, (int)Math.Ceiling(x + ext));
            int ya = Math.Max(0, (int)Math.Floor(y - ext)), yb = Math.Min(_h - 1, (int)Math.Ceiling(y + ext));
            double thin = Math.Min(1, ry / 0.5); // very thin slivers get fainter instead of vanishing
            for (int py = ya; py <= yb; py++)
            {
                for (int px = xa; px <= xb; px++)
                {
                    double dx = px + 0.5 - x, dy = py + 0.5 - y;
                    double u = (dx * cs + dy * sn) / rx, v = (-dx * sn + dy * cs) / Math.Max(0.5, ry);
                    double cov = (1 - Math.Sqrt(u * u + v * v)) * minR + 0.5;
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    Add(py * _w + px, c, alpha * cov * thin);
                }
            }
        }

        /// <summary>
        /// Strokes a polyline as one shape (overlapping segments do not double up). The alpha may vary
        /// along the line: <paramref name="alphaAt"/> gets 0 at the start and 1 at the end.
        /// </summary>
        public void Stroke((double X, double Y)[] pts, double width, Rgb c, Func<double, double> alphaAt, Func<double, double, bool>? clip = null)
        {
            if (pts.Length < 2 || width <= 0) return;
            double half = width / 2;
            double thinScale = width < 1 ? width : 1;
            double hw = Math.Max(half, 0.5);

            double total = 0;
            var cum = new double[pts.Length];
            for (int i = 1; i < pts.Length; i++)
            {
                double dx = pts[i].X - pts[i - 1].X, dy = pts[i].Y - pts[i - 1].Y;
                total += Math.Sqrt(dx * dx + dy * dy);
                cum[i] = total;
            }
            if (total <= 0) return;

            int bxa = _w, bxb = -1, bya = _h, byb = -1;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var (ax, ay) = pts[i];
                var (bx, by) = pts[i + 1];
                int xa = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - hw - 1)), xb = Math.Min(_w - 1, (int)Math.Ceiling(Math.Max(ax, bx) + hw + 1));
                int ya = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - hw - 1)), yb = Math.Min(_h - 1, (int)Math.Ceiling(Math.Max(ay, by) + hw + 1));
                if (xa > xb || ya > yb) continue;
                bxa = Math.Min(bxa, xa); bxb = Math.Max(bxb, xb); bya = Math.Min(bya, ya); byb = Math.Max(byb, yb);
                double sx = bx - ax, sy = by - ay, len2 = sx * sx + sy * sy;
                double segLen = Math.Sqrt(len2);
                for (int py = ya; py <= yb; py++)
                {
                    for (int px = xa; px <= xb; px++)
                    {
                        double qx = px + 0.5, qy = py + 0.5;
                        double t = len2 > 0 ? Math.Clamp(((qx - ax) * sx + (qy - ay) * sy) / len2, 0, 1) : 0;
                        double ddx = qx - (ax + sx * t), ddy = qy - (ay + sy * t);
                        double d = Math.Sqrt(ddx * ddx + ddy * ddy);
                        double cov = width < 1 ? thinScale * Math.Clamp(1 - d, 0, 1) : Math.Clamp(half + 0.5 - d, 0, 1);
                        if (cov <= 0) continue;
                        if (clip != null && !clip(qx, qy)) continue;
                        double s = (cum[i] + t * segLen) / total;
                        double v = cov * Math.Max(0, alphaAt(s));
                        int idx = py * _w + px;
                        if (v > _mask[idx]) _mask[idx] = (float)v;
                    }
                }
            }

            for (int py = bya; py <= byb; py++)
            {
                for (int px = bxa; px <= bxb; px++)
                {
                    int idx = py * _w + px;
                    float m = _mask[idx];
                    if (m <= 0) continue;
                    Add(idx, c, m);
                    _mask[idx] = 0;
                }
            }
        }

        /// <summary>Radial glow: hot centre fading through the main colour to nothing.</summary>
        public void Halo(double x, double y, double rad, Rgb hot, Rgb col)
        {
            int xa = Math.Max(0, (int)Math.Floor(x - rad)), xb = Math.Min(_w - 1, (int)Math.Ceiling(x + rad));
            int ya = Math.Max(0, (int)Math.Floor(y - rad)), yb = Math.Min(_h - 1, (int)Math.Ceiling(y + rad));
            for (int py = ya; py <= yb; py++)
            {
                for (int px = xa; px <= xb; px++)
                {
                    double dx = px + 0.5 - x, dy = py + 0.5 - y;
                    double s = Math.Sqrt(dx * dx + dy * dy) / rad;
                    if (s >= 1) continue;
                    int idx = py * _w + px;
                    if (s < 0.35)
                    {
                        double u = s / 0.35;
                        Add(idx, hot, 0.55 * (1 - u));
                        Add(idx, col, 0.25 * u);
                    }
                    else Add(idx, col, 0.25 * (1 - (s - 0.35) / 0.65));
                }
            }
        }

        public BitmapSource ToBitmap()
        {
            var pixels = new byte[_w * _h * 4];
            for (int i = 0; i < _w * _h; i++)
            {
                float a = Math.Min(1f, _a[i]);
                float r = Math.Min(a, Math.Min(1f, _r[i]));
                float g = Math.Min(a, Math.Min(1f, _g[i]));
                float b = Math.Min(a, Math.Min(1f, _b[i]));
                int o = i * 4;
                pixels[o] = (byte)(b * 255 + 0.5f);
                pixels[o + 1] = (byte)(g * 255 + 0.5f);
                pixels[o + 2] = (byte)(r * 255 + 0.5f);
                pixels[o + 3] = (byte)(a * 255 + 0.5f);
            }
            var bmp = BitmapSource.Create(_w, _h, 96, 96, PixelFormats.Pbgra32, null, pixels, _w * 4);
            bmp.Freeze();
            return bmp;
        }
    }
}
