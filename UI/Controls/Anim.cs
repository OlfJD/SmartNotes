using System.Windows.Media.Animation;

namespace SmartNotes.UI.Controls;

/// <summary>
/// Animation helpers. Note windows are layered (transparent) windows, which WPF redraws in software: every frame of an
/// animation repaints the whole window. Capping the frame rate keeps animations smooth enough while costing a
/// fraction of the default 60 frames per second.
/// </summary>
public static class Anim
{
    /// <summary>Caps how often WPF redraws for this animation.</summary>
    public static T AtFps<T>(this T timeline, int fps) where T : Timeline
    {
        Timeline.SetDesiredFrameRate(timeline, fps);
        return timeline;
    }
}
