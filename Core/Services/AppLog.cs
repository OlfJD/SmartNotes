using System;
using System.IO;

namespace SmartNotes.Core.Services;

/// <summary>
/// The app's log file (startup.log in the data folder). Errors that the app recovers from are written here
/// instead of Debug.WriteLine, which a release build never shows. Never throws.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static bool _rotated;

    public static string FilePath => Path.Combine(AppPaths.DataFolder, "startup.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataFolder);
                // Appended on every launch; start fresh once it passes 512 KB
                if (!_rotated)
                {
                    _rotated = true;
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > 512 * 1024) info.Delete();
                }
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
            }
        }
        catch { }
    }

    public static void Error(string what, Exception ex) => Write($"[Error] {what}: {ex.GetType().Name}: {ex.Message}");
}
