using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SmartNotes.Core.Models;
using SmartNotes.UI.Windows;

namespace SmartNotes.Core.Native;

public class DesktopWindowManager
{
    private class ClusterMemberInfo
    {
        public StickyNoteWindow Window { get; set; } = null!;
        public IntPtr Hwnd { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int CurrentX { get; set; }
        public int CurrentY { get; set; }
    }

    private readonly Window _window;
    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private NotePinMode _currentPinMode = NotePinMode.DesktopStuck;
    private bool _isInteracting = false;

    private bool _isModalMove = false;
    private Win32Api.POINT _dragStartCursor;
    private Win32Api.POINT _dragStartWinPos;

    private readonly List<ClusterMemberInfo> _activeClusterSiblings = new();
    private readonly HashSet<Guid> _clusterNoteIds = new();

    public NotePinMode PinMode => _currentPinMode;

    public DesktopWindowManager(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
        _window.Closed += OnWindowClosed;
    }

    public void StartDrag()
    {
        _isModalMove = true;
        Win32Api.GetCursorPos(out _dragStartCursor);

        if (_hwnd != IntPtr.Zero && Win32Api.GetWindowRect(_hwnd, out var winRect))
        {
            _dragStartWinPos = new Win32Api.POINT(winRect.left, winRect.top);
        }
        else
        {
            _dragStartWinPos = new Win32Api.POINT((int)_window.Left, (int)_window.Top);
        }

        _activeClusterSiblings.Clear();
        _clusterNoteIds.Clear();

        // Check if breakaway modifier key is held (Alt or Ctrl) to drag only this single note
        bool isBreakaway = (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) != 0
            || (Win32Api.GetAsyncKeyState(Win32Api.VK_MENU) < 0)
            || (Win32Api.GetAsyncKeyState(Win32Api.VK_CONTROL) < 0);

        if (!isBreakaway && _window is StickyNoteWindow rootNote && App.Instance != null)
        {
            var allWindows = App.Instance.ActiveNoteWindows;
            var cluster = NoteClusterEngine.GetConnectedCluster(rootNote, allWindows);

            if (cluster.Count > 1)
            {
                // Only the highest point (top-most) note of the stack acts as the handle to drag the entire cluster!
                var stackLeader = NoteClusterEngine.GetStackLeader(cluster);

                if (rootNote == stackLeader)
                {
                    foreach (var win in cluster)
                    {
                        _clusterNoteIds.Add(win.Note.Id);
                        if (win != rootNote)
                        {
                            var hwnd = new WindowInteropHelper(win).Handle;
                            if (hwnd != IntPtr.Zero && Win32Api.GetWindowRect(hwnd, out var sibRect))
                            {
                                _activeClusterSiblings.Add(new ClusterMemberInfo
                                {
                                    Window = win,
                                    Hwnd = hwnd,
                                    StartX = sibRect.left,
                                    StartY = sibRect.top,
                                    CurrentX = sibRect.left,
                                    CurrentY = sibRect.top
                                });
                            }
                        }
                    }
                    return;
                }
            }

            // If not the leader, or single note: drag ONLY this individual note normally!
            _clusterNoteIds.Add(rootNote.Note.Id);
        }
        else if (_window is StickyNoteWindow rootSingle)
        {
            _clusterNoteIds.Add(rootSingle.Note.Id);
        }
    }

    public void EndDrag()
    {
        _isModalMove = false;

        if (_activeClusterSiblings.Count > 0)
        {
            foreach (var sibling in _activeClusterSiblings)
            {
                try
                {
                    var source = PresentationSource.FromVisual(sibling.Window);
                    if (source?.CompositionTarget != null)
                    {
                        var matrix = source.CompositionTarget.TransformFromDevice;
                        var dipPoint = matrix.Transform(new Point(sibling.CurrentX, sibling.CurrentY));
                        sibling.Window.Left = dipPoint.X;
                        sibling.Window.Top = dipPoint.Y;
                    }
                    else
                    {
                        sibling.Window.Left = sibling.CurrentX;
                        sibling.Window.Top = sibling.CurrentY;
                    }
                    sibling.Window.SaveNoteState();
                }
                catch { }
            }
            _activeClusterSiblings.Clear();
        }
        _clusterNoteIds.Clear();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(_window).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        ApplyToolWindowStyle();
        ApplyPinMode(_currentPinMode);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    public void SetInteracting(bool interacting)
    {
        _isInteracting = interacting;
        if (!interacting && _currentPinMode == NotePinMode.DesktopStuck)
        {
            SendToDesktopBottom();
        }
    }

    public void BringToFront()
    {
        _isInteracting = true;
        if (_hwnd == IntPtr.Zero) return;

        if (_currentPinMode == NotePinMode.AlwaysOnTop)
        {
            _window.Topmost = true;
            Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_SHOWWINDOW);
        }
        else
        {
            _window.Topmost = false;
            Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOP, 0, 0, 0, 0,
                Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_SHOWWINDOW);
        }

        try
        {
            _window.Activate();
            _window.Focus();
        }
        catch { }
    }

