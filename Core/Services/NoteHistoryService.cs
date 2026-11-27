using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

/// <summary>
/// Earlier versions of each note ("Earlier Versions" in the note's ⋯ menu). Autosave writes within 400 ms and
/// notes.json.bak only keeps the previous save, so without this, typing over a note could not be undone after a
/// restart. One file per note in &lt;data&gt;\history\, newest version first; at most <see cref="MaxVersions"/> versions,
/// none older than <see cref="MaxAge"/>. A note's history is deleted when the note leaves the trash for good.
/// </summary>
public static class NoteHistoryService
{
    public const int MaxVersions = 20;
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    /// <summary>A version is kept when a note is edited again after this long without edits.</summary>
    public static readonly TimeSpan EditPause = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object Gate = new();

    public static string Folder => Path.Combine(AppPaths.DataFolder, "history");

    private static string FileFor(Guid noteId) => Path.Combine(Folder, $"note_{noteId:N}.json");

    /// <summary>The note's saved versions, newest first (expired ones left out). Never throws.</summary>
    public static IReadOnlyList<NoteVersion> GetVersions(Guid noteId)
    {
        lock (Gate)
        {
            return Prune(Read(noteId));
        }
    }

    /// <summary>
    /// Keeps a version of the note, unless the newest saved version already has the same content.
    /// Returns false when it could not be written (logged; the note itself is not affected).
    /// </summary>
    public static bool AddVersion(Guid noteId, NoteVersion version)
    {
        lock (Gate)
        {
            try
            {
                var versions = Read(noteId);
                if (versions.Count > 0 && versions[0].SameContentAs(version)) return true;
                versions.Add(version);
                versions = Prune(versions);
                Directory.CreateDirectory(Folder);
                AtomicFile.WriteAllText(FileFor(noteId), JsonSerializer.Serialize(versions, JsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Saving an earlier version of a note", ex);
                return false;
            }
        }
    }

    /// <summary>Deletes all versions of a note (when it leaves the trash for good).</summary>
    public static void DeleteHistory(Guid noteId)
    {
        lock (Gate)
        {
            try
            {
                string file = FileFor(noteId);
                if (File.Exists(file)) File.Delete(file);
            }
            catch (Exception ex)
            {
                AppLog.Error("Deleting the history of a note", ex);
            }
        }
    }

    private static List<NoteVersion> Read(Guid noteId)
    {
        try
        {
            string file = FileFor(noteId);
            if (!File.Exists(file)) return new List<NoteVersion>();
            var list = JsonSerializer.Deserialize<List<NoteVersion>>(File.ReadAllText(file)) ?? new List<NoteVersion>();
            foreach (var v in list.Where(v => v != null))
            {
                v.Title ??= "";
                v.Content ??= "";
                v.Checklist ??= new();
                v.CopyList ??= new();
                v.Sketch ??= "";
            }
            return list.Where(v => v != null).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Error("Reading the history of a note", ex);
            return new List<NoteVersion>();
        }
    }

    /// <summary>Newest first, at most MaxVersions, none older than MaxAge.</summary>
    private static List<NoteVersion> Prune(IEnumerable<NoteVersion> versions)
    {
        var oldest = DateTime.Now - MaxAge;
        return versions.Where(v => v.SavedAt >= oldest)
            .OrderByDescending(v => v.SavedAt)
            .Take(MaxVersions)
            .ToList();
    }
}
