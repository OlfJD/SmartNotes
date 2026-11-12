using System;
using System.Windows;
using System.Windows.Media;
using SmartNotes.Core.Models;
using WpfPen = System.Windows.Media.Pen;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Paints the Paper skin: a sticky note in the note's colour with a darker glue strip behind the
/// header, a soft drop shadow and a folded bottom-right corner.
/// Coordinates must stay in sync with NoteSkin.ContentInset for the Paper skin.
/// </summary>
public static class PaperStickyArt
{
    private const double Left = 4, Top = 3, RightPad = 7, BottomPad = 10;
    private const double Fold = 26;
    private const double StripBottom = 47; // content top inset (3) + header row (44)

    public static Drawing Build(double w, double h, NoteColorTheme theme)
    {
        WpfColor paper = theme.BgBrush.Color;
        WpfColor strip = theme.HeaderBgBrush.Color;
        WpfColor ink = theme.TextPrimaryBrush.Color;
        double right = w - RightPad, bottom = h - BottomPad;

        var sheet = new StreamGeometry();
        using (var g = sheet.Open())
        {
            g.BeginFigure(new WpfPoint(Left, Top), true, true);
            g.LineTo(new WpfPoint(right, Top), true, false);
            g.LineTo(new WpfPoint(right, bottom - Fold), true, false);
            g.LineTo(new WpfPoint(right - Fold, bottom), true, false);
            g.LineTo(new WpfPoint(Left, bottom), true, false);
        }
        sheet.Freeze();

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Layered soft shadow (the note lifts slightly at the bottom)
            for (int i = 0; i < 4; i++)
            {
                dc.PushTransform(new TranslateTransform(0.8 + i * 0.7, 1.5 + i * 2));
                dc.DrawGeometry(Solid(Colors.Black, 0.10 - i * 0.018), null, sheet);
                dc.Pop();
            }

            // Paper
            var fill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            fill.GradientStops.Add(new GradientStop(Blend(paper, Colors.White, 0.10), 0));
            fill.GradientStops.Add(new GradientStop(paper, 0.45));
            fill.GradientStops.Add(new GradientStop(Blend(paper, strip, 0.45), 1));
            dc.DrawGeometry(fill, null, sheet);

            dc.PushClip(sheet);

            // Glue strip behind the header
            var glue = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(0, 1) };
            glue.GradientStops.Add(new GradientStop(Blend(strip, Colors.Black, 0.06), 0));
            glue.GradientStops.Add(new GradientStop(strip, 1));
            dc.DrawRectangle(glue, null, new Rect(0, 0, w, StripBottom));
            dc.DrawRectangle(Solid(ink, 0.14), null, new Rect(0, StripBottom - 1, w, 1));
            dc.DrawRectangle(Solid(Colors.White, 0.22), null, new Rect(0, StripBottom, w, 1));

            // Light falling from the top-left
            var sheen = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 1) };
            sheen.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0.16), 0));
            sheen.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, 0), 0.5));
            dc.DrawRectangle(sheen, null, new Rect(0, 0, w, h));

            // Shadow the folded flap casts on the sheet
            var cast = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 1) };
            cast.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0), 0.2));
            cast.GradientStops.Add(new GradientStop(WithAlpha(Colors.Black, 0.22), 1));
            dc.DrawRectangle(cast, null, new Rect(right - Fold - 14, bottom - Fold - 14, Fold + 14, Fold + 14));

            dc.Pop();

            // Folded corner: the back of the paper, slightly darker
            var flap = new StreamGeometry();
            using (var g = flap.Open())
            {
                g.BeginFigure(new WpfPoint(right - Fold, bottom), true, true);
                g.LineTo(new WpfPoint(right, bottom - Fold), true, false);
                g.LineTo(new WpfPoint(right - Fold, bottom - Fold), true, false);
            }
            flap.Freeze();
            var flapFill = new LinearGradientBrush { StartPoint = new WpfPoint(0, 0), EndPoint = new WpfPoint(1, 1) };
            flapFill.GradientStops.Add(new GradientStop(Blend(paper, Colors.White, 0.22), 0));
            flapFill.GradientStops.Add(new GradientStop(Blend(strip, Colors.Black, 0.16), 1));
            dc.DrawGeometry(flapFill, new WpfPen(Solid(ink, 0.18), 0.8), flap);

            // Hairline edge
            dc.DrawGeometry(null, new WpfPen(Solid(ink, 0.16), 0.8), sheet);
        }

        group.Freeze();
        return group;
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
