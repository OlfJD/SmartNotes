using System.Text.Json;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;

namespace SmartNotes.Tests;

/// <summary>Earlier versions of notes (NoteHistoryService) and how they follow the trash.</summary>
public static class HistoryTests
{
    private static NoteVersion Version(string content, DateTime? at = null) => new() { Content = content, SavedAt = at ?? DateTime.Now };

    [Test]
    public static void Versions_AreKeptNewestFirst_WithoutDuplicates()
    {
        var id = Guid.NewGuid();
        Assert.Equal(0, NoteHistoryService.GetVersions(id).Count, "no history yet");

        NoteHistoryService.AddVersion(id, Version("first", DateTime.Now.AddHours(-2)));
        NoteHistoryService.AddVersion(id, Version("second", DateTime.Now.AddHours(-1)));
        NoteHistoryService.AddVersion(id, Version("second", DateTime.Now)); // same content as the newest: not kept twice

        var versions = NoteHistoryService.GetVersions(id);
        Assert.Equal("second|first", string.Join("|", versions.Select(v => v.Content)), "newest first, no duplicate");
        Assert.True(File.Exists(Path.Combine(NoteHistoryService.Folder, $"note_{id:N}.json")), "one file per note in history\\");
    }

    [Test]
    public static void Versions_KeepTasksAndCopyRows()
    {
        var id = Guid.NewGuid();
        NoteHistoryService.AddVersion(id, new NoteVersion
        {
            Title = "List",
            ViewMode = NoteViewMode.Checklist,
            Checklist = new() { new TodoCheckItem { Text = "Milk", IsDone = true } },
            CopyList = new() { new CopySnippetItem { Text = "IBAN" } },
        });
        var v = NoteHistoryService.GetVersions(id).Single();
        Assert.Equal(NoteViewMode.Checklist, v.ViewMode, "mode");
        Assert.True(v.Checklist.Single().Text == "Milk" && v.Checklist.Single().IsDone, "task and its tick");
        Assert.Equal("IBAN", v.CopyList.Single().Text, "copy row");
    }

    [Test]
    public static void Versions_AreLimitedTo20_AndToThe30LastDays()
    {
        var id = Guid.NewGuid();
        NoteHistoryService.AddVersion(id, Version("ancient", DateTime.Now.AddDays(-31)));
        for (int i = 0; i < 25; i++) NoteHistoryService.AddVersion(id, Version($"v{i}", DateTime.Now.AddMinutes(-100 + i)));

        var versions = NoteHistoryService.GetVersions(id);
        Assert.Equal(NoteHistoryService.MaxVersions, versions.Count, "at most 20 versions");
        Assert.Equal("v24", versions[0].Content, "the newest is kept");
        Assert.False(versions.Any(v => v.Content == "ancient" || v.Content == "v0"), "the oldest and anything over 30 days are dropped");
    }

    private static NoteItem TrashedNote(NoteStorageService storage, string title, double hoursAgo)
    {
        var note = new NoteItem { Title = title, Content = title };
        storage.AddNote(note);
        storage.DeleteNote(note.Id);
        if (hoursAgo > 0)
        {
            string file = TestData.PathIn($"trash\\note_{note.Id:N}.json");
            var trashed = JsonSerializer.Deserialize<NoteItem>(File.ReadAllText(file))!;
            trashed.DeletedAt = DateTime.Now.AddHours(-hoursAgo);
            File.WriteAllText(file, JsonSerializer.Serialize(trashed));
        }
        return note;
    }

    [Test]
    public static void EmptyingTheTrash_RemovesTheHistoryOfThoseNotesOnly()
    {
        var storage = new NoteStorageService();
        var active = new NoteItem { Title = "Active" };
        storage.AddNote(active);
        var trashed = TrashedNote(storage, "Trashed", 0);
        NoteHistoryService.AddVersion(active.Id, Version("active before"));
        NoteHistoryService.AddVersion(trashed.Id, Version("trashed before"));

        Assert.Equal(1, NoteHistoryService.GetVersions(trashed.Id).Count, "a note in the trash keeps its history");
        storage.ClearTrash();
        Assert.Equal(0, NoteHistoryService.GetVersions(trashed.Id).Count, "emptying the trash removes its history");
        Assert.Equal(1, NoteHistoryService.GetVersions(active.Id).Count, "active notes keep theirs");
    }

    [Test]
    public static void ExpiredTrash_RemovesHistory_ButRestoredNotesKeepIt()
    {
        var storage = new NoteStorageService();
        var expired = TrashedNote(storage, "Expired", 49);
        var restored = TrashedNote(storage, "Restored", 1);
        NoteHistoryService.AddVersion(expired.Id, Version("old text"));
        NoteHistoryService.AddVersion(restored.Id, Version("old text"));

        storage.RestoreNoteFromTrash(restored.Id);
        storage.PurgeExpiredTrash();

        Assert.Equal(0, NoteHistoryService.GetVersions(expired.Id).Count, "gone with the expired trash");
        Assert.Equal(1, NoteHistoryService.GetVersions(restored.Id).Count, "a restored note keeps its history");
    }

    [Test]
    public static void ExpiredTrash_NeverRemovesTheHistoryOfAnActiveNote()
    {
        // an interrupted delete can leave a trash copy of a note that is still active
        var storage = new NoteStorageService();
        var note = new NoteItem { Title = "Still here" };
        storage.AddNote(note);
        Directory.CreateDirectory(TestData.PathIn("trash"));
        var stale = JsonSerializer.Deserialize<NoteItem>(JsonSerializer.Serialize(note))!;
        stale.IsDeleted = true;
        stale.DeletedAt = DateTime.Now.AddHours(-60);
        File.WriteAllText(TestData.PathIn($"trash\\note_{note.Id:N}.json"), JsonSerializer.Serialize(stale));
        NoteHistoryService.AddVersion(note.Id, Version("earlier"));

        storage.PurgeExpiredTrash();
        Assert.Equal(1, NoteHistoryService.GetVersions(note.Id).Count, "history of an active note stays");
    }
}
