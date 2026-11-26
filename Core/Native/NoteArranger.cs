using System;
using System.Collections.Generic;
using System.Windows;

namespace SmartNotes.Core.Native;

/// <summary>
/// "Arrange notes": places groups of notes (a single note, or a docked stack that moves as one) in rows inside an
/// area, left to right and top to bottom, keeping every note's size. When the area is full it starts again at the
/// top, shifted a little, so nothing ends up off-screen. Coordinates are WPF units (DIPs).
/// </summary>
public static class NoteArranger
{
    public const double Margin = 40, Gap = 20, Cascade = 30;

    /// <summary>The new top-left corner for each group, in the same order.</summary>
    public static List<Point> Layout(IReadOnlyList<Size> groups, Rect area)
    {
        var result = new List<Point>(groups.Count);
        double left = area.Left + Margin, top = area.Top + Margin;
        double right = area.Right - Margin, bottom = area.Bottom - Margin;
        double offset = 0, x = left, y = top, rowHeight = 0;

        foreach (var size in groups)
        {
            if (x > left + offset && x + size.Width > right)
            {
                // row full: next row
                x = left + offset;
                y += rowHeight + Gap;
                rowHeight = 0;
            }
            if (y > top + offset && y + size.Height > bottom)
            {
                // no room left: start again on top, slightly offset
                offset += Cascade;
                x = left + offset;
                y = top + offset;
                rowHeight = 0;
            }
            result.Add(new Point(x, y));
            x += size.Width + Gap;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return result;
    }
}
