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

    private readonly List<NoteItem> _notes = new();
    private readonly object _lock = new();

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
                if (File.Exists(NotesFilePath))
                {
                    string json = File.ReadAllText(NotesFilePath);
                    var list = JsonSerializer.Deserialize<List<NoteItem>>(json);
                    if (list != null && list.Count > 0)
                    {
                        _notes.AddRange(list);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading notes: {ex.Message}");
            }

            // If completely empty, create initial welcome notes demonstrating capabilities!
            if (_notes.Count == 0)
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
                if (permanent)
                {
                    _notes.Remove(existing);
                }
                else
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = DateTime.Now;
                }
                Save();
            }
        }
    }

    public void RestoreNote(Guid id)
    {
        lock (_lock)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == id);
            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.ModifiedAt = DateTime.Now;
                Save();
            }
        }
    }

    public void ClearTrash()
    {
        lock (_lock)
        {
            _notes.RemoveAll(n => n.IsDeleted);
            Save();
        }
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
