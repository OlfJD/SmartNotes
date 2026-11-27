using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static SmartNotes.UI.Skins.ArtKit;
using FontFamily = System.Windows.Media.FontFamily;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>
/// The wax seal a locked Medieval note gets with living themes on: an uneven blob of wax in the note's seal colour,
/// a raised rim, and the first letter of the title in blackletter pressed into it (a lily without a title).
/// </summary>
public static class WaxSealArt
{
    private static readonly FontFamily Blackletter = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#UnifrakturMaguntia, Georgia");

    public static ImageSource Render(WpfColor wax, string? title, int px)
    {
        const double S = 46;
        var c = new WpfPoint(S / 2, S / 2);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(px / S, px / S));

            // the spilled wax: a circle pushed out in a few soft lobes
            var blob = new StreamGeometry();
            using (var g = blob.Open())
            {
                const int n = 40;
                for (int i = 0; i <= n; i++)
                {
                    double a = i * 2 * Math.PI / n;
                    double r = 19.5 + 1.6 * Math.Sin(a * 3 + 0.7) + 1.1 * Math.Sin(a * 5 + 2.1);
                    var p = new WpfPoint(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
                    if (i == 0) g.BeginFigure(p, true, true); else g.LineTo(p, true, true);
                }
            }
            blob.Freeze();
            dc.PushTransform(new TranslateTransform(0.8, 1.4));
            dc.DrawGeometry(Solid(Colors.Black, 0.28), null, blob);
            dc.Pop();
            var body = new RadialGradientBrush { GradientOrigin = new WpfPoint(0.35, 0.3), Center = new WpfPoint(0.45, 0.45), RadiusX = 0.65, RadiusY = 0.65 };
            body.GradientStops.Add(new GradientStop(Blend(wax, Colors.White, 0.22), 0));
            body.GradientStops.Add(new GradientStop(wax, 0.55));
            body.GradientStops.Add(new GradientStop(Blend(wax, Colors.Black, 0.35), 1));
            body.Freeze();
            dc.DrawGeometry(body, null, blob);

            // the stamp's raised rim and the pressed-in face
            dc.DrawEllipse(null, Line(Blend(wax, Colors.Black, 0.35), 0.9, 1.6), c, 14.5, 14.5);
            dc.DrawEllipse(null, Line(Blend(wax, Colors.White, 0.35), 0.5, 0.8), new WpfPoint(c.X - 0.4, c.Y - 0.5), 15.3, 15.3);
            var face = new RadialGradientBrush { GradientOrigin = new WpfPoint(0.65, 0.7) };
            face.GradientStops.Add(new GradientStop(Blend(wax, Colors.White, 0.08), 0));
            face.GradientStops.Add(new GradientStop(Blend(wax, Colors.Black, 0.18), 1));
            face.Freeze();
            dc.DrawEllipse(face, null, c, 13.8, 13.8);

            string letter = InkWashiArt.SealLetter(title);
            Geometry? mark = null;
            if (letter != "印")
            {
                var text = new FormattedText(letter, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(Blackletter, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 19, Brushes.Black, 1.0);
                mark = text.BuildGeometry(new WpfPoint(0, 0));
            }
            mark ??= Lily();
            var b = mark.Bounds;
            mark.Transform = new TranslateTransform(c.X - b.Left - b.Width / 2, c.Y - b.Top - b.Height / 2);
            // pressed in: a dark edge below right, a light edge above left
            dc.PushTransform(new TranslateTransform(0.6, 0.7));
            dc.DrawGeometry(Solid(Blend(wax, Colors.White, 0.35), 0.7), null, mark);
            dc.Pop();
            dc.DrawGeometry(Solid(Blend(wax, Colors.Black, 0.45), 0.95), null, mark);
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>A simple heraldic lily for a seal without a letter.</summary>
    private static Geometry Lily()
    {
        var geo = Geometry.Parse("M 0 -10 C 3 -6 3 -2 0 2 C -3 -2 -3 -6 0 -10 Z M -1.2 2 C -7 0 -9 -5 -6 -8 C -6 -4 -4 -1 -1.2 2 Z M 1.2 2 C 7 0 9 -5 6 -8 C 6 -4 4 -1 1.2 2 Z M -5 3 L 5 3 L 5 5 L -5 5 Z M -1.2 5 L 1.2 5 L 2.5 10 L -2.5 10 Z");
        return geo.Clone();
    }
}
