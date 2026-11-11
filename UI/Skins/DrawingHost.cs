using System.Windows;
using System.Windows.Media;

namespace SmartNotes.UI.Skins;

/// <summary>Lightweight element that paints a (frozen) Drawing in window coordinates.</summary>
public class DrawingHost : FrameworkElement
{
    private Drawing? _drawing;

    public Drawing? Drawing
    {
        get => _drawing;
        set
        {
            _drawing = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_drawing != null) dc.DrawDrawing(_drawing);
    }
}
