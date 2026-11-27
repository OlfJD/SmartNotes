using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace SmartNotes.UI.Windows;

/// <summary>
/// Sketch mode, the fourth note mode: drawing with a pen or the mouse on WPF's InkCanvas (pen pressure comes with
/// it). Pen, highlighter and eraser, five colours (the first is the note's own), and Clear, which first keeps the
/// drawing as an earlier version so it can be brought back. The strokes are saved in NoteItem.Sketch.
/// </summary>
public partial class StickyNoteWindow
{
    private string _sketchTool = "Pen";
    private int _sketchColorIndex;
    /// <summary>Bumped on every stroke change, so "modified" and Earlier Versions notice drawing.</summary>
    private int _sketchRevision;

    private static readonly Color[] SketchFixedColors =
    {
        Color.FromRgb(0xE5, 0x48, 0x4D), // red
        Color.FromRgb(0x3B, 0x82, 0xF6), // blue
        Color.FromRgb(0x22, 0xC5, 0x5E), // green
    };

    /// <summary>The note's own accent, its text colour, then red, blue and green.</summary>
    private Color[] SketchPalette => new[] { _currentTheme.GlowBrush.Color, _currentTheme.TextPrimaryBrush.Color }
        .Concat(SketchFixedColors).ToArray();

    private void InitSketch()
    {
        SetSketchStrokes(SketchData.FromBase64(_note.Sketch));
        ApplySketchTool();
    }

    private void SetSketchStrokes(StrokeCollection strokes)
    {
        SketchCanvas.Strokes.StrokesChanged -= SketchStrokes_Changed;
        SketchCanvas.Strokes = strokes;
        strokes.StrokesChanged += SketchStrokes_Changed;
        _sketchRevision++;
    }

    private void SketchStrokes_Changed(object? sender, StrokeCollectionChangedEventArgs e)
    {
        _sketchRevision++;
    }

