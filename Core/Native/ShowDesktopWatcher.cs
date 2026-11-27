using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;
using SmartNotes.Core.Models;
using SmartNotes.UI.Windows;

namespace SmartNotes.Core.Native;

/// <summary>
/// Keeps "Desktop Stuck" notes visible through Show Desktop (Win+D, the taskbar corner, Aero Peek).
/// Show Desktop raises the shell's desktop window (Progman / WorkerW) above every other window and makes it the
/// foreground window; notes parked at HWND_BOTTOM then end up behind it (GitHub issue #1).
/// This watches foreground changes in other processes: when the desktop takes the foreground, stuck notes are
/// placed directly above the desktop window wherever it sits; when any other window takes it, they go back to the bottom.
/// </summary>
public sealed class ShowDesktopWatcher : IDisposable
{
    private readonly Func<IEnumerable<StickyNoteWindow>> _notes;
    private readonly ForegroundWatcher _foreground;
    private readonly DispatcherTimer _retry;
    private IntPtr _desktop;
    private int _retriesLeft;

    public ShowDesktopWatcher(Func<IEnumerable<StickyNoteWindow>> notes, ForegroundWatcher foreground)
    {
        _notes = notes;
        _foreground = foreground;
        // The shell can re-raise the desktop a moment after the switch (animations), so place the notes again shortly after.
        _retry = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _retry.Tick += (_, _) =>
        {
            if (--_retriesLeft <= 0) _retry.Stop();
            if (_desktop != IntPtr.Zero && Win32Api.GetForegroundWindow() is var fg && Win32Api.GetAncestor(fg, Win32Api.GA_ROOT) == _desktop)
                PlaceAboveDesktop(_desktop);
        };
        _foreground.Changed += OnForegroundChanged;
    }

    private void OnForegroundChanged(IntPtr root)
    {
        try
        {
            if (IsDesktopWindow(root))
            {
                _desktop = root;
                PlaceAboveDesktop(root);
                _retriesLeft = 3;
                _retry.Start();
            }
            else
            {
                _desktop = IntPtr.Zero;
                _retry.Stop();
                foreach (var note in StuckNotes()) note.DesktopWindowManager.SendToDesktopBottom();
            }
        }
        catch
        {
            // never let a shell quirk take the app down
        }
    }

    internal static bool IsDesktopWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var sb = new StringBuilder(32);
        Win32Api.GetClassName(hwnd, sb, sb.Capacity);
        string cls = sb.ToString();
        return cls == "Progman" || cls == "WorkerW";
    }

    /// <summary>Puts every stuck note directly above the desktop window (below whatever is above the desktop).</summary>
    private void PlaceAboveDesktop(IntPtr desktop)
    {
        var notes = StuckNotes().ToList();
        if (notes.Count == 0) return;
        var noteHandles = new HashSet<IntPtr>(notes.Select(n => new WindowInteropHelper(n).Handle));

        // The window just above the desktop that is not one of our notes
        IntPtr above = Win32Api.GetWindow(desktop, Win32Api.GW_HWNDPREV);
        while (above != IntPtr.Zero && noteHandles.Contains(above))
            above = Win32Api.GetWindow(above, Win32Api.GW_HWNDPREV);

        // Inserting after a topmost window (taskbar etc.) would not keep the note out of the topmost band,
        // so when the desktop is the highest normal window, use the top of the normal band instead.
        bool aboveIsTopmost = above != IntPtr.Zero && (Win32Api.GetWindowLong(above, Win32Api.GWL_EXSTYLE) & Win32Api.WS_EX_TOPMOST) != 0;
        IntPtr insertAfter = above == IntPtr.Zero || aboveIsTopmost ? Win32Api.HWND_TOP : above;

        foreach (var hwnd in noteHandles)
        {
            if (hwnd == IntPtr.Zero) continue;
            Win32Api.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
                Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
        }
    }

    private IEnumerable<StickyNoteWindow> StuckNotes() =>
        _notes().Where(n => n.IsVisible && n.DesktopWindowManager.PinMode == NotePinMode.DesktopStuck);

    public void Dispose()
    {
        _retry.Stop();
        _foreground.Changed -= OnForegroundChanged;
    }
}
