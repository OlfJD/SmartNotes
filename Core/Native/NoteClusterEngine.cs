using System;
using System.Collections.Generic;
using System.Linq;
using SmartNotes.UI.Windows;

namespace SmartNotes.Core.Native;

/// <summary>
/// Engine for detecting docked/touching sticky notes and resolving connected note clusters
/// for synchronous multi-window group movement.
/// </summary>
public static class NoteClusterEngine
{
    public const int DefaultTolerance = 6; // Strict pixel tolerance for docked edge contact

    /// <summary>
    /// Checks if two note rectangles are touching/docked along exterior perimeter edges.
    /// Overlapping/stacked interiors are excluded.
    /// </summary>
    public static bool AreNotesAdjacent(Win32Api.RECT a, Win32Api.RECT b, int tolerance = DefaultTolerance)
    {
        // 1. Horizontal exterior adjacency (Side-by-side docking)
        bool aIsLeftOfB = (a.left < b.left) && Math.Abs(a.right - b.left) <= tolerance;
        bool aIsRightOfB = (b.left < a.left) && Math.Abs(a.left - b.right) <= tolerance;
        if (aIsLeftOfB || aIsRightOfB)
        {
            // Must have shared vertical collinear overlap
            int overlapTop = Math.Max(a.top, b.top);
            int overlapBottom = Math.Min(a.bottom, b.bottom);
            if (overlapBottom - overlapTop >= 20) // At least 20px edge contact
            {
                return true;
            }
        }

        // 2. Vertical exterior adjacency (Top-to-Bottom stack docking)
        bool aIsAboveB = (a.top < b.top) && Math.Abs(a.bottom - b.top) <= tolerance;
        bool aIsBelowB = (b.top < a.top) && Math.Abs(a.top - b.bottom) <= tolerance;
        if (aIsAboveB || aIsBelowB)
        {
            // Must have shared horizontal collinear overlap
            int overlapLeft = Math.Max(a.left, b.left);
            int overlapRight = Math.Min(a.right, b.right);
            if (overlapRight - overlapLeft >= 20) // At least 20px edge contact
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the leader/handle note of a cluster (the topmost note, breaking ties by leftmost).
    /// </summary>
    public static StickyNoteWindow GetStackLeader(IEnumerable<StickyNoteWindow> cluster)
    {
        return cluster.OrderBy(w => w.Top).ThenBy(w => w.Left).First();
    }

    /// <summary>
    /// Finds all transitively connected, unlocked sticky note windows that form a docked cluster
    /// starting from the given root note window.
    /// </summary>
    public static List<StickyNoteWindow> GetConnectedCluster(
        StickyNoteWindow rootWindow,
        IEnumerable<StickyNoteWindow> candidateWindows,
        int tolerance = DefaultTolerance)
    {
        var cluster = new List<StickyNoteWindow> { rootWindow };
        var queue = new Queue<StickyNoteWindow>();
        queue.Enqueue(rootWindow);

        // Filter valid candidates: visible, not locked, and not the root
        var remainingCandidates = candidateWindows
            .Where(w => w != null && w != rootWindow && w.IsVisible && !w.Note.IsLocked && !w.Note.IsDeleted)
            .ToList();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentRect = current.GetWindowScreenRect();

            for (int i = remainingCandidates.Count - 1; i >= 0; i--)
            {
                var candidate = remainingCandidates[i];
                var candidateRect = candidate.GetWindowScreenRect();

                if (AreNotesAdjacent(currentRect, candidateRect, tolerance))
                {
                    cluster.Add(candidate);
                    queue.Enqueue(candidate);
                    remainingCandidates.RemoveAt(i);
                }
            }
        }

        return cluster;
    }
}
