using System.Text.Json;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>The sync folder: merge rules (newest wins, positions stay per PC, deletions stick) and two real PCs.</summary>
public static class SyncTests
{
    private static readonly DateTime T0 = new(2026, 11, 27, 9, 0, 0);

    private static NoteItem N(Guid id, string text, DateTime modified, double x = 100) =>
        new() { Id = id, Title = "Note", Content = text, ModifiedAt = modified, X = x, Y = 100, Width = 300, Height = 300 };

    private static SyncSnapshot From(string device, params NoteItem[] notes) => new() { DeviceId = device, Notes = notes.ToList() };

    private static readonly Dictionary<Guid, DateTime> NothingDeleted = new();

    [Test]
    public static void Sync_NewerVersionFromAnotherPc_ReplacesTheContent_ButNotThePosition()
    {
        var id = Guid.NewGuid();
        var mine = N(id, "old", T0, x: 50);
        var theirs = N(id, "new", T0.AddMinutes(5), x: 1800);
        var plan = SyncMerge.Plan(new[] { mine }, NothingDeleted, new[] { From("B", theirs) });
        var updated = plan.Updated.Single();
        Assert.Equal("new", updated.Content, "their newer text");
        Assert.Equal(50.0, updated.X, "my position (their screen is laid out differently)");
        Assert.Equal(0, plan.Added.Count + plan.Deleted.Count, "nothing else");
    }

    [Test]
    public static void Sync_OlderVersionFromAnotherPc_ChangesNothing()
    {
        var id = Guid.NewGuid();
        var plan = SyncMerge.Plan(new[] { N(id, "mine", T0.AddMinutes(5)) }, NothingDeleted, new[] { From("B", N(id, "theirs", T0)) });
        Assert.True(plan.IsEmpty, "my newer version stays");
    }

    [Test]
    public static void Sync_ANoteOnlyTheOtherPcHas_IsAdded()
    {
        var theirs = N(Guid.NewGuid(), "from the laptop", T0);
        var plan = SyncMerge.Plan(Array.Empty<NoteItem>(), NothingDeleted, new[] { From("B", theirs) });
        Assert.Equal("from the laptop", plan.Added.Single().Content, "added");
    }

    [Test]
    public static void Sync_Deletions_WinOverOlderEdits_AndLoseToNewerOnes()
    {
        var id = Guid.NewGuid();
        // deleted on the other PC after my last edit: deleted here too
        var deletedThere = new SyncSnapshot { DeviceId = "B", Deleted = { new SyncTombstone { Id = id, DeletedAt = T0.AddMinutes(10) } } };
        Assert.Equal(id, SyncMerge.Plan(new[] { N(id, "x", T0) }, NothingDeleted, new[] { deletedThere }).Deleted.Single(), "deleted here too");
        // edited here after the other PC deleted it: it stays
        Assert.True(SyncMerge.Plan(new[] { N(id, "x", T0.AddMinutes(20)) }, NothingDeleted, new[] { deletedThere }).IsEmpty, "a newer edit keeps it");

        // deleted here: the other PC's older copy doesn't bring it back
        var deletedHere = new Dictionary<Guid, DateTime> { [id] = T0.AddMinutes(10) };
        Assert.True(SyncMerge.Plan(Array.Empty<NoteItem>(), deletedHere, new[] { From("B", N(id, "x", T0)) }).IsEmpty, "stays deleted");
        // ...unless it was edited there after the deletion
        Assert.Equal(1, SyncMerge.Plan(Array.Empty<NoteItem>(), deletedHere, new[] { From("B", N(id, "x", T0.AddMinutes(30))) }).Added.Count, "a newer edit brings it back");
    }

    [Test]
    public static void Sync_ThreePcs_TheNewestVersionWins()
    {
        var id = Guid.NewGuid();
        var plan = SyncMerge.Plan(new[] { N(id, "mine", T0) }, NothingDeleted,
            new[] { From("B", N(id, "B", T0.AddMinutes(3))), From("C", N(id, "C", T0.AddMinutes(7))), From("D", N(id, "D", T0.AddMinutes(1))) });
        Assert.Equal("C", plan.Updated.Single().Content, "the newest of all");
    }

