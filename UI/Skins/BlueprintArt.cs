using System;
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
/// Paints the Blueprint skin: blue drawing paper (a violet note prints like a diazo blueline) with a fine and a
/// coarse grid, a white double border with zone ticks and centring marks, a rule under the header, and the title
/// row framed as the drawing's title block, with the note's id as the drawing number and its date on the right.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Blueprint skin.
/// </summary>
public static class BlueprintArt
{
    private const double M = 4;              // sheet edge = NoteSkin.VisualInset
    private const double Frame = 5;          // outer border line, inside the sheet edge
    private const double InnerFrame = 8.5;
    private const double HeaderBottom = 57;  // content top inset (13) + header row (44)

    public static Drawing Build(double w, double h, NoteColorTheme theme, NoteSkin skin, string tag, double pixelsPerDip,
        Rect? titleBand, DateTime? created = null)
    {
        WpfColor paper = theme.BgBrush.Color;
        WpfColor line = theme.GlowBrush.Color;
        WpfColor white = Hex("#F2F8FF");
        int seed = Seed(tag);
        var sheet = new Rect(M, M, w - 2 * M, h - 2 * M);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Paper: lighter in the middle, a few uneven patches like a real print
            var fill = new RadialGradientBrush { RadiusX = 0.8, RadiusY = 0.8 };
            fill.GradientStops.Add(new GradientStop(Blend(paper, white, 0.07), 0));
            fill.GradientStops.Add(new GradientStop(paper, 0.6));
            fill.GradientStops.Add(new GradientStop(Blend(paper, Colors.Black, 0.22), 1));
            dc.DrawRectangle(fill, null, sheet);
            dc.PushClip(new RectangleGeometry(sheet));
            for (int i = 0; i < 4; i++)
            {
                var spot = new WpfPoint(Hash(i, seed) , Hash(i, seed + 7));
                var blob = new RadialGradientBrush { Center = spot, GradientOrigin = spot, RadiusX = 0.35, RadiusY = 0.3 };
                blob.GradientStops.Add(new GradientStop(WithAlpha(i % 2 == 0 ? white : Colors.Black, 0.035), 0));
                blob.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 1));
                dc.DrawRectangle(blob, null, sheet);
            }

            // Grid: 10 px fine, 50 px coarse, both anchored to the inner frame
            double gx = M + InnerFrame, gy = M + InnerFrame;
            dc.DrawRectangle(GridBrush(10, gx, gy, line, 0.07, 0.6), null, sheet);
            dc.DrawRectangle(GridBrush(50, gx, gy, line, 0.14, 0.8), null, sheet);

            // Rule under the header, with a short thick end like a drafting line
            dc.DrawLine(Line(white, 0.55, 0.8), new WpfPoint(M + InnerFrame, HeaderBottom), new WpfPoint(w - M - InnerFrame, HeaderBottom));
            dc.DrawLine(Line(white, 0.8, 2), new WpfPoint(M + InnerFrame, HeaderBottom), new WpfPoint(M + InnerFrame + 36, HeaderBottom));
            dc.Pop();

            // Border: double frame, zone ticks between the lines, centring marks on each side
            var outer = new Rect(M + Frame, M + Frame, w - 2 * (M + Frame), h - 2 * (M + Frame));
            var inner = new Rect(M + InnerFrame, M + InnerFrame, w - 2 * (M + InnerFrame), h - 2 * (M + InnerFrame));
            dc.DrawRectangle(null, Line(white, 0.85, 1.2), outer);
            dc.DrawRectangle(null, Line(white, 0.5, 0.6), inner);
            var tick = Line(white, 0.6, 0.6);
            for (double x = inner.Left + 60; x < inner.Right - 20; x += 60)
            {
                dc.DrawLine(tick, new WpfPoint(x, outer.Top), new WpfPoint(x, inner.Top));
                dc.DrawLine(tick, new WpfPoint(x, inner.Bottom), new WpfPoint(x, outer.Bottom));
            }
            for (double y = inner.Top + 60; y < inner.Bottom - 20; y += 60)
            {
                dc.DrawLine(tick, new WpfPoint(outer.Left, y), new WpfPoint(inner.Left, y));
                dc.DrawLine(tick, new WpfPoint(inner.Right, y), new WpfPoint(outer.Right, y));
            }
            var mark = Solid(white, 0.85);
            dc.DrawGeometry(mark, null, Triangle(new WpfPoint(w / 2, outer.Top), 0, 3.2));
            dc.DrawGeometry(mark, null, Triangle(new WpfPoint(w / 2, outer.Bottom), 180, 3.2));
            dc.DrawGeometry(mark, null, Triangle(new WpfPoint(outer.Left, h / 2), 270, 3.2));
            dc.DrawGeometry(mark, null, Triangle(new WpfPoint(outer.Right, h / 2), 90, 3.2));

            // The title row is the drawing's title block: the title cell, and on the right the drawing number and date
            if (titleBand is { } band && band.Height > 18 && w > 220)
            {
                var block = new Rect(inner.Left, band.Top, inner.Width, band.Height + 2);
                DrawTitleBlock(dc, block, skin, tag, created, white, paper, pixelsPerDip, skin.TitleReserveRight + 6);
            }
        }

        group.Freeze();
        return group;
    }

    private static void DrawTitleBlock(DrawingContext dc, Rect b, NoteSkin skin, string tag, DateTime? created,
        WpfColor white, WpfColor paper, double pixelsPerDip, double cellWidth)
    {
        dc.DrawRectangle(Solid(paper, 0.35), Line(white, 0.7, 0.9), b);
        double split = b.Right - cellWidth, mid = b.Top + b.Height / 2;
        var cell = Line(white, 0.5, 0.6);
        dc.DrawLine(cell, new WpfPoint(split, b.Top), new WpfPoint(split, b.Bottom));
        dc.DrawLine(cell, new WpfPoint(split, mid), new WpfPoint(b.Right, mid));

        var caption = Solid(white, 0.5);
        var value = Solid(white, 0.9);
        var font = skin.MetaFont;
        void Cell(string label, string text, double x, double y, double cw, double ch)
        {
            dc.DrawText(Text(label, font, 6.5, caption, pixelsPerDip), new WpfPoint(x + 3, y + 1));
            var t = Text(text, font, 9.5, value, pixelsPerDip);
            t.MaxTextWidth = Math.Max(10, cw - 6);
            t.MaxLineCount = 1;
            t.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(t, new WpfPoint(x + cw - 4 - Math.Min(t.Width, cw - 6), y + ch - t.Height - 0.5));
        }
        dc.DrawText(Text("TITLE", font, 6.5, caption, pixelsPerDip), new WpfPoint(b.Left + 3, b.Top + 1));
        Cell("DWG NO.", "SN-" + tag, split, b.Top, b.Right - split, mid - b.Top);
        Cell("DATE", (created ?? DateTime.Today).ToString("dd.MM.yy"), split, mid, b.Right - split, b.Bottom - mid);
    }

    private static DrawingBrush GridBrush(double step, double ox, double oy, WpfColor c, double alpha, double thickness)
    {
        var g = new GeometryGroup();
        g.Children.Add(new LineGeometry(new WpfPoint(0, 0), new WpfPoint(step, 0)));
        g.Children.Add(new LineGeometry(new WpfPoint(0, 0), new WpfPoint(0, step)));
        var brush = new DrawingBrush(new GeometryDrawing(null, new Pen(Solid(c, alpha), thickness), g))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(ox, oy, step, step),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, step, step),
            ViewboxUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>A small filled triangle pointing inward from a frame side (0 = top side, pointing down).</summary>
    private static Geometry Triangle(WpfPoint at, double angle, double s)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new WpfPoint(-s, 0), true, true);
            g.LineTo(new WpfPoint(s, 0), true, false);
            g.LineTo(new WpfPoint(0, s * 1.5), true, false);
        }
        var t = new TransformGroup();
        t.Children.Add(new RotateTransform(angle));
        t.Children.Add(new TranslateTransform(at.X, at.Y));
        geo.Transform = t;
        geo.Freeze();
        return geo;
    }
}
