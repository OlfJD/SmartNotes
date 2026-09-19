using System;
using System.Collections.Generic;

namespace SmartNotes.Core.Native;

/// <summary>
/// High-performance magnetic snapping engine for sticking desktop sticky notes together
/// on all 4 perimeter edges (Left, Right, Top, Bottom) and Center guidelines.
/// </summary>
public static class MagneticSnapEngine
{
    public const int DefaultSnapThreshold = 22; // Pixels distance to trigger magnetic snap

    /// <summary>
    /// Calculates the optimal magnetic snapped rectangle for a moving note window.
    /// </summary>
    public static Win32Api.RECT CalculateSnap(
        Win32Api.RECT currentRect,
        IEnumerable<Win32Api.RECT> targetRects,
        Win32Api.RECT? screenRect = null,
        int threshold = DefaultSnapThreshold)
    {
        int width = currentRect.Width;
        int height = currentRect.Height;

        int bestDeltaX = int.MaxValue;
        int bestSnapX = currentRect.left;

        int bestDeltaY = int.MaxValue;
        int bestSnapY = currentRect.top;

        int dragCenterX = currentRect.left + width / 2;
        int dragCenterY = currentRect.top + height / 2;

        // 1. Evaluate snapping against all other active note windows
        foreach (var target in targetRects)
        {
            int targetWidth = target.Width;
            int targetHeight = target.Height;
            int targetCenterX = target.left + targetWidth / 2;
            int targetCenterY = target.top + targetHeight / 2;

            // --- Horizontal Snaps ---
            // A. Right edge to Target Left edge (sticking to the left of target)
            int dRightToLeft = Math.Abs(currentRect.right - target.left);
            if (dRightToLeft <= threshold && dRightToLeft < bestDeltaX)
            {
                bestDeltaX = dRightToLeft;
                bestSnapX = target.left - width;
            }

            // B. Left edge to Target Right edge (sticking to the right of target)
            int dLeftToRight = Math.Abs(currentRect.left - target.right);
            if (dLeftToRight <= threshold && dLeftToRight < bestDeltaX)
            {
                bestDeltaX = dLeftToRight;
                bestSnapX = target.right;
            }

            // C. Left-to-Left alignment
            int dLeftToLeft = Math.Abs(currentRect.left - target.left);
            if (dLeftToLeft <= threshold && dLeftToLeft < bestDeltaX)
            {
                bestDeltaX = dLeftToLeft;
                bestSnapX = target.left;
            }

            // D. Right-to-Right alignment
            int dRightToRight = Math.Abs(currentRect.right - target.right);
            if (dRightToRight <= threshold && dRightToRight < bestDeltaX)
            {
                bestDeltaX = dRightToRight;
                bestSnapX = target.right - width;
            }

            // E. Horizontal Center-to-Center alignment
            int dCenterX = Math.Abs(dragCenterX - targetCenterX);
            if (dCenterX <= threshold && dCenterX < bestDeltaX)
            {
                bestDeltaX = dCenterX;
                bestSnapX = targetCenterX - width / 2;
            }

            // --- Vertical Snaps ---
            // F. Bottom edge to Target Top edge (sticking above target)
            int dBottomToTop = Math.Abs(currentRect.bottom - target.top);
            if (dBottomToTop <= threshold && dBottomToTop < bestDeltaY)
            {
                bestDeltaY = dBottomToTop;
                bestSnapY = target.top - height;
            }

            // G. Top edge to Target Bottom edge (sticking below target)
            int dTopToBottom = Math.Abs(currentRect.top - target.bottom);
            if (dTopToBottom <= threshold && dTopToBottom < bestDeltaY)
            {
                bestDeltaY = dTopToBottom;
                bestSnapY = target.bottom;
            }

            // H. Top-to-Top alignment
            int dTopToTop = Math.Abs(currentRect.top - target.top);
            if (dTopToTop <= threshold && dTopToTop < bestDeltaY)
            {
                bestDeltaY = dTopToTop;
                bestSnapY = target.top;
            }

            // I. Bottom-to-Bottom alignment
            int dBottomToBottom = Math.Abs(currentRect.bottom - target.bottom);
            if (dBottomToBottom <= threshold && dBottomToBottom < bestDeltaY)
            {
                bestDeltaY = dBottomToBottom;
                bestSnapY = target.bottom - height;
            }

            // J. Vertical Center-to-Center alignment
            int dCenterY = Math.Abs(dragCenterY - targetCenterY);
            if (dCenterY <= threshold && dCenterY < bestDeltaY)
            {
                bestDeltaY = dCenterY;
                bestSnapY = targetCenterY - height / 2;
            }
        }

        // 2. Evaluate snapping against Screen / WorkArea boundaries
        if (screenRect.HasValue)
        {
            var s = screenRect.Value;

            // Screen Left
            int dScreenLeft = Math.Abs(currentRect.left - s.left);
            if (dScreenLeft <= threshold && dScreenLeft < bestDeltaX)
            {
                bestDeltaX = dScreenLeft;
                bestSnapX = s.left;
            }

            // Screen Right
            int dScreenRight = Math.Abs(currentRect.right - s.right);
            if (dScreenRight <= threshold && dScreenRight < bestDeltaX)
            {
                bestDeltaX = dScreenRight;
                bestSnapX = s.right - width;
            }

            // Screen Top
            int dScreenTop = Math.Abs(currentRect.top - s.top);
            if (dScreenTop <= threshold && dScreenTop < bestDeltaY)
            {
                bestDeltaY = dScreenTop;
                bestSnapY = s.top;
            }

            // Screen Bottom
            int dScreenBottom = Math.Abs(currentRect.bottom - s.bottom);
            if (dScreenBottom <= threshold && dScreenBottom < bestDeltaY)
            {
                bestDeltaY = dScreenBottom;
                bestSnapY = s.bottom - height;
            }
        }

        return new Win32Api.RECT(
            bestSnapX,
            bestSnapY,
            bestSnapX + width,
            bestSnapY + height
        );
    }
}
