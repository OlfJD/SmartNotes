using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SmartNotes.Core.Models;
using Pen = System.Windows.Media.Pen;
using FontFamily = System.Windows.Media.FontFamily;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>Small helpers shared by the painters of the newer skins (frozen brushes, colour blends, seeded noise, text).</summary>
internal static class ArtKit
{
    public static SolidColorBrush Solid(WpfColor c, double alpha = 1)
    {
        var b = new SolidColorBrush(alpha >= 1 ? c : WithAlpha(c, alpha));
        b.Freeze();
        return b;
    }

    public static Pen Line(WpfColor c, double alpha, double thickness)
    {
        var p = new Pen(Solid(c, alpha), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        p.Freeze();
        return p;
    }

    public static WpfColor WithAlpha(WpfColor c, double alpha) => NoteSkin.WithAlpha(c, alpha);

    public static WpfColor Blend(WpfColor a, WpfColor b, double t) => WpfColor.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    public static WpfColor Hex(string hex) => (WpfColor)ColorConverter.ConvertFromString(hex);

    /// <summary>Deterministic 0..1 noise: the same note always gets the same stars, fibres or bubbles.</summary>
    public static double Hash(int i, int seed)
    {
        unchecked
        {
            uint x = (uint)(i * 374761393 + seed * 668265263);
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }

    public static int Seed(string tag)
    {
        int seed = 23;
        foreach (char c in tag) seed = unchecked(seed * 31 + c);
        return seed;
    }

    public static RadialGradientBrush Glow(WpfColor c, double alpha, WpfPoint? center = null)
    {
        var b = new RadialGradientBrush();
        if (center is { } p) { b.Center = p; b.GradientOrigin = p; }
        b.GradientStops.Add(new GradientStop(WithAlpha(c, alpha), 0));
        b.GradientStops.Add(new GradientStop(WithAlpha(c, alpha * 0.35), 0.45));
        b.GradientStops.Add(new GradientStop(WithAlpha(c, 0), 1));
        b.Freeze();
        return b;
    }

    public static FormattedText Text(string text, FontFamily font, double size, Brush brush, double pixelsPerDip,
        FontWeight? weight = null) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(font, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, brush, pixelsPerDip);

    public static Geometry RoundedRect(double l, double t, double r, double b, double radius)
    {
        var geo = new RectangleGeometry(new Rect(l, t, Math.Max(0, r - l), Math.Max(0, b - t)), radius, radius);
        geo.Freeze();
        return geo;
    }
}
