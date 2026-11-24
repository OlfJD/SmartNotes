using System.Text.Json;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;

namespace SmartNotes.Tests;

/// <summary>Notes, trash, settings and backups on disk (the unit-test data folder is emptied before each test).</summary>
public static class StorageTests
{
    private static NoteItem SampleNote(string title) => new()
    {
        Title = title,
        Content = "body of " + title,
        X = 300, Y = 220, Width = 360, Height = 380,
        Checklist = new() { new TodoCheckItem { Text = "task", IsDone = true } },
        CopyList = new() { new CopySnippetItem { Text = "snippet" } },
        ColorKey = "Violet",
        SkinKey = "Paper",
        PinMode = NotePinMode.AlwaysOnTop,
    };

    private static void WriteNotes(string fileName, params NoteItem[] notes) =>
        File.WriteAllText(TestData.PathIn(fileName), JsonSerializer.Serialize(notes));

    [Test]
    public static void FirstRun_CreatesWelcomeNotes()
    {
        var storage = new NoteStorageService();
        Assert.Equal(2, storage.Notes.Count, "welcome notes on a fresh data folder");
        Assert.True(File.Exists(TestData.PathIn("notes.json")), "notes.json written on first run");
    }

    [Test]
    public static void SaveAndReload_KeepsEveryField()
    {
        var storage = new NoteStorageService();
        var note = SampleNote("Groceries");
        storage.AddNote(note);

        var reloaded = new NoteStorageService().Notes.Single(n => n.Id == note.Id);
        Assert.Equal("Groceries", reloaded.Title, "title");
        Assert.Equal("body of Groceries", reloaded.Content, "content");
        Assert.Equal(300.0, reloaded.X, "x");
        Assert.Equal(380.0, reloaded.Height, "height");
        Assert.Equal("task", reloaded.Checklist.Single().Text, "checklist text");
        Assert.True(reloaded.Checklist.Single().IsDone, "checklist done state");
        Assert.Equal("snippet", reloaded.CopyList.Single().Text, "copy row");
        Assert.Equal("Paper", reloaded.SkinKey, "skin");
        Assert.Equal(NotePinMode.AlwaysOnTop, reloaded.PinMode, "pin mode");
    }

    [Test]
    public static void CorruptNotesFile_IsKeptAside_AndBackupIsUsed()
    {
        File.WriteAllText(TestData.PathIn("notes.json"), "{ this is not json");
        WriteNotes("notes.json.bak", SampleNote("From backup"));

        var storage = new NoteStorageService();

        Assert.Equal("From backup", storage.Notes.Single().Title, "notes recovered from notes.json.bak");
        var corrupt = Directory.GetFiles(TestData.UnitDataDir, "notes.corrupt-*.json");
        Assert.Equal(1, corrupt.Length, "damaged notes.json kept as notes.corrupt-*.json");
        Assert.Equal("{ this is not json", File.ReadAllText(corrupt[0]), "damaged file content untouched");
        Assert.True(Directory.GetDirectories(Path.Combine(TestData.UnitDataDir, "backups"), "*unreadable-notes*").Length == 1,
            "snapshot taken before anything could write");
    }

    [Test]
    public static void CorruptNotesFile_WithoutBackup_DoesNotInventWelcomeNotes()
    {
        File.WriteAllText(TestData.PathIn("notes.json"), "[{ broken");
        var storage = new NoteStorageService();
        Assert.Equal(0, storage.Notes.Count, "no welcome notes when the real file failed to load");
    }

