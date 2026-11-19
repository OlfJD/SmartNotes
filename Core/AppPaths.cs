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

    /// <summary>True when SMARTNOTES_DATA_DIR is set.</summary>
    public static bool IsOverridden => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTNOTES_DATA_DIR"));

    /// <summary>Suffix for the single-instance mutex / events: empty normally, so a copy with its own data
    /// folder (tests) runs as a separate instance and never signals or quits the user's SmartNotes.</summary>
    public static string InstanceSuffix => IsOverridden
        ? "_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataFolder.ToLowerInvariant())))[..12]
        : "";

    private static string Resolve()
    {
        string? overrideDir = Environment.GetEnvironmentVariable("SMARTNOTES_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir)) return Path.GetFullPath(overrideDir);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartNotes");
    }
}
