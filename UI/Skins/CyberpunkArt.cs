using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SmartNotes.Core.Models;
using WpfPen = System.Windows.Media.Pen;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Cyberpunk note body: a neon HUD panel with a stepped top edge (solid label tab on the
/// left, lower edge to the right), cut corners, a side notch and detached accent pieces.
/// Coordinates must stay in sync with NoteSkin (ContentInset / HeaderTopPad) for the Cyberpunk skin.
/// </summary>
public static class CyberpunkArt
{
    private const double M = 4;          // outer margin (room for the glow)
    private const double BodyTop = 18;   // y of the lowered top edge
    private const double HeaderBottom = 63;
    private const double CutTopRight = 10;
    private const double CutBottomRight = 24;
    private const double CutBottomLeft = 10;

    public static Drawing Build(double w, double h, NoteColorTheme theme, NoteSkin skin, string tag, double pixelsPerDip)
    {
        WpfColor accent = theme.GlowBrush.Color;
        WpfColor bg = theme.BgBrush.Color;
        WpfColor header = theme.HeaderBgBrush.Color;
        var accentBrush = Solid(accent);

        double right = w - M;
        double bottom = h - M;
        double tabEnd = M + Math.Min(150, w * 0.45);
        double slope = BodyTop - M;
        bool notch = h > 300;
        double notchY = BodyTop + Math.Max(100, h * 0.30);

        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new WpfPoint(M, BodyTop), true, true);
            g.LineTo(new WpfPoint(right - CutTopRight, BodyTop), true, false);
            g.LineTo(new WpfPoint(right, BodyTop + CutTopRight), true, false);
            if (notch)
            {
                g.LineTo(new WpfPoint(right, notchY), true, false);
                g.LineTo(new WpfPoint(right - 5, notchY + 5), true, false);
                g.LineTo(new WpfPoint(right - 5, notchY + 61), true, false);
                g.LineTo(new WpfPoint(right, notchY + 66), true, false);
            }
            g.LineTo(new WpfPoint(right, bottom - CutBottomRight), true, false);
            g.LineTo(new WpfPoint(right - CutBottomRight, bottom), true, false);
            g.LineTo(new WpfPoint(M + CutBottomLeft, bottom), true, false);
            g.LineTo(new WpfPoint(M, bottom - CutBottomLeft), true, false);
        }
        body.Freeze();

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Soft neon glow around the silhouette
            dc.DrawGeometry(null, new WpfPen(Solid(accent, 0.08), 8), body);
            dc.DrawGeometry(null, new WpfPen(Solid(accent, 0.16), 4.5), body);

            // Panel fill
            var fill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            fill.GradientStops.Add(new GradientStop(Blend(bg, accent, 0.10), 0));
            fill.GradientStops.Add(new GradientStop(bg, 0.30));
            fill.GradientStops.Add(new GradientStop(Blend(bg, Colors.Black, 0.35), 1));
            dc.DrawGeometry(fill, null, body);

            dc.PushClip(body);

            // Header band
            dc.DrawRectangle(Solid(header, 0.92), null, new Rect(0, BodyTop, w, HeaderBottom - BodyTop));

            // HUD grid below the header
            var gridPen = new WpfPen(Solid(accent, 0.075), 1);
            var gridLines = new GeometryGroup();
            gridLines.Children.Add(new LineGeometry(new WpfPoint(0, 0.5), new WpfPoint(26, 0.5)));
            gridLines.Children.Add(new LineGeometry(new WpfPoint(0.5, 0), new WpfPoint(0.5, 26)));
            var grid = new DrawingBrush(new GeometryDrawing(null, gridPen, gridLines))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(M, HeaderBottom, 26, 26),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 26, 26),
                ViewboxUnits = BrushMappingMode.Absolute
            };
            dc.DrawRectangle(grid, null, new Rect(0, HeaderBottom, w, Math.Max(0, h - HeaderBottom)));

            // Scanlines
            var scan = new DrawingBrush(new GeometryDrawing(Solid(Colors.White, 0.022), null, new RectangleGeometry(new Rect(0, 0, 4, 1))))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 4, 3),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 4, 3),
                ViewboxUnits = BrushMappingMode.Absolute
            };
            dc.DrawRectangle(scan, null, new Rect(0, 0, w, h));

            // Accent bloom rising from the cut corner
            var bloom = new RadialGradientBrush
            {
                Center = new WpfPoint(1, 1),
                GradientOrigin = new WpfPoint(1, 1),
                RadiusX = 0.75,
                RadiusY = 0.55
            };
            bloom.GradientStops.Add(new GradientStop(WithAlpha(accent, 0.13), 0));
            bloom.GradientStops.Add(new GradientStop(WithAlpha(accent, 0), 1));
            dc.DrawRectangle(bloom, null, new Rect(0, 0, w, h));

            // Header rule: thick lead-in, three pips, hairline
            dc.DrawRectangle(Solid(accent, 0.45), null, new Rect(M, HeaderBottom - 0.5, w, 1));
            dc.DrawRectangle(accentBrush, null, new Rect(M, HeaderBottom - 2, 44, 3));
            for (int i = 0; i < 3; i++)
            {
                dc.DrawRectangle(Solid(accent, 1 - i * 0.3), null, new Rect(M + 50 + i * 7, HeaderBottom - 2, 3, 3));
            }

            // Left edge ruler
            double midY = HeaderBottom + (bottom - HeaderBottom) / 2;
            double[] ticks = { 7, 3, 3, 3, 7 };
            for (int i = 0; i < ticks.Length; i++)
            {
                dc.DrawRectangle(Solid(accent, ticks[i] > 3 ? 1 : 0.55), null, new Rect(M, midY - 18 + i * 9, ticks[i], 2));
            }

            // Bottom edge weights
            dc.DrawRectangle(accentBrush, null, new Rect(M + CutBottomLeft + 6, bottom - 3, 54, 3));
            dc.DrawRectangle(Solid(accent, 0.5), null, new Rect(M + CutBottomLeft + 66, bottom - 3, 14, 3));

            // Right edge weight above the cut corner
            dc.DrawRectangle(accentBrush, null, new Rect(right - 3, bottom - CutBottomRight - 46, 3, 46));

            dc.Pop();

            // Crisp outline + heavy top-right cut
            dc.DrawGeometry(null, new WpfPen(accentBrush, 1.5) { LineJoin = PenLineJoin.Miter }, body);
            dc.DrawLine(new WpfPen(accentBrush, 3), new WpfPoint(right - CutTopRight - 1, BodyTop + 0.5), new WpfPoint(right - 0.5, BodyTop + CutTopRight + 1));

            // Raised label tab (the "step" from top-left down to the top-right edge)
            var tab = new StreamGeometry();
            using (var g = tab.Open())
            {
                g.BeginFigure(new WpfPoint(M, BodyTop + 0.5), true, true);
                g.LineTo(new WpfPoint(M, M + 5), true, false);
                g.LineTo(new WpfPoint(M + 5, M), true, false);
                g.LineTo(new WpfPoint(tabEnd, M), true, false);
                g.LineTo(new WpfPoint(tabEnd + slope, BodyTop + 0.5), true, false);
            }
            tab.Freeze();
            dc.DrawGeometry(accentBrush, null, tab);

            var ink = Solid(Blend(bg, Colors.Black, 0.3));
            var label = new FormattedText($"NOTE//{tag}", CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface(skin.MetaFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 10.5, ink, pixelsPerDip);
            dc.DrawText(label, new WpfPoint(M + 9, M + (slope - label.Height) / 2));

            // Slash cut-outs at the end of the tab
            for (int i = 0; i < 3; i++)
            {
                double x = tabEnd - 12 - i * 7;
                if (x < M + 9 + label.Width + 8) break;
                dc.DrawGeometry(ink, null, Quad(x, M + 3, x + 3, M + 3, x + 3 + (slope - 6), BodyTop - 3, x + (slope - 6), BodyTop - 3));
            }

            // Detached hazard strip floating above the lowered edge
            double hx1 = tabEnd + slope + 10;
            double hx2 = Math.Min(hx1 + 92, right - CutTopRight - 30);
            if (hx2 - hx1 > 24)
            {
                var hazard = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new WpfPoint(0, 0),
                    EndPoint = new WpfPoint(5, 5),
                    SpreadMethod = GradientSpreadMethod.Repeat
                };
                hazard.GradientStops.Add(new GradientStop(accent, 0));
                hazard.GradientStops.Add(new GradientStop(accent, 0.5));
                hazard.GradientStops.Add(new GradientStop(Colors.Transparent, 0.5));
                hazard.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
                dc.DrawGeometry(hazard, null, Quad(hx1 - 7, M + 11, hx1, M + 4, hx2, M + 4, hx2 - 7, M + 11));
            }

            // Status pips, top right
            for (int i = 0; i < 3; i++)
            {
                dc.DrawRectangle(Solid(accent, 1 - i * 0.32), null, new Rect(right - CutTopRight - 6 - i * 7, M + 6, 4, 4));
            }

            // Detached bar inside the side notch
            if (notch)
            {
                dc.DrawRectangle(accentBrush, null, new Rect(right - 2.5, notchY + 12, 2.5, 42));
            }

            // Detached corner wedge in the cut bottom-right corner
            double wedge = CutBottomRight - 10;
            var corner = new StreamGeometry();
            using (var g = corner.Open())
            {
                g.BeginFigure(new WpfPoint(right, bottom - wedge), true, true);
                g.LineTo(new WpfPoint(right, bottom), true, false);
                g.LineTo(new WpfPoint(right - wedge, bottom), true, false);
            }
            corner.Freeze();
            dc.DrawGeometry(accentBrush, null, corner);
        }

        group.Freeze();
        return group;
    }

    private static Geometry Quad(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new WpfPoint(x1, y1), true, true);
            g.LineTo(new WpfPoint(x2, y2), true, false);
            g.LineTo(new WpfPoint(x3, y3), true, false);
            g.LineTo(new WpfPoint(x4, y4), true, false);
        }
        geo.Freeze();
        return geo;
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
