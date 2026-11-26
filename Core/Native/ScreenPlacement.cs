using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SmartNotes.Core.Native;

/// <summary>
/// Keeps notes reachable. A note whose title strip is on no screen (a monitor was unplugged, the screens are arranged
/// differently than when the note was saved, or it ended up in a gap between screens of different sizes) is moved onto
/// the nearest screen, keeping its size. All coordinates are physical pixels (the app is per-monitor DPI aware).
/// </summary>
public static class ScreenPlacement
{
    /// <summary>The top part of a note that you grab to drag it.</summary>
    public const int StripHeight = 40;
    /// <summary>At least this much of that strip (in both directions) must be on one screen.</summary>
    public const int MinVisibleWidth = 80, MinVisibleHeight = 20;

    /// <summary>Where to move the window, or null when enough of its title strip is already on a screen.</summary>
    public static Win32Api.RECT? Rescue(Win32Api.RECT window, IReadOnlyList<Win32Api.RECT> workAreas)
    {
        if (workAreas.Count == 0) return null;
        int width = window.Width, height = window.Height;
        int stripBottom = window.top + Math.Min(StripHeight, height);
        int neededWidth = Math.Min(MinVisibleWidth, width);
        int neededHeight = Math.Min(MinVisibleHeight, stripBottom - window.top);

        foreach (var area in workAreas)
        {
            int visibleWidth = Math.Min(window.right, area.right) - Math.Max(window.left, area.left);
            int visibleHeight = Math.Min(stripBottom, area.bottom) - Math.Max(window.top, area.top);
            if (visibleWidth >= neededWidth && visibleHeight >= neededHeight) return null;
        }

        // the screen nearest to the note's centre; keep the size, slide it in just far enough
        int cx = window.left + width / 2, cy = window.top + height / 2;
        var target = workAreas.OrderBy(a => DistanceSquared(cx, cy, a)).First();
        int left = Math.Clamp(window.left, target.left, Math.Max(target.left, target.right - width));
        int top = Math.Clamp(window.top, target.top, Math.Max(target.top, target.bottom - height));
        return new Win32Api.RECT(left, top, left + width, top + height);
    }

    private static long DistanceSquared(int x, int y, Win32Api.RECT r)
    {
        long dx = x < r.left ? r.left - x : x > r.right ? x - r.right : 0;
        long dy = y < r.top ? r.top - y : y > r.bottom ? y - r.bottom : 0;
        return dx * dx + dy * dy;
    }

    /// <summary>The work areas (screen minus taskbar) of all monitors, read fresh from Windows.</summary>
    public static List<Win32Api.RECT> WorkAreas()
    {
        var result = new List<Win32Api.RECT>();
        Win32Api.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref Win32Api.RECT bounds, IntPtr data) =>
        {
            var info = new Win32Api.MONITORINFO { cbSize = Marshal.SizeOf<Win32Api.MONITORINFO>() };
            if (Win32Api.GetMonitorInfo(monitor, ref info)) result.Add(info.rcWork);
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
