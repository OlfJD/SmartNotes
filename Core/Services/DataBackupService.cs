using System;
using System.IO;
using System.Linq;

namespace SmartNotes.Core.Services;

/// <summary>
/// Keeps copies of the user's data so no update or bad load can lose notes.
///  * Before a new app version runs for the first time, notes.json, its .bak and settings.json are copied to
///    &lt;data&gt;\backups\&lt;date&gt;-before-v&lt;version&gt;\ (call before NoteStorageService loads anything).
///  * <see cref="Snapshot"/> can be called any time (e.g. when notes.json could not be read) before writing.
/// The newest <see cref="KeepSnapshots"/> snapshot folders are kept.
/// </summary>
public static class DataBackupService
{
    private const int KeepSnapshots = 10;
    private static readonly string DataFolder = AppPaths.DataFolder;
    private static readonly string BackupFolder = Path.Combine(DataFolder, "backups");
    private static readonly string VersionFile = Path.Combine(DataFolder, "last_version.txt");
    private static readonly string[] DataFiles = { "notes.json", "notes.json.bak", "settings.json" };

    /// <summary>Takes a snapshot when this is the first start of a different app version. Never throws.</summary>
    public static void SnapshotIfNewVersion(Version current)
    {
        try
        {
            string now = current.ToString(3);
            string? last = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;
            if (last == now) return;
            // last == null: first start of a version that writes this file (or a fresh install, then there is nothing to copy)
            Snapshot($"before-v{now}" + (last != null ? $"-from-v{last}" : ""));
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(VersionFile, now);
        }
        catch
        {
            // a failed backup must never stop the app from starting
        }
    }

    /// <summary>Copies the data files that exist into a new snapshot folder. Returns the folder, or null if nothing was copied.</summary>
    public static string? Snapshot(string reason)
    {
        try
        {
            var existing = DataFiles.Select(f => Path.Combine(DataFolder, f)).Where(File.Exists).ToList();
            if (existing.Count == 0) return null;

            string folder = Path.Combine(BackupFolder, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Sanitize(reason)}");
            Directory.CreateDirectory(folder);
            foreach (var file in existing)
            {
                try { File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), overwrite: true); }
                catch { /* copy what we can */ }
            }
            Prune();
            return folder;
        }
        catch
        {
            return null;
        }
    }

    private static void Prune()
    {
        try
        {
            var old = new DirectoryInfo(BackupFolder).GetDirectories()
                .OrderByDescending(d => d.CreationTimeUtc)
                .Skip(KeepSnapshots);
            foreach (var dir in old)
            {
                try { dir.Delete(recursive: true); } catch { }
            }
        }
        catch { }
    }

    private static string Sanitize(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '-'));
}
