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
/// Paints the Terminal skin: an old CRT. A dark bezel with a soft highlight, black glass with a faint phosphor
/// bloom in the note's colour, scanlines, a dashed rule under the header with a "tty" label, and a slight
/// curvature vignette at the corners. Still art only: the block cursor in the footer is the only thing that
/// moves, and only while the note has focus.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Terminal skin.
/// </summary>
public static class TerminalArt
{
    private const double M = 4;            // bezel edge = NoteSkin.VisualInset
    private const double Bezel = 4;        // bezel width inside M
    private const double HeaderBottom = 51; // content top inset (7) + header row (44)

    public static Drawing Build(double w, double h, NoteColorTheme theme, NoteSkin skin, string tag, double pixelsPerDip)
    {
        WpfColor p = theme.GlowBrush.Color;       // phosphor
        WpfColor glass = theme.BgBrush.Color;
        WpfColor bezel = Blend(Hex("#1A1C1A"), p, 0.04);

        var outer = RoundedRect(M, M, w - M, h - M, 9);
        var screen = RoundedRect(M + Bezel, M + Bezel, w - M - Bezel, h - M - Bezel, 7);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Bezel: dark plastic with a light top edge
            var bezelFill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            bezelFill.GradientStops.Add(new GradientStop(Blend(bezel, Colors.White, 0.10), 0));
            bezelFill.GradientStops.Add(new GradientStop(bezel, 0.25));
            bezelFill.GradientStops.Add(new GradientStop(Blend(bezel, Colors.Black, 0.35), 1));
            dc.DrawGeometry(bezelFill, Line(Colors.Black, 0.6, 1), outer);

            // Glass with a phosphor bloom in the middle
            dc.DrawGeometry(Solid(glass), null, screen);
            dc.PushClip(screen);
            var bloom = new RadialGradientBrush { RadiusX = 0.75, RadiusY = 0.7 };
            bloom.GradientStops.Add(new GradientStop(WithAlpha(p, 0.10), 0));
            bloom.GradientStops.Add(new GradientStop(WithAlpha(p, 0.03), 0.6));
            bloom.GradientStops.Add(new GradientStop(WithAlpha(p, 0), 1));
            dc.DrawRectangle(bloom, null, new Rect(0, 0, w, h));

            // Scanlines: one dark line every 3 px
            var scan = new DrawingBrush(new GeometryDrawing(Solid(Colors.Black, 0.28), null, new RectangleGeometry(new Rect(0, 0, 4, 1))))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 4, 3),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 4, 3),
                ViewboxUnits = BrushMappingMode.Absolute
            };
            scan.Freeze();
            dc.DrawRectangle(scan, null, new Rect(0, 0, w, h));

            // A brighter band, as if the beam was caught mid-refresh
            int seed = Seed(tag);
            double bandY = HeaderBottom + 20 + Hash(1, seed) * Math.Max(10, h - HeaderBottom - 80);
            var band = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            band.GradientStops.Add(new GradientStop(WithAlpha(p, 0), 0));
            band.GradientStops.Add(new GradientStop(WithAlpha(p, 0.045), 0.5));
            band.GradientStops.Add(new GradientStop(WithAlpha(p, 0), 1));
            dc.DrawRectangle(band, null, new Rect(0, bandY, w, 36));

            // Header rule: a dashed line in phosphor, the tty label at its right end
            double ruleY = HeaderBottom - 1;
            var label = Text($"tty/{tag.ToLowerInvariant()}", skin.MetaFont, 10.5, Solid(p, 0.55), pixelsPerDip);
            double labelX = w - M - Bezel - 10 - label.Width;
            var dash = new Pen(Solid(p, 0.45), 1) { DashStyle = new DashStyle(new[] { 3.0, 2.0 }, 0) };
            dash.Freeze();
            dc.DrawLine(dash, new WpfPoint(M + Bezel + 8, ruleY), new WpfPoint(labelX - 8, ruleY));
            dc.DrawText(label, new WpfPoint(labelX, ruleY - label.Height / 2 - 0.5));

            // Curved glass: darker corners, a faint reflection at the top left
            var vignette = new RadialGradientBrush { RadiusX = 0.72, RadiusY = 0.72 };
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.62));
            vignette.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.55), 1));
            dc.DrawRectangle(vignette, null, new Rect(0, 0, w, h));
            var reflection = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0.6, 0.5) };
            reflection.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.05), 0));
            reflection.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0), 1));
            dc.DrawRectangle(reflection, null, new Rect(0, 0, w, h));
            dc.Pop();

            // Inner edge of the glass and a faint phosphor halo on it
            dc.DrawGeometry(null, Line(p, 0.14, 2.5), screen);
            dc.DrawGeometry(null, Line(Colors.Black, 0.8, 1), screen);

            // Power LED on the bezel, bottom right
            var led = new WpfPoint(w - M - 12, h - M - Bezel / 2);
            dc.DrawEllipse(Glow(p, 0.6), null, led, 5, 5);
            dc.DrawEllipse(Solid(Blend(p, Colors.White, 0.4)), null, led, 1.3, 1.3);
        }

        group.Freeze();
        return group;
    }
}
