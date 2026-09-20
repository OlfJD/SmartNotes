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
            return procPath;
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string directExe = Path.Combine(baseDir, "SmartNotes.exe");
        if (File.Exists(directExe)) return directExe;

        DirectoryInfo? parent = Directory.GetParent(baseDir);
        while (parent != null)
        {
            string candidate = Path.Combine(parent.FullName, "SmartNotes.exe");
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
                if (!string.Equals(currentVal?.Trim('\"'), currentExe, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppName, expectedVal);
                }
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
