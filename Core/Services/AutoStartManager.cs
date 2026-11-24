using System;
using System.IO;
using Microsoft.Win32;

namespace SmartNotes.Core.Services;

public static class AutoStartManager
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SmartNotes";

    public static string GetExecutablePath()
    {
        string? procPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(procPath) && Path.GetExtension(procPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(procPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            // If running from a debug/intermediate folder, prefer the published/release binary if available
            if (procPath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                procPath.Contains(@"/bin/Debug/", StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo? dir = Directory.GetParent(procPath);
                while (dir != null)
                {
                    string releaseExe = Path.Combine(dir.FullName, "release", "SmartNotes.exe");
                    if (File.Exists(releaseExe)) return releaseExe;

                    string publishExe = Path.Combine(dir.FullName, "bin", "Release", "net10.0-windows", "win-x64", "publish", "SmartNotes.exe");
                    if (File.Exists(publishExe)) return publishExe;

                    dir = dir.Parent;
                }
            }

            return procPath;
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string directExe = Path.Combine(baseDir, "SmartNotes.exe");
        if (File.Exists(directExe)) return directExe;

        DirectoryInfo? parent = Directory.GetParent(baseDir);
        while (parent != null)
        {
            string candidate = Path.Combine(parent.FullName, "release", "SmartNotes.exe");
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(parent.FullName, "SmartNotes.exe");
            if (File.Exists(candidate)) return candidate;

            parent = parent.Parent;
        }

        return directExe;
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            var val = key?.GetValue(AppName)?.ToString();
            return !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    public static void SyncStartupRegistration(bool shouldEnable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            string currentExe = GetExecutablePath();
            string expectedVal = $"\"{currentExe}\"";

            if (shouldEnable)
            {
                var currentVal = key.GetValue(AppName)?.ToString();
                string? currentClean = currentVal?.Trim('\"');

                // Update if missing, changed, or pointing to a non-existent path
                if (!string.Equals(currentClean, currentExe, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(currentClean) && !File.Exists(currentClean)))
                {
                    key.SetValue(AppName, expectedVal);
                }

                // A "Disabled" set in Task Manager (StartupApproved\Run) is the user's choice: never switch it back on
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error configuring startup: {ex.Message}");
        }
    }

    public static void SetStartup(bool enable)
    {
        SyncStartupRegistration(enable);
    }
}
