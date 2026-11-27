using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

public class NoteStorageService
{
    private static readonly string AppFolder = AppPaths.DataFolder;
    private static readonly string NotesFilePath = Path.Combine(AppFolder, "notes.json");
    private static readonly string NotesBackupPath = Path.Combine(AppFolder, "notes.json.bak");
    private static readonly string NotesPendingPath = Path.Combine(AppFolder, "notes.pending.json");
    private static readonly string TrashFolder = Path.Combine(AppFolder, "trash");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static readonly TimeSpan TrashRetentionPeriod = TimeSpan.FromHours(48);

    private readonly List<NoteItem> _notes = new();
    private readonly object _lock = new();
    private bool _loadIncomplete; // notes.json exists but could not be read at startup (see Load / Save)

    public string TrashDirectoryPath => TrashFolder;

    public IReadOnlyList<NoteItem> Notes
    {
        get
        {
            lock (_lock)
            {
                return _notes.ToList();
            }
        }
    }

    public NoteStorageService()
    {
        Load();
    }

    public void Load()
    {
        lock (_lock)
        {
            _notes.Clear();
            _loadIncomplete = false;
            bool isFirstRun = false;
            bool recoveredFromBackup = false;
            bool mergedPending = false;
            try
            {
                Directory.CreateDirectory(AppFolder);

                isFirstRun = !File.Exists(NotesFilePath) && !File.Exists(NotesBackupPath);

                var list = TryReadNotes(NotesFilePath, out bool mainFileCorrupt);
                if (list == null && (mainFileCorrupt || File.Exists(NotesFilePath)))
                {
                    // notes.json exists but could not be read: copy everything aside BEFORE anything can write,
                    // so a later save can never replace the user's real notes (and then the .bak) with an empty list
                    DataBackupService.Snapshot("unreadable-notes");
                }
                if (mainFileCorrupt)
                {
                    // Never let a damaged notes.json be overwritten: keep it aside for manual recovery
                    PreserveCorruptFile(NotesFilePath);
                }
                if (list == null && File.Exists(NotesBackupPath))
                {
                    list = TryReadNotes(NotesBackupPath, out _);
                    recoveredFromBackup = list != null;
                }
                // notes.json is there but stayed locked: don't overwrite it later, merge with it once readable
                _loadIncomplete = !mainFileCorrupt && File.Exists(NotesFilePath) && !recoveredFromBackup && list == null;

                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item == null) continue; // one damaged entry must not stop the others from loading
                        Repair(item);
                        // Migrate legacy inline deleted notes into dedicated 48h trash folder
                        // (if the trash cannot be written, keep the note in the list so it is not lost)
                        if (!item.IsDeleted || !SaveToTrash(item))
                        {
                            _notes.Add(item);
                        }
                    }
                }

