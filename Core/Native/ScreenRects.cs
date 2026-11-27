namespace SmartNotes.Core.Native;

/// <summary>
/// Window rectangle ↔ visible body. Shaped skins draw a few pixels inside their window (NoteSkin.VisualInset), so
/// snapping and docking work on the visible bodies and the window is moved by the same amount.
/// </summary>
public static class ScreenRects
{
    /// <summary>Shrinks a rectangle by an inset given per side (left, top, right, bottom in the RECT fields).</summary>
    public static Win32Api.RECT Deflate(Win32Api.RECT r, Win32Api.RECT inset) =>
        new(r.left + inset.left, r.top + inset.top, r.right - inset.right, r.bottom - inset.bottom);

    /// <summary>The reverse of <see cref="Deflate"/>: from the visible body back to the window.</summary>
    public static Win32Api.RECT Inflate(Win32Api.RECT r, Win32Api.RECT inset) =>
        new(r.left - inset.left, r.top - inset.top, r.right + inset.right, r.bottom + inset.bottom);
}
