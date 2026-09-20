using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

public class NoteStorageService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartNotes"
    );
    private static readonly string NotesFilePath = Path.Combine(AppFolder, "notes.json");
    private static readonly string BackupsFolder = Path.Combine(AppFolder, "backups");
    private static readonly string TrashFolder = Path.Combine(AppFolder, "trash");

    public static readonly TimeSpan TrashRetentionPeriod = TimeSpan.FromHours(48);

    private readonly List<NoteItem> _notes = new();
    private readonly object _lock = new();

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
            try
            {
                Directory.CreateDirectory(AppFolder);
                Directory.CreateDirectory(TrashFolder);

                // Auto-purge any deleted files older than 48 hours
                PurgeExpiredTrash();

                if (File.Exists(NotesFilePath))
                {
                    string json = File.ReadAllText(NotesFilePath);
                    var list = JsonSerializer.Deserialize<List<NoteItem>>(json);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list)
                        {
                            // Migrate legacy inline deleted notes into dedicated 48h trash folder
                            if (item.IsDeleted)
                            {
                                SaveToTrash(item);
                            }
                            else
                            {
                                _notes.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading notes: {ex.Message}");
            }

            // If completely empty, create initial welcome notes demonstrating capabilities!
            if (_notes.Count == 0 && GetDeletedNotes().Count == 0)
            {
                CreateDefaultWelcomeNotes();
                Save();
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                string json = JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(NotesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving notes: {ex.Message}");
            }
        }
    }

    public void AddNote(NoteItem note)
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
            Save();
        }
    }

    public void UpdateNote(NoteItem note)
    {
        lock (_lock)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == note.Id);
            if (existing != null)
            {
                int index = _notes.IndexOf(existing);
                note.ModifiedAt = DateTime.Now;
                _notes[index] = note;
                Save();
            }
        }
    }

    public void DeleteNote(Guid id, bool permanent = false)
    {
        lock (_lock)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == id);
            if (existing != null)
            {
                _notes.Remove(existing);
                Save();

                if (permanent)
                {
                    DeleteFromTrash(id);
                }
                else
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = DateTime.Now;
                    SaveToTrash(existing);
                }
            }
            else if (permanent)
            {
                DeleteFromTrash(id);
            }
        }
    }

    private void SaveToTrash(NoteItem note)
    {
        try
        {
            Directory.CreateDirectory(TrashFolder);
            string filePath = Path.Combine(TrashFolder, $"note_{note.Id:N}.json");
            string json = JsonSerializer.Serialize(note, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving note to trash: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error restoring note: {ex.Message}");
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
                        }
                    }
                    else if (now - File.GetLastWriteTime(file) > TrashRetentionPeriod)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    if (now - File.GetLastWriteTime(file) > TrashRetentionPeriod)
                    {
                        try { File.Delete(file); } catch { }
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
                    try { File.Delete(file); } catch { }
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

    public string ExportAllToJson()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public bool ImportFromJson(string json)
    {
        try
        {
            var imported = JsonSerializer.Deserialize<List<NoteItem>>(json);
            if (imported != null)
            {
                lock (_lock)
                {
                    foreach (var item in imported)
                    {
                        if (!_notes.Any(n => n.Id == item.Id))
                        {
                            _notes.Add(item);
                        }
                    }
                    Save();
                }
                return true;
            }
        }
        catch { }
        return false;
    }

    private void CreateDefaultWelcomeNotes()
    {
        // 1. Main Welcome Note
        _notes.Add(new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "Welcome to SmartNotes ⚡",
            Content = "📌 **Stuck to your Desktop!**\n\nThis note stays right on your wallpaper beneath all other windows and programs. Open Chrome, games, or work apps — your desktop stays clean and your notes are always there when you need them!\n\n✨ **Quick Features:**\n• Click **+** in top bar to spawn a new note\n• Click 🎨 to switch vibrant neon themes\n• Toggle between Text & Todo Checklist\n• 🔒 Lock note to prevent edits\n• 📌 Switch between Desktop Stuck & Floating",
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
