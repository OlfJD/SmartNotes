using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Native;

public class DesktopWindowManager
{
    private readonly Window _window;
    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private NotePinMode _currentPinMode = NotePinMode.DesktopStuck;
    private bool _isInteracting = false;

    public NotePinMode PinMode => _currentPinMode;

    public DesktopWindowManager(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
        _window.Closed += OnWindowClosed;
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

    public void ApplyPinMode(NotePinMode pinMode)
    {
        _currentPinMode = pinMode;
        if (_hwnd == IntPtr.Zero) return;

        switch (pinMode)
        {
            case NotePinMode.DesktopStuck:
                _window.Topmost = false;
                SendToDesktopBottom();
                break;

            case NotePinMode.AlwaysOnTop:
                _window.Topmost = true;
                Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_SHOWWINDOW);
                break;

            case NotePinMode.Normal:
                _window.Topmost = false;
                Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_SHOWWINDOW);
                break;
        }
    }

    public void SendToDesktopBottom()
    {
        if (_hwnd == IntPtr.Zero) return;
        Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_BOTTOM, 0, 0, 0, 0,
            Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
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
        switch (msg)
        {
            case Win32Api.WM_ACTIVATE:
                int lowWord = (int)(wParam.ToInt64() & 0xFFFF);
                if (lowWord == Win32Api.WA_INACTIVE)
                {
                    if (_currentPinMode == NotePinMode.DesktopStuck && !_isInteracting)
                    {
                        SendToDesktopBottom();
                    }
                }
                break;

            case Win32Api.WM_KILLFOCUS:
                if (_currentPinMode == NotePinMode.DesktopStuck && !_isInteracting)
                {
                    SendToDesktopBottom();
                }
                break;

            case Win32Api.WM_WINDOWPOSCHANGING:
                if (_currentPinMode == NotePinMode.DesktopStuck && !_isInteracting)
                {
                    try
                    {
                        var pos = Marshal.PtrToStructure<Win32Api.WINDOWPOS>(lParam);
                        pos.hwndInsertAfter = Win32Api.HWND_BOTTOM;
                        pos.flags &= ~Win32Api.SWP_NOZORDER;
                        Marshal.StructureToPtr(pos, lParam, true);
                    }
                    catch { }
                }
                break;
        }
        return IntPtr.Zero;
    }
}