    public void ApplyPinMode(NotePinMode pinMode)
    {
        _currentPinMode = pinMode;
        if (_hwnd == IntPtr.Zero) return;

        switch (pinMode)
        {
            case NotePinMode.DesktopStuck:
                _window.Topmost = false;
                _isInteracting = false;
                Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_BOTTOM, 0, 0, 0, 0,
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
                break;

            case NotePinMode.AlwaysOnTop:
                _window.Topmost = true;
                _isInteracting = true;
                Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_SHOWWINDOW);
                break;

            case NotePinMode.Normal:
                _window.Topmost = false;
                _isInteracting = true;
                Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_SHOWWINDOW);
                break;
        }
    }

    public void SendToDesktopBottom()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_currentPinMode == NotePinMode.DesktopStuck)
        {
            _isInteracting = false;
            Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_BOTTOM, 0, 0, 0, 0,
                Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
        }
    }

    private void ApplyToolWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;
        int exStyle = Win32Api.GetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE);
        // Add WS_EX_TOOLWINDOW so it doesn't clutter taskbar / Alt+Tab
        exStyle |= Win32Api.WS_EX_TOOLWINDOW;
        Win32Api.SetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE, exStyle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Api.WM_ENTERSIZEMOVE)
        {
            StartDrag();
        }
        else if (msg == Win32Api.WM_EXITSIZEMOVE)
        {
            EndDrag();
        }
        else if (msg == Win32Api.WM_MOVING)
        {
            try
            {
                var currentRect = Marshal.PtrToStructure<Win32Api.RECT>(lParam);

                var excludedIds = _clusterNoteIds.Count > 0 
                    ? _clusterNoteIds 
                    : (_window is UI.Windows.StickyNoteWindow sn ? new HashSet<Guid> { sn.Note.Id } : new HashSet<Guid>());

                var otherRects = App.Instance?.GetOtherNoteRects(excludedIds) ?? new List<Win32Api.RECT>();

                var screenRect = new Win32Api.RECT(
                    (int)SystemParameters.WorkArea.Left,
                    (int)SystemParameters.WorkArea.Top,
                    (int)SystemParameters.WorkArea.Right,
                    (int)SystemParameters.WorkArea.Bottom
                );

                Win32Api.RECT baseRect;
                if (_isModalMove && Win32Api.GetCursorPos(out var curCursor))
                {
                    int rawLeft = _dragStartWinPos.x + (curCursor.x - _dragStartCursor.x);
                    int rawTop = _dragStartWinPos.y + (curCursor.y - _dragStartCursor.y);
                    baseRect = new Win32Api.RECT(rawLeft, rawTop, rawLeft + currentRect.Width, rawTop + currentRect.Height);
                }
                else
                {
                    baseRect = currentRect;
                }

                var snapped = MagneticSnapEngine.CalculateSnap(baseRect, otherRects, screenRect, threshold: 22);

                int deltaX = snapped.left - _dragStartWinPos.x;
                int deltaY = snapped.top - _dragStartWinPos.y;

                bool isBreakawayLive = (Win32Api.GetAsyncKeyState(Win32Api.VK_MENU) < 0)
                    || (Win32Api.GetAsyncKeyState(Win32Api.VK_CONTROL) < 0);

                if (!isBreakawayLive)
                {
                    // Move all sibling windows in the docked cluster synchronously
                    foreach (var sibling in _activeClusterSiblings)
                    {
                        sibling.CurrentX = sibling.StartX + deltaX;
                        sibling.CurrentY = sibling.StartY + deltaY;

                        Win32Api.SetWindowPos(
                            sibling.Hwnd,
                            IntPtr.Zero,
                            sibling.CurrentX,
                            sibling.CurrentY,
                            0, 0,
                            Win32Api.SWP_NOSIZE | Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW
                        );
                    }
                }
                else if (_activeClusterSiblings.Count > 0)
                {
                    // If broken away mid-drag with Alt/Ctrl, restore siblings to original positions
                    foreach (var sibling in _activeClusterSiblings)
                    {
                        sibling.CurrentX = sibling.StartX;
                        sibling.CurrentY = sibling.StartY;
                        Win32Api.SetWindowPos(
                            sibling.Hwnd,
                            IntPtr.Zero,
                            sibling.StartX,
                            sibling.StartY,
                            0, 0,
                            Win32Api.SWP_NOSIZE | Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW
                        );
                    }
                    _activeClusterSiblings.Clear();
                }

                Marshal.StructureToPtr(snapped, lParam, false);
                handled = true;
                return (IntPtr)1;
            }
            catch { }
        }
        else if (_currentPinMode == NotePinMode.DesktopStuck)
        {
            if (msg == Win32Api.WM_ACTIVATE)
            {
                int wa = (int)(wParam.ToInt64() & 0xFFFF);
                if (wa == Win32Api.WA_INACTIVE)
                {
                    _isInteracting = false;
                    SendToDesktopBottom();
                }
            }
            else if (msg == Win32Api.WM_KILLFOCUS)
            {
                _isInteracting = false;
                SendToDesktopBottom();
            }
            else if (msg == Win32Api.WM_ACTIVATEAPP)
            {
                if (wParam == IntPtr.Zero)
                {
                    _isInteracting = false;
                    SendToDesktopBottom();
                }
            }
            else if (msg == Win32Api.WM_WINDOWPOSCHANGING && !_isInteracting)
            {
                try
                {
                    var wp = Marshal.PtrToStructure<Win32Api.WINDOWPOS>(lParam);
                    if (wp.hwndInsertAfter != Win32Api.HWND_BOTTOM && (wp.flags & Win32Api.SWP_NOZORDER) == 0)
                    {
                        wp.hwndInsertAfter = Win32Api.HWND_BOTTOM;
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
                catch { }
            }
        }

        return IntPtr.Zero;
    }
}