    [Test]
    public static void LockedNotesFile_IsNeverOverwritten_AndMergedLater()
    {
        var original = SampleNote("Original");
        WriteNotes("notes.json", original);

        NoteStorageService storage;
        var newNote = SampleNote("Added while locked");
        using (new FileStream(TestData.PathIn("notes.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            storage = new NoteStorageService(); // retries ~2 s, then gives up without treating the file as corrupt
            Assert.Equal(0, storage.Notes.Count, "nothing readable while locked");
            storage.AddNote(newNote);
        }
        Assert.True(File.Exists(TestData.PathIn("notes.pending.json")), "session notes parked in notes.pending.json");
        Assert.Contains("Original", File.ReadAllText(TestData.PathIn("notes.json")), "locked notes.json left alone");

        storage.AddNote(SampleNote("After unlock"));
        var onDisk = new NoteStorageService().Notes.Select(n => n.Title).ToList();
        Assert.True(onDisk.Contains("Original") && onDisk.Contains("Added while locked") && onDisk.Contains("After unlock"),
            $"merged once readable, got: {string.Join(", ", onDisk)}");
    }

    [Test]
    public static void PendingNotes_FromALockedSession_AreMergedOnTheNextStart()
    {
        var onDisk = SampleNote("On disk");
        WriteNotes("notes.json", onDisk);
        var edited = SampleNote("On disk");
        edited.Id = onDisk.Id;
        edited.Content = "edited while locked";
        edited.ModifiedAt = DateTime.Now.AddMinutes(5);
        var created = SampleNote("Created while locked");
        WriteNotes("notes.pending.json", edited, created);

        var storage = new NoteStorageService();

        Assert.Equal(2, storage.Notes.Count, "notes after merging");
        Assert.Equal("edited while locked", storage.Notes.Single(n => n.Id == onDisk.Id).Content, "newer edit wins");
        Assert.True(storage.Notes.Any(n => n.Id == created.Id), "note created while locked is back");
        Assert.Contains("edited while locked", File.ReadAllText(TestData.PathIn("notes.json")), "merged into notes.json");
        Assert.False(File.Exists(TestData.PathIn("notes.pending.json")), "side file renamed after merging");
        Assert.Equal(1, Directory.GetFiles(TestData.UnitDataDir, "notes.pending.merged-*.json").Length, "kept for reference");
    }

    [Test]
    public static void DeleteNote_GoesToTrash_AndCanBeRestored()
    {
        var storage = new NoteStorageService();
        var note = SampleNote("Temporary");
        storage.AddNote(note);

        storage.DeleteNote(note.Id);
        Assert.False(storage.Notes.Any(n => n.Id == note.Id), "removed from active notes");
        Assert.True(storage.GetDeletedNotes().Any(n => n.Id == note.Id), "listed in the trash");

        var restored = storage.RestoreNoteFromTrash(note.Id);
        Assert.NotNull(restored, "restored note");
        Assert.True(new NoteStorageService().Notes.Any(n => n.Id == note.Id && n.Title == "Temporary"), "back in notes.json");
        Assert.False(storage.GetDeletedNotes().Any(n => n.Id == note.Id), "no longer in the trash");
    }

    [Test]
    public static void Trash_PurgesNotesOlderThan48Hours()
    {
        Directory.CreateDirectory(TestData.PathIn("trash"));
        var old = SampleNote("Old");
        old.IsDeleted = true;
        old.DeletedAt = DateTime.Now.AddHours(-49);
        var recent = SampleNote("Recent");
        recent.IsDeleted = true;
        recent.DeletedAt = DateTime.Now.AddHours(-1);
        File.WriteAllText(TestData.PathIn($"trash\\note_{old.Id:N}.json"), JsonSerializer.Serialize(old));
        File.WriteAllText(TestData.PathIn($"trash\\note_{recent.Id:N}.json"), JsonSerializer.Serialize(recent));

        var deleted = new NoteStorageService().GetDeletedNotes();
        Assert.Equal("Recent", deleted.Single().Title, "only the recent note survives");
    }

    [Test]
    public static void DeleteNote_WhenTrashCannotBeWritten_DoesNotLoseTheNote()
    {
        var storage = new NoteStorageService();
        var note = SampleNote("Precious");
        storage.AddNote(note);

        // The trash folder cannot be created: a plain file sits where it should be
        Directory.Delete(TestData.PathIn("trash"), recursive: true);
        File.WriteAllText(TestData.PathIn("trash"), "not a folder");

        storage.DeleteNote(note.Id);

        var onDisk = JsonSerializer.Deserialize<List<NoteItem>>(File.ReadAllText(TestData.PathIn("notes.json")))!;
        bool stillActive = storage.Notes.Any(n => n.Id == note.Id) && onDisk.Any(n => n.Id == note.Id);
        Assert.True(stillActive, "a note that cannot be moved to the trash must stay a note (in memory and in notes.json)");
    }

    [Test]
    public static void Load_WhenTrashFolderIsBroken_StillLoadsNotes()
    {
        WriteNotes("notes.json", SampleNote("Keep me"));
        File.WriteAllText(TestData.PathIn("trash"), "not a folder");

        var storage = new NoteStorageService();
        Assert.Equal("Keep me", storage.Notes.SingleOrDefault()?.Title, "notes loaded although the trash folder is unusable");

        // and a save in that state must never replace notes.json with less than it had
        storage.AddNote(SampleNote("New"));
        var onDisk = JsonSerializer.Deserialize<List<NoteItem>>(File.ReadAllText(TestData.PathIn("notes.json")))!;
        Assert.True(onDisk.Any(n => n.Title == "Keep me"), "existing note still in notes.json after a save");
    }

    [Test]
    public static void NullEntriesAndNullLists_LoadWithoutLosingNotes()
    {
        var id = Guid.NewGuid();
        File.WriteAllText(TestData.PathIn("notes.json"),
            $$"""[null, {"Id":"{{id}}","Title":"Survivor","Content":null,"Checklist":null,"CopyList":null}]""");

        var storage = new NoteStorageService();

        var note = storage.Notes.SingleOrDefault(n => n.Id == id);
        Assert.NotNull(note, "valid note next to a null entry is loaded");
        Assert.NotNull(note!.Checklist, "null checklist replaced with an empty list");
        Assert.NotNull(note.CopyList, "null copy list replaced with an empty list");
        Assert.NotNull(note.Content, "null text replaced with an empty string");
    }

    [Test]
    public static void CorruptSettings_AreSnapshottedBeforeTheFirstSave()
    {
        File.WriteAllText(TestData.PathIn("settings.json"), "{ nope");
        var settings = new SettingsService();
        Assert.Equal("Amber", settings.Settings.DefaultColorKey, "defaults in use");

        settings.Save();
        var snaps = Directory.GetDirectories(Path.Combine(TestData.UnitDataDir, "backups"), "*unreadable-settings*");
        Assert.Equal(1, snaps.Length, "snapshot of the unreadable settings");
        Assert.Equal("{ nope", File.ReadAllText(Path.Combine(snaps[0], "settings.json")), "original content kept");
    }

    [Test]
    public static void SettingsWithNullCollections_AreRepairedOnLoad()
    {
        File.WriteAllText(TestData.PathIn("settings.json"),
            """{"SkinTitleFonts":null,"SkinTextFonts":null,"DefaultSkinKey":null,"DefaultColorKey":null,"AppLanguage":null,"ProofingLanguage":null}""");
        var s = new SettingsService().Settings;
        Assert.NotNull(s.SkinTitleFonts, "SkinTitleFonts");
        Assert.NotNull(s.SkinTextFonts, "SkinTextFonts");
        Assert.NotNull(s.DefaultSkinKey, "DefaultSkinKey");
        Assert.NotNull(s.DefaultColorKey, "DefaultColorKey");
        Assert.NotNull(s.AppLanguage, "AppLanguage");
        Assert.NotNull(s.ProofingLanguage, "ProofingLanguage");
    }

    [Test]
    public static void AtomicFile_ReplacesContent_AndKeepsOneBackup()
    {
        string path = TestData.PathIn("atomic.txt");
        string bak = TestData.PathIn("atomic.txt.bak");
        AtomicFile.WriteAllText(path, "first", bak);
        AtomicFile.WriteAllText(path, "second", bak);
        Assert.Equal("second", File.ReadAllText(path), "new content");
        Assert.Equal("first", File.ReadAllText(bak), "previous content in the backup");
        Assert.False(File.Exists(path + ".tmp"), "temp file cleaned up");
    }

    [Test]
    public static void Backup_SnapshotOncePerVersion()
    {
        WriteNotes("notes.json", SampleNote("A"));
        string backups = Path.Combine(TestData.UnitDataDir, "backups");

        DataBackupService.SnapshotIfNewVersion(new Version(9, 0, 0));
        DataBackupService.SnapshotIfNewVersion(new Version(9, 0, 0));
        Assert.Equal(1, Directory.GetDirectories(backups).Length, "one snapshot for the same version");

        DataBackupService.SnapshotIfNewVersion(new Version(9, 1, 0));
        Assert.Equal(2, Directory.GetDirectories(backups).Length, "a new snapshot for a new version");
        Assert.True(Directory.GetDirectories(backups, "*before-v9.1.0-from-v9.0.0").Length == 1, "named after both versions");
    }

    [Test]
    public static void Backup_KeepsOnlyTheNewestTenSnapshots()
    {
        WriteNotes("notes.json", SampleNote("A"));
        for (int i = 0; i < 12; i++)
        {
            DataBackupService.Snapshot($"r{i}");
            Thread.Sleep(15); // distinct creation times
        }
        var dirs = Directory.GetDirectories(Path.Combine(TestData.UnitDataDir, "backups"));
        Assert.Equal(10, dirs.Length, "snapshot count after pruning");
        Assert.False(dirs.Any(d => d.EndsWith("-r0") || d.EndsWith("-r1")), "the two oldest were pruned");
    }
}
