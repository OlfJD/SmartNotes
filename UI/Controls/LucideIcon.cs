using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartNotes.UI.Controls;

public class LucideIcon : System.Windows.Controls.UserControl
{
    private readonly Viewbox _viewbox;
    private readonly Canvas _canvas;
    private readonly Path _path;
    private readonly Path _fillPath;

    /// <summary>
    /// Inherited icon pack name ("" = Lucide). Note skins set it on the note surface so every icon
    /// below picks {Pack}Icon_{key} / {Pack}IconFill_{key} from UI/Icons/SkinIcons.xaml, falling
    /// back to Lucide for icons the pack does not define.
    /// </summary>
    public static readonly DependencyProperty PackProperty =
        DependencyProperty.RegisterAttached("Pack", typeof(string), typeof(LucideIcon),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.Inherits, OnIconKeyChanged));

    public static void SetPack(DependencyObject element, string value) => element.SetValue(PackProperty, value);
    public static string GetPack(DependencyObject element) => (string)element.GetValue(PackProperty);

    public static readonly DependencyProperty IconKeyProperty =
        DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(LucideIcon),
            new PropertyMetadata("file-text", OnIconKeyChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(LucideIcon),
            new PropertyMetadata(2.2, OnStrokeThicknessChanged));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(double), typeof(LucideIcon),
            new PropertyMetadata(18.0, OnSizeChanged));

    public string IconKey
    {
        get => (string)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public LucideIcon()
    {
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        _path = new Path
        {
            Stroke = Foreground ?? Brushes.White,
            StrokeThickness = 2.0,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetEdgeMode(_path, EdgeMode.Unspecified);

        _canvas = new Canvas
        {
            Width = 24,
            Height = 24,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        _fillPath = new Path { Fill = Foreground ?? Brushes.White, UseLayoutRounding = true, SnapsToDevicePixels = true };
        _canvas.Children.Add(_fillPath);
        _canvas.Children.Add(_path);

        _viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = _canvas,
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(_viewbox, BitmapScalingMode.HighQuality);

        Content = _viewbox;
        Width = Size;
        Height = Size;

        Loaded += (s, e) => UpdateIcon();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ForegroundProperty)
        {
            if (_path != null && Foreground != null)
            {
                _path.Stroke = Foreground;
                if (_fillPath != null) _fillPath.Fill = Foreground;
            }
        }
    }

    private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LucideIcon icon)
        {
            icon.UpdateIcon();
        }
    }

    private static void OnStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LucideIcon icon && icon._path != null)
        {
            icon.UpdateIcon();
        }
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LucideIcon icon)
        {
            double newSize = (double)e.NewValue;
            icon.Width = newSize;
            icon.Height = newSize;
            if (icon._viewbox != null)
            {
                icon._viewbox.Width = newSize;
                icon._viewbox.Height = newSize;
            }
        }
    }

    /// <summary>Visual-only enlargement (no layout change) so pack glyphs fill their button.</summary>
    private void SetGlyphScale(double scale)
    {
        _viewbox.RenderTransformOrigin = new Point(0.5, 0.5);
        _viewbox.RenderTransform = scale == 1.0 ? Transform.Identity : new ScaleTransform(scale, scale);
    }

    public void UpdateIcon()
    {
        if (_path == null || _fillPath == null) return;
        var brush = Foreground ?? Brushes.White;
        _path.Stroke = brush;
        _fillPath.Fill = brush;

        string key = IconKey?.ToLowerInvariant().Trim() ?? "file-text";
        string pack = GetPack(this) ?? "";

        if (pack.Length > 0)
        {
            var stroke = Application.Current?.TryFindResource($"{pack}Icon_{key}") as Geometry;
            var fill = Application.Current?.TryFindResource($"{pack}IconFill_{key}") as Geometry;
            if (stroke != null || fill != null)
            {
                bool blocky = pack == "Cyber";
                _path.Data = stroke;
                _fillPath.Data = fill;
                _path.StrokeThickness = StrokeThickness * (blocky ? 0.85 : 0.7);
                _path.StrokeStartLineCap = _path.StrokeEndLineCap = blocky ? PenLineCap.Flat : PenLineCap.Round;
                _path.StrokeLineJoin = blocky ? PenLineJoin.Miter : PenLineJoin.Round;
                SetGlyphScale(blocky ? 1.1 : 1.3);
                return;
            }
        }

        // Lucide (default)
        SetGlyphScale(1.0);
        _fillPath.Data = null;
        _path.StrokeThickness = StrokeThickness;
        _path.StrokeStartLineCap = _path.StrokeEndLineCap = PenLineCap.Round;
        _path.StrokeLineJoin = PenLineJoin.Round;

        if (Application.Current?.TryFindResource($"LucideIcon_{key}") is Geometry geom)
        {
            _path.Data = geom;
        }
        else if (Application.Current?.TryFindResource("LucideIcon_file-text") is Geometry fallback)
        {
            _path.Data = fallback;
        }
    }
}