    private void SketchCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e) => RequestSave(isTyping: false);

    private void SketchCanvas_StrokeErased(object sender, RoutedEventArgs e) => RequestSave(isTyping: false);

    private void BtnSketchMode_Click(object sender, RoutedEventArgs e)
    {
        UpdateViewMode(NoteViewMode.Sketch);
        RequestSave();
    }

    private void BtnSketchTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tool })
        {
            _sketchTool = tool;
            ApplySketchTool();
        }
    }

    /// <summary>Clearing keeps the drawing as an earlier version first, so it can be brought back from the ⋯ menu.</summary>
    private void BtnSketchClear_Click(object sender, RoutedEventArgs e)
    {
        if (_note.IsLocked || SketchCanvas.Strokes.Count == 0) return;
        SaveNoteState();
        NoteHistoryService.AddVersion(_note.Id, TakeVersion());
        SetSketchStrokes(new StrokeCollection());
        if (SaveNoteState()) ShowStatus("undo-2", LocalizationService.T("Status_SketchCleared"), _inkEmerald);
    }

    /// <summary>Pen, highlighter or eraser with the chosen colour; nothing while the note is locked.</summary>
    private void ApplySketchTool()
    {
        if (SketchCanvas == null) return;
        var colors = SketchPalette;
        _sketchColorIndex = Math.Clamp(_sketchColorIndex, 0, colors.Length - 1);
        var color = colors[_sketchColorIndex];

        if (_note.IsLocked)
        {
            SketchCanvas.EditingMode = InkCanvasEditingMode.None;
        }
        else if (_sketchTool == "Eraser")
        {
            SketchCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            double eraser = ToolSize("Eraser");
            SketchCanvas.EraserShape = new EllipseStylusShape(eraser, eraser);
        }
        else
        {
            SketchCanvas.EditingMode = InkCanvasEditingMode.Ink;
            bool highlighter = _sketchTool == "Highlighter";
            double size = ToolSize(_sketchTool);
            SketchCanvas.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = color,
                Width = size,
                Height = highlighter ? Math.Max(2, size * 0.36) : size,
                StylusTip = highlighter ? StylusTip.Rectangle : StylusTip.Ellipse,
                IsHighlighter = highlighter,
                FitToCurve = true,
                IgnorePressure = highlighter,
            };
        }
        SketchToolbar.IsEnabled = !_note.IsLocked;

        // the size button's dot grows with the tool's thickness
        double dot = Math.Clamp(ToolSize(_sketchTool) * (_sketchTool == "Pen" ? 1.6 : 0.6), 3, 16);
        SketchSizeDot.Width = SketchSizeDot.Height = dot;
        BtnSketchSize.ToolTip = LocalizationService.T("Tooltip_SketchSize", LocalizationService.T("Tooltip_Sketch" + _sketchTool));

        IconSketchPen.Foreground = _sketchTool == "Pen" ? _inkActive : _inkMuted;
        IconSketchHighlighter.Foreground = _sketchTool == "Highlighter" ? _inkActive : _inkMuted;
        IconSketchEraser.Foreground = _sketchTool == "Eraser" ? _inkActive : _inkMuted;
        BuildSketchColors(colors);
    }

    private void BuildSketchColors(Color[] colors)
    {
        SketchColorDots.Children.Clear();
        for (int i = 0; i < colors.Length; i++)
        {
            int index = i;
            bool selected = i == _sketchColorIndex && _sketchTool != "Eraser";
            var dot = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(colors[i]),
                Stroke = selected ? _inkActive : _inkMuted,
                StrokeThickness = selected ? 2 : 0.8,
            };
            var button = new Button
            {
                Style = TryFindResource("IconButton") as Style,
                Width = 22,
                Height = 24,
                Padding = new Thickness(0),
                Content = dot,
            };
            button.Click += (s, e) =>
            {
                _sketchColorIndex = index;
                if (_sketchTool == "Eraser") _sketchTool = "Pen";
                ApplySketchTool();
            };
            SketchColorDots.Children.Add(button);
        }
    }

    // ---------------------------------------------------------------- thickness per tool

    /// <summary>Smallest and largest thickness the slider offers for each tool.</summary>
    private static (double Min, double Max) SizeRange(string tool) => tool switch
    {
        "Highlighter" => (4, 40),
        "Eraser" => (4, 60),
        _ => (1, 16),
    };

    private double ToolSize(string tool)
    {
        var s = _settingsService.Settings;
        double size = tool switch { "Highlighter" => s.SketchHighlighterSize, "Eraser" => s.SketchEraserSize, _ => s.SketchPenSize };
        var (min, max) = SizeRange(tool);
        return Math.Clamp(size <= 0 ? min : size, min, max);
    }

    private bool _settingSizeSlider;

    private void BtnSketchSize_Click(object sender, RoutedEventArgs e)
    {
        var (min, max) = SizeRange(_sketchTool);
        _settingSizeSlider = true;
        SliderSketchSize.Minimum = min;
        SliderSketchSize.Maximum = max;
        SliderSketchSize.Value = ToolSize(_sketchTool);
        _settingSizeSlider = false;
        TxtSketchSizeHeader.Text = LocalizationService.T("Sketch_SizeHeader", LocalizationService.T("Tooltip_Sketch" + _sketchTool));
        TxtSketchSizeValue.Text = $"{SliderSketchSize.Value:0.#} px";
        SketchSizePopup.IsOpen = true;
    }

    private void SliderSketchSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settingSizeSlider || TxtSketchSizeValue == null) return;
        double size = Math.Round(e.NewValue * 2) / 2; // half pixels are enough
        var s = _settingsService.Settings;
        switch (_sketchTool)
        {
            case "Highlighter": s.SketchHighlighterSize = size; break;
            case "Eraser": s.SketchEraserSize = size; break;
            default: s.SketchPenSize = size; break;
        }
        TxtSketchSizeValue.Text = $"{size:0.#} px";
        ApplySketchTool();
    }

    private void SketchSizePopup_Closed(object? sender, EventArgs e) => _settingsService.Save(); // remembered for every note

    /// <summary>The drawing as a PNG (for Export as Markdown), drawn on the note's own background.</summary>
    private void SaveSketchPng(string path)
    {
        var bounds = SketchCanvas.Strokes.GetBounds();
        double w = Math.Max(SketchCanvas.ActualWidth, bounds.IsEmpty ? 1 : bounds.Right + 8);
        double h = Math.Max(SketchCanvas.ActualHeight, bounds.IsEmpty ? 1 : bounds.Bottom + 8);
        const double scale = 2;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(_currentTheme.BgBrush, null, new Rect(0, 0, w, h));
            SketchCanvas.Strokes.Draw(dc);
        }
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
