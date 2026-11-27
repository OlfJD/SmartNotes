using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SmartNotes.Core.Native;

/// <summary>A program with an open window, as offered in a note's "Show Only With" menu.</summary>
public sealed record OpenProgram(string ExeName, string DisplayName);

/// <summary>Finds out which program a window belongs to, and which programs have windows open (app-linked notes).</summary>
public static class ProgramWindows
{
    /// <summary>
    /// The exe file name ("Code.exe") of the program that owns a top-level window, or null. Works for elevated programs
    /// too (PROCESS_QUERY_LIMITED_INFORMATION). Store apps run inside ApplicationFrameHost.exe; for those the app's own
    /// process (the frame's child window) is used, so e.g. Calculator is "CalculatorApp.exe", not the host.
    /// </summary>
    public static string? ExeNameOf(IntPtr hwnd) => Path.GetFileName(ExePathOf(hwnd) ?? "") is { Length: > 0 } name ? name : null;

    public static string? ExePathOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
        string? path = ProcessPath(pid);
        if (path != null && Path.GetFileName(path).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            uint appPid = 0;
            Win32Api.EnumChildWindows(hwnd, (child, _) =>
            {
                Win32Api.GetWindowThreadProcessId(child, out uint childPid);
                if (childPid != pid) { appPid = childPid; return false; }
                return true;
            }, IntPtr.Zero);
            if (appPid != 0 && ProcessPath(appPid) is { } appPath) path = appPath;
        }
        return path;
    }

    private static string? ProcessPath(uint pid)
    {
        if (pid == 0) return null;
        IntPtr process = Win32Api.OpenProcess(Win32Api.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return Win32Api.QueryFullProcessImageName(process, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally
        {
            Win32Api.CloseHandle(process);
        }
    }

    /// <summary>
    /// Programs with a window you could Alt+Tab to (visible, not owned, not a tool window, with a title, not cloaked),
    /// SmartNotes and the desktop excluded, one entry per exe, sorted by name.
    /// </summary>
    public static List<OpenProgram> ListOpenPrograms()
    {
        var found = new Dictionary<string, OpenProgram>(StringComparer.OrdinalIgnoreCase);
        uint ownPid = (uint)Environment.ProcessId;
        Win32Api.EnumWindows((hwnd, _) =>
        {
            try
            {
                if (!IsAltTabWindow(hwnd)) return true;
                Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == ownPid) return true;
                string? path = ExePathOf(hwnd);
                string exe = Path.GetFileName(path ?? "");
                if (exe.Length == 0 || found.ContainsKey(exe)) return true;
                found[exe] = new OpenProgram(exe, DisplayNameOf(path!));
            }
            catch
            {
                // a window that closed while we looked at it
            }
            return true;
        }, IntPtr.Zero);
        return found.Values.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static bool IsAltTabWindow(IntPtr hwnd)
    {
        if (!Win32Api.IsWindowVisible(hwnd)) return false;
        if (Win32Api.GetWindow(hwnd, Win32Api.GW_OWNER) != IntPtr.Zero) return false;
        if ((Win32Api.GetWindowLong(hwnd, Win32Api.GWL_EXSTYLE) & Win32Api.WS_EX_TOOLWINDOW) != 0) return false;
        if (Win32Api.GetWindowTextLength(hwnd) == 0) return false;
        if (Win32Api.DwmGetWindowAttribute(hwnd, Win32Api.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        return !ShowDesktopWatcher.IsDesktopWindow(hwnd);
    }

    /// <summary>The program's own name ("Visual Studio Code"), else its product name, else the exe without ".exe".</summary>
    private static string DisplayNameOf(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            foreach (var candidate in new[] { info.FileDescription, info.ProductName })
            {
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate.Trim();
            }
        }
        catch
        {
            // no version resource readable
        }
        return Path.GetFileNameWithoutExtension(path);
    }
}
