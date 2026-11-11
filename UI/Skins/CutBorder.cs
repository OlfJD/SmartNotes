using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace SmartNotes.UI.Skins;

/// <summary>
/// A Border that can cut (chamfer) its top-left and bottom-right corners.
/// With <see cref="Cut"/> = 0 it renders exactly like a normal Border, so skins that do not
/// use cut cells are unaffected.
/// </summary>
public class CutBorder : Border
{
    public static readonly DependencyProperty CutProperty =
        DependencyProperty.Register(nameof(Cut), typeof(double), typeof(CutBorder),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Length of the cut along each edge, in pixels (0 = regular border).</summary>
    public double Cut
    {
        get => (double)GetValue(CutProperty);
        set => SetValue(CutProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double cut = Cut;
        if (cut <= 0)
        {
            base.OnRender(dc);
            return;
        }

        double t = BorderThickness.Left;
        double half = t / 2;
        double w = ActualWidth, h = ActualHeight;
        if (w <= cut * 2 || h <= cut * 2) return;

        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(new WpfPoint(half + cut, half), true, true);
            g.LineTo(new WpfPoint(w - half, half), true, false);
            g.LineTo(new WpfPoint(w - half, h - half - cut), true, false);
            g.LineTo(new WpfPoint(w - half - cut, h - half), true, false);
            g.LineTo(new WpfPoint(half, h - half), true, false);
            g.LineTo(new WpfPoint(half, half + cut), true, false);
        }
        shape.Freeze();

        WpfPen? pen = t > 0 && BorderBrush != null ? new WpfPen(BorderBrush, t) { LineJoin = PenLineJoin.Miter } : null;
        dc.DrawGeometry(Background, pen, shape);

        // Solid accent wedge in the cut bottom-right corner
        if (BorderBrush != null && cut >= 5)
        {
            double k = cut - 2.5;
            var wedge = new StreamGeometry();
            using (var g = wedge.Open())
            {
                g.BeginFigure(new WpfPoint(w, h - k), true, true);
                g.LineTo(new WpfPoint(w, h), true, false);
                g.LineTo(new WpfPoint(w - k, h), true, false);
            }
            wedge.Freeze();
            dc.DrawGeometry(BorderBrush, null, wedge);
        }
    }
}