    [Test]
    public static void Sync_ALookChange_CountsToo()
    {
        var id = Guid.NewGuid();
        var mine = N(id, "same", T0);
        var theirs = N(id, "same", T0);
        theirs.SkinKey = "Terminal";
        theirs.ChangedAt = T0.AddMinutes(2); // the other PC switched the theme, the text is unchanged
        var plan = SyncMerge.Plan(new[] { mine }, NothingDeleted, new[] { From("B", theirs) });
        Assert.Equal("Terminal", plan.Updated.Single().SkinKey, "the new theme comes over");
    }

    [Test]
    public static void Sync_WelcomeNotes_StayOnTheirPc_UntilEdited()
    {
        var welcome = new NoteItem { IsSample = true, CreatedAt = T0, ModifiedAt = T0 };
        Assert.True(welcome.IsUntouchedSample, "a fresh welcome note is not synced");
        welcome.ModifiedAt = T0.AddMinutes(3);
        Assert.False(welcome.IsUntouchedSample, "once edited it is a real note");
        Assert.False(new NoteItem { IsSample = true, CreatedAt = T0, ModifiedAt = T0, ChangedAt = T0.AddMinutes(1) }.IsUntouchedSample, "recoloured counts too");
        Assert.False(new NoteItem().Clone().IsSample, "a duplicate is never a sample");
    }

    [Test]
    public static void Sync_FilesAreRead_AndBrokenOnesSkipped()
    {
        string dir = Path.Combine(TestData.UnitDataDir, "syncfolder");
        Directory.CreateDirectory(dir);
        var snapshot = From("B", N(Guid.NewGuid(), "hello", T0));
        snapshot.Deleted.Add(new SyncTombstone { Id = Guid.NewGuid(), DeletedAt = T0 });
        File.WriteAllText(Path.Combine(dir, "notes-B.json"), JsonSerializer.Serialize(snapshot));
        File.WriteAllText(Path.Combine(dir, "notes-C.json"), "{ half written");

        var read = SyncService.ReadSnapshot(Path.Combine(dir, "notes-B.json"));
        Assert.Equal("hello", read?.Notes.Single().Content, "notes");
        Assert.Equal(1, read?.Deleted.Count, "deletions");
        Assert.Equal(null, SyncService.ReadSnapshot(Path.Combine(dir, "notes-C.json")), "a damaged file is skipped, not fatal");
    }

    // ---------------------------------------------------------------- two PCs, one sync folder

    [Test(E2E = true)]
    public static void Sync_TwoPcs_ShareNotes_Edits_AndDeletions()
    {
        string folder = Path.Combine(TestData.Root, "e2e-sync-folder");
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);
        var syncOn = new Dictionary<string, object?> { ["SyncFolder"] = folder };

        var fromA = new NoteItem { Title = "From A", Content = "hello", X = 140, Y = 160, Width = 340, Height = 300 };
        var fromB = new NoteItem { Title = "From B", Content = "hi", X = 560, Y = 160, Width = 340, Height = 300 };
        using var a = Start(nameof(Sync_TwoPcs_ShareNotes_Edits_AndDeletions) + "-A", new[] { fromA }, syncOn);
        a.WaitForNoteWindows(1);
        using var b = Start(nameof(Sync_TwoPcs_ShareNotes_Edits_AndDeletions) + "-B", new[] { fromB }, syncOn, alongsideOthers: true);

        // each PC gets the other's note
        Assert.Eventually(() => b.ReadNotes().Any(n => n.Id == fromA.Id), "B got A's note", 20000);
        Assert.Eventually(() => a.ReadNotes().Any(n => n.Id == fromB.Id), "A got B's note", 20000);
        b.WaitForNoteWindows(2, 10000);

        // an edit on A shows up on B, in the open window
        SetText(Find(a.NoteWithTitle("From A"), "TxtContent"), "changed on A");
        Assert.Eventually(() => GetText(Find(b.NoteWithTitle("From A"), "TxtContent")) == "changed on A", "B shows A's edit", 20000);

        // a deletion on A sends the note to B's trash
        Invoke(Find(a.NoteWithTitle("From A"), "BtnClose"));
        Assert.Eventually(() => b.ReadNotes().All(n => n.Id != fromA.Id), "deleted on B too", 20000);
        Assert.True(File.Exists(b.PathIn($"trash\\note_{fromA.Id:N}.json")), "and in B's trash, so it can be restored");
        Assert.True(Directory.GetDirectories(a.PathIn("backups"), "*before-sync*").Length == 1, "the notes were backed up before the first sync");
    }
}
