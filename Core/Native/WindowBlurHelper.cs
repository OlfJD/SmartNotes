using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SmartNotes.Core.Native;

public static class WindowBlurHelper
{
    public static void ApplyModernWindowStyles(Window window)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.EnsureHandle();

        try
        {
            // Dark mode title / frame
            int darkMode = 1;
            Win32Api.DwmSetWindowAttribute(hwnd, Win32Api.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Don't draw system-level rounded frames since WPF handles per-pixel transparency & corner radii
            int cornerPref = (int)Win32Api.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            Win32Api.DwmSetWindowAttribute(hwnd, Win32Api.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
        }
        catch
        {
            // Fallback for older OS
        }
    }
}
