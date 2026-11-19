using System;
using System.IO;

namespace SmartNotes.Core;

/// <summary>
/// Where SmartNotes keeps its data (notes, settings, dictionaries, trash, log).
/// Normally %APPDATA%\SmartNotes. The SMARTNOTES_DATA_DIR environment variable points it somewhere else,
/// so screenshot / test runs never touch the user's real notes.
/// </summary>
public static class AppPaths
{
    public static readonly string DataFolder = Resolve();

    private static string Resolve()
    {
        string? overrideDir = Environment.GetEnvironmentVariable("SMARTNOTES_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir)) return Path.GetFullPath(overrideDir);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartNotes");
    }
}
