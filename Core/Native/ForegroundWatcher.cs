using System;

namespace SmartNotes.Core.Native;

/// <summary>
/// Tells when another program's window comes to the front (EVENT_SYSTEM_FOREGROUND). SmartNotes' own windows are
/// skipped (WINEVENT_SKIPOWNPROCESS), so clicking between notes, menus and dialogs raises nothing.
/// Used by ShowDesktopWatcher (Win+D) and by app-linked notes.
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    private readonly Win32Api.WinEventDelegate _callback; // kept alive: the hook holds a native pointer to it
    private IntPtr _hook;

    /// <summary>The top-level window that now has the foreground (never one of SmartNotes' own).</summary>
    public event Action<IntPtr>? Changed;

    public ForegroundWatcher()
    {
        _callback = OnForegroundChanged;
        // Out-of-context hook: callbacks arrive on this (UI) thread through its message loop.
        _hook = Win32Api.SetWinEventHook(Win32Api.EVENT_SYSTEM_FOREGROUND, Win32Api.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _callback, 0, 0, Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero) return;
        var root = Win32Api.GetAncestor(hwnd, Win32Api.GA_ROOT);
        if (root == IntPtr.Zero) root = hwnd;
        foreach (Action<IntPtr> handler in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try
            {
                handler(root);
            }
            catch (Exception ex)
            {
                // never let a shell quirk or one listener take the others (or the app) down
                Services.AppLog.Error("Foreground change", ex);
            }
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32Api.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
