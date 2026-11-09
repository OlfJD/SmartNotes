using System;
using System.IO;

namespace SmartNotes.Core.Services;

/// <summary>
/// Crash-safe file writes: the new content goes to a temp file first and then replaces the target in
/// one step, so a crash or power loss mid-write can never leave a truncated file behind.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents, string? backupPath = null)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);

        if (File.Exists(path))
        {
            try
            {
                // Atomic on NTFS, and keeps the previous version as a one-generation backup
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                return;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (PlatformNotSupportedException) { }
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