                mergedPending = !_loadIncomplete && MergePendingNotes();
            }
            catch (Exception ex)
            {
                AppLog.Error("Loading notes", ex);
                // Never let a later save replace notes.json with whatever part of it got loaded
                if (File.Exists(NotesFilePath)) _loadIncomplete = true;
            }

            // Housekeeping only: a problem with the trash folder must never stop the notes from loading
            try
            {
                Directory.CreateDirectory(TrashFolder);
                PurgeExpiredTrash(); // deleted notes older than 48 hours
            }
            catch (Exception ex)
            {
                AppLog.Error("Preparing the trash folder", ex);
            }

            // Welcome notes only on a genuine first run - not when the file failed to load
            // (that used to replace every note with the welcome set) or when the user deleted them all
            if (isFirstRun)
            {
                CreateDefaultWelcomeNotes();
                Save();
            }
            else if (recoveredFromBackup || mergedPending)
            {
                Save();
            }
        }
    }

    /// <summary>Fills in what a damaged or hand-edited notes.json left out (null lists crash the note window).</summary>
    private static NoteItem Repair(NoteItem note)
    {
        if (note.Id == Guid.Empty) note.Id = Guid.NewGuid();
        note.Title ??= "";
        note.Content ??= "";
        note.Checklist ??= new();
        note.Checklist.RemoveAll(i => i == null);
        note.CopyList ??= new();
        note.CopyList.RemoveAll(i => i == null);
        note.ColorKey ??= "Amber";
        note.SkinKey ??= NoteSkin.ClassicKey;
        note.RuneKey ??= GreatRune.DefaultKey;
        note.Emblems ??= new();
        note.LinkedApp ??= "";
        note.LinkedAppName ??= "";
        return note;
    }

    /// <summary>
    /// Edits made while notes.json stayed locked for a whole session were parked in notes.pending.json.
    /// Bring them back (newer version of a note wins) and keep the side file renamed for reference.
    /// </summary>
    private bool MergePendingNotes()
    {
        if (!File.Exists(NotesPendingPath)) return false;
        var pending = TryReadNotes(NotesPendingPath, out _);
        if (pending == null) return false;

        foreach (var item in pending)
        {
            if (item == null || item.IsDeleted) continue;
            Repair(item);
            int index = _notes.FindIndex(n => n.Id == item.Id);
            if (index < 0) _notes.Add(item);
            else if (item.ModifiedAt > _notes[index].ModifiedAt) _notes[index] = item;
        }
        try
        {
            File.Move(NotesPendingPath, Path.Combine(AppFolder, $"notes.pending.merged-{DateTime.Now:yyyyMMdd-HHmmss}.json"), overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Renaming notes.pending.json after merging it", ex);
        }
        AppLog.Write($"Merged {pending.Count} note(s) from notes.pending.json.");
        return true;
    }

    /// <summary>
    /// Returns null if the file is missing or unreadable. <paramref name="corrupt"/> is true only when the content
    /// itself is bad (invalid JSON). A file that is merely locked (antivirus, backup or sync tools) is retried
    /// and is never called corrupt, so it is not moved aside.
    /// </summary>
    private static List<NoteItem>? TryReadNotes(string path, out bool corrupt)
    {
        corrupt = false;
        if (!File.Exists(path)) return null;

        string? text = null;
        for (int attempt = 0; attempt < 8 && text == null; attempt++)
        {
            try { text = File.ReadAllText(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"Notes file busy ({attempt + 1}/8): {ex.Message}");
                System.Threading.Thread.Sleep(250);
            }
        }
        if (text == null) return null; // still locked: not corrupt, leave it where it is

        try
        {
            var list = JsonSerializer.Deserialize<List<NoteItem>>(text);
            if (list == null) corrupt = true;
            return list;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Unreadable notes file {Path.GetFileName(path)}", ex);
            corrupt = true;
            return null;
        }
    }

    private static void PreserveCorruptFile(string path)
    {
        try
        {
            string target = Path.Combine(AppFolder, $"notes.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(path, target, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Keeping the damaged notes file aside", ex);
        }
    }

    /// <summary>Writes all notes. Returns false when nothing could be written (the error is logged).</summary>
    public bool Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                if (_loadIncomplete)
                {
                    var onDisk = TryReadNotes(NotesFilePath, out _);
                    if (onDisk == null)
                    {
                        // still unreadable: keep this session's notes in a side file, never over the real one
                        // (merged back on the next start, see MergePendingNotes)
                        AtomicFile.WriteAllText(NotesPendingPath, JsonSerializer.Serialize(_notes, JsonOptions));
                        return true;
                    }
                    foreach (var item in onDisk)
                    {
                        if (item != null && !item.IsDeleted && !_notes.Any(n => n.Id == item.Id)) _notes.Add(Repair(item));
                    }
                    _loadIncomplete = false;
                }
                string json = JsonSerializer.Serialize(_notes, JsonOptions);
                AtomicFile.WriteAllText(NotesFilePath, json, NotesBackupPath);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Saving notes", ex);
                return false;
            }
        }
    }

    /// <summary>Adds (or replaces) a note and saves. Returns false if the save failed.</summary>
    public bool AddNote(NoteItem note)
    {
        lock (_lock)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == note.Id);
            if (existing != null)
            {
                int index = _notes.IndexOf(existing);
                _notes[index] = note;
            }
            else
            {
                _notes.Add(note);
            }
            return Save();
        }
    }

    /// <summary>Replaces a known note and saves. Returns false if the save failed.</summary>
    public bool UpdateNote(NoteItem note)
    {
        lock (_lock)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == note.Id);
            if (existing == null) return true; // deleted meanwhile: nothing to write
            int index = _notes.IndexOf(existing);
            _notes[index] = note;
            return Save();
        }
    }

    /// <summary>
    /// Moves a note to the 48-hour trash (or deletes it for good). The trash copy is written first: if that fails,
    /// the note stays a note and false is returned, so a delete can never lose it.
    /// </summary>
    public bool DeleteNote(Guid id, bool permanent = false)
    {
        lock (_lock)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == id);
            if (existing != null && !permanent)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = DateTime.Now;
                if (!SaveToTrash(existing))
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    return false;
                }
            }
            if (existing != null)
            {
                _notes.Remove(existing);
                Save();
            }
            if (permanent)
            {
                DeleteFromTrash(id);
                ForgetHistory(id);
            }
            return true;
        }
    }

    /// <summary>A note left the trash for good: its earlier versions go too (never for a note that is still active).</summary>
    private void ForgetHistory(Guid id)
    {
        bool stillActive;
        lock (_lock)
        {
            stillActive = _notes.Any(n => n.Id == id);
        }
        if (!stillActive) NoteHistoryService.DeleteHistory(id);
    }

    /// <summary>The note id in a trash file name (note_&lt;id&gt;.json).</summary>
    private static bool TryIdFromTrashFile(string file, out Guid id)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        return Guid.TryParseExact(name.StartsWith("note_", StringComparison.Ordinal) ? name.Substring(5) : "", "N", out id);
    }

    private static bool SaveToTrash(NoteItem note)
    {
        try
        {
            Directory.CreateDirectory(TrashFolder);
            string filePath = Path.Combine(TrashFolder, $"note_{note.Id:N}.json");
            string json = JsonSerializer.Serialize(note, JsonOptions);
            AtomicFile.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Moving a note to the trash", ex);
            return false;
        }
    }

    private void DeleteFromTrash(Guid id)
    {
        try
        {
            string filePath = Path.Combine(TrashFolder, $"note_{id:N}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch { }
    }

    public IReadOnlyList<NoteItem> GetDeletedNotes()
    {
        var result = new List<NoteItem>();
        try
        {
            PurgeExpiredTrash();

            if (Directory.Exists(TrashFolder))
            {
                var files = Directory.GetFiles(TrashFolder, "note_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var item = JsonSerializer.Deserialize<NoteItem>(json);
                        if (item != null)
                        {
                            result.Add(item);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return result.OrderByDescending(n => n.DeletedAt ?? DateTime.MinValue).ToList();
    }

    public NoteItem? RestoreNoteFromTrash(Guid id)
    {
        lock (_lock)
        {
            try
            {
                string filePath = Path.Combine(TrashFolder, $"note_{id:N}.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var note = JsonSerializer.Deserialize<NoteItem>(json);
                    if (note != null)
                    {
                        Repair(note);
                        note.IsDeleted = false;
                        note.DeletedAt = null;
                        note.ModifiedAt = DateTime.Now;

                        if (!_notes.Any(n => n.Id == note.Id))
                        {
                            _notes.Add(note);
                            Save();
                        }

                        File.Delete(filePath);
                        return note;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Restoring a note from the trash", ex);
            }

            return null;
        }
    }

    public List<NoteItem> RestoreAllTrash()
    {
        var restored = new List<NoteItem>();
        var deleted = GetDeletedNotes();
        foreach (var item in deleted)
        {
            var note = RestoreNoteFromTrash(item.Id);
            if (note != null)
            {
                restored.Add(note);
            }
        }
        return restored;
    }

    public void PurgeExpiredTrash()
    {
        try
        {
            if (!Directory.Exists(TrashFolder)) return;

            var files = Directory.GetFiles(TrashFolder, "note_*.json");
            var now = DateTime.Now;

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var item = JsonSerializer.Deserialize<NoteItem>(json);
                    if (item != null)
                    {
                        var deletedAt = item.DeletedAt ?? File.GetCreationTime(file);
                        if (now - deletedAt > TrashRetentionPeriod)
                        {
                            File.Delete(file);
                            ForgetHistory(item.Id);
                        }
                    }
                    else if (now - File.GetLastWriteTime(file) > TrashRetentionPeriod)
                    {
                        File.Delete(file);
                        if (TryIdFromTrashFile(file, out var id)) ForgetHistory(id);
                    }
                }
                catch
                {
                    if (now - File.GetLastWriteTime(file) > TrashRetentionPeriod)
                    {
                        try
                        {
                            File.Delete(file);
                            if (TryIdFromTrashFile(file, out var id)) ForgetHistory(id);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    public void ClearTrash()
    {
        try
        {
            if (Directory.Exists(TrashFolder))
            {
                var files = Directory.GetFiles(TrashFolder, "note_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        if (TryIdFromTrashFile(file, out var id)) ForgetHistory(id);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public void OpenTrashFolderInExplorer()
    {
        try
        {
            Directory.CreateDirectory(TrashFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = TrashFolder,
                UseShellExecute = true
            });
        }
        catch { }
    }


    private void CreateDefaultWelcomeNotes()
    {
        // 1. Main Welcome Note
        _notes.Add(new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "Welcome to SmartNotes ⚡",
            Content = "📌 Stuck to your Desktop!\n\nThis note stays right on your wallpaper beneath all other windows and programs. Open Chrome, games, or work apps — your desktop stays clean and your notes are always there when you need them!\n\n✨ Quick Features:\n• Click + in the top bar to spawn a new note\n• Click 🎨 to switch vibrant neon themes\n• Toggle between Text & Todo Checklist\n• 🔒 Lock note to prevent edits\n• 📌 Switch between Desktop Stuck & Floating",
            X = 80,
            Y = 80,
            Width = 350,
            Height = 380,
            FontSize = 16.0,
            ColorKey = "Amber",
            PinMode = NotePinMode.DesktopStuck,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        });

        // 2. Interactive Checklist Note
        var todoNote = new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "Today's Priorities 🚀",
            IsChecklistMode = true,
            Checklist = new List<TodoCheckItem>
            {
                new() { Text = "Explore SmartNotes desktop sticking", IsDone = true },
                new() { Text = "Customize note color & opacity", IsDone = false },
                new() { Text = "Try global shortcut (Win+Alt+N)", IsDone = false },
                new() { Text = "Pin important reminder", IsDone = false }
            },
            X = 460,
            Y = 80,
            Width = 340,
            Height = 340,
            FontSize = 16.0,
            ColorKey = "Emerald",
            PinMode = NotePinMode.DesktopStuck,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
        _notes.Add(todoNote);
    }
}
