using System.Windows.Automation;
using SmartNotes.Core.Models;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>The real app, driven like a user would (see AppDriver). Run with --e2e.</summary>
public static class EndToEndTests
{
    private static NoteItem Note(string title, string content = "", double x = 140) =>
        new() { Title = title, Content = content, X = x, Y = 160, Width = 340, Height = 320 };

    [Test(E2E = true)]
    public static void Edits_AreSaved_AndShownAgainAfterRestart()
    {
        var first = Note("First");
        using var app = Start(nameof(Edits_AreSaved_AndShownAgainAfterRestart), new[] { first, Note("Second", x: 520) });
        app.WaitForNoteWindows(2);

        SetText(Find(app.NoteWithTitle("First"), "TxtContent"), "hello world");
        Assert.Eventually(() => app.ReadNotes().Single(n => n.Id == first.Id).Content == "hello world", "edit written to notes.json");

        app.Restart();
        app.WaitForNoteWindows(2);
        Assert.Equal("hello world", GetText(Find(app.NoteWithTitle("First"), "TxtContent")), "content after restart");
    }

    [Test(E2E = true)]
    public static void NewNoteButton_CreatesASavedNote()
    {
        using var app = Start(nameof(NewNoteButton_CreatesASavedNote), new[] { Note("Only") });
        var w = app.WaitForNoteWindows(1)[0];
        Invoke(Find(w, "BtnNewNote"));
        app.WaitForNoteWindows(2);
        Assert.Eventually(() => app.ReadNotes().Count == 2, "two notes in notes.json");
    }

    [Test(E2E = true)]
    public static void ChecklistMode_TurnsLinesIntoTasks()
    {
        var note = Note("Shopping", "milk\neggs");
        using var app = Start(nameof(ChecklistMode_TurnsLinesIntoTasks), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];
        Invoke(Find(w, "BtnChecklistMode"));
        Assert.Eventually(() =>
        {
            var saved = app.ReadNotes().Single();
            return saved.ViewMode == NoteViewMode.Checklist && string.Join("|", saved.Checklist.Select(c => c.Text)) == "milk|eggs";
        }, "checklist with two tasks saved");
    }

    [Test(E2E = true)]
    public static void DeleteButton_MovesTheNoteToTheTrash()
    {
        var doomed = Note("Doomed");
        using var app = Start(nameof(DeleteButton_MovesTheNoteToTheTrash), new[] { doomed, Note("Stays", x: 520) });
        app.WaitForNoteWindows(2);
        Invoke(Find(app.NoteWithTitle("Doomed"), "BtnClose"));
        app.WaitForNoteWindows(1);
        Assert.Eventually(() => !app.ReadNotes().Any(n => n.Id == doomed.Id), "gone from notes.json");
        Assert.True(File.Exists(app.PathIn($"trash\\note_{doomed.Id:N}.json")), "kept in the trash folder");
    }

    [Test(E2E = true)]
    public static void LockedNote_TasksAndCopyRowsCannotBeEdited()
    {
        var note = Note("Locked list");
        note.ViewMode = NoteViewMode.Checklist;
        note.IsLocked = true;
        note.Checklist = new() { new TodoCheckItem { Text = "keep" }, new TodoCheckItem { Text = "as is" } };
        using var app = Start(nameof(LockedNote_TasksAndCopyRowsCannotBeEdited), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        Assert.True(IsReadOnly(Find(w, "TxtTitle")), "precondition: the locked note's title is read-only");
        var list = Find(w, "ChecklistItemsControl");
        var taskBoxes = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)).Cast<AutomationElement>().ToList();
        Assert.Equal(2, taskBoxes.Count, "task text boxes found");
        Assert.True(taskBoxes.All(IsReadOnly), "task text of a locked note must be read-only");
        var deleteButtons = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>().ToList();
        Assert.True(deleteButtons.Count == 2 && deleteButtons.All(b => !b.Current.IsEnabled), "delete-task buttons of a locked note must be disabled");
    }

    [Test(E2E = true)]
    public static void Export_WorksForTitlesWithColons()
    {
        var note = Note("To-do: today", "buy milk");
        using var app = Start(nameof(Export_WorksForTitlesWithColons), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];
        Invoke(Find(w, "BtnMore"));
        app.InvokeMenuItem("Export as Markdown (.md)");

        AutomationElement? dialog = null, error = null;
        Assert.Eventually(() =>
        {
            dialog = app.WindowOfClass("#32770");
            error = app.WindowContaining("BtnPrimary");
            return dialog != null || error != null;
        }, "a save dialog or an error message");

        if (error != null)
        {
            string text = string.Join(" / ", error.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                .Cast<AutomationElement>().Select(t => t.Current.Name));
            Invoke(Find(error, "BtnPrimary"));
            throw new TestFailure("export showed an error instead of the save dialog: " + text);
        }

        var nameBox = Find(dialog!, "1001");
        string suggested = GetText(nameBox);
        string target = app.PathIn("exported.md");
        try
        {
            Assert.True(suggested.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, $"suggested file name is valid, got '{suggested}'");
            SetText(nameBox, target);
            Invoke(Find(dialog!, "1"));
            Assert.Eventually(() => File.Exists(target), "file written");
            Assert.Contains("# To-do: today", File.ReadAllText(target), "exported title");
            Assert.Contains("buy milk", File.ReadAllText(target), "exported text");
        }
        finally
        {
            // close whatever is still open (an "invalid file name" message, then the dialog itself)
            for (int i = 0; i < 3 && app.WindowOfClass("#32770") is { } open; i++)
            {
                var close = TryFind(open, "2") ?? TryFind(open, "CommandButton_1");
                if (close == null) break;
                Invoke(close);
                Thread.Sleep(400);
            }
        }
    }

    [Test(E2E = true)]
    public static void FailedSave_IsNotReportedAsSaved()
    {
        var note = Note("Sticky");
        using var app = Start(nameof(FailedSave_IsNotReportedAsSaved), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];
        string notesFile = app.PathIn("notes.json");
        File.SetAttributes(notesFile, FileAttributes.ReadOnly); // e.g. a sync or backup tool holding the file
        try
        {
            SetText(Find(w, "TxtContent"), "this change cannot be written");
            var seen = new List<string>();
            Assert.Eventually(() =>
            {
                if (TryFind(w, "TxtTypingStatus") is { } status && status.Current.Name is { Length: > 0 } s && !seen.Contains(s)) seen.Add(s);
                return seen.Any(s => s != "Typing...");
            }, "a save status after the debounce", 4000);
            Assert.False(seen.Contains("Saved"), $"the note said 'Saved' although notes.json could not be written (statuses: {string.Join(", ", seen)})");
        }
        finally
        {
            File.SetAttributes(notesFile, FileAttributes.Normal);
        }
        Assert.Eventually(() => app.ReadNotes().Single().Content == "this change cannot be written", "written once the file is writable again", 12000);
    }

    [Test(E2E = true)]
    public static void TurningHotkeysOff_FromTheNoteMenu_TakesEffect()
    {
        using var app = Start(nameof(TurningHotkeysOff_FromTheNoteMenu_TakesEffect), new[] { Note("Only") },
            new() { ["EnableGlobalHotkeys"] = true });
        var w = app.WaitForNoteWindows(1)[0];

        PressWinAlt('N');
        app.WaitForNoteWindows(2); // precondition: the hotkey works

        Invoke(Find(w, "BtnMore"));
        app.InvokeMenuItem("Settings & Preferences...");
        AutomationElement? settings = null;
        Assert.Eventually(() => (settings = app.WindowContaining("BtnSave")) != null, "settings window");
        ((SelectionItemPattern)Find(settings!, "NavHotkeys").GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        var toggle = (TogglePattern)Find(settings!, "ChkHotkeys").GetCurrentPattern(TogglePattern.Pattern);
        Assert.Equal(ToggleState.On, toggle.Current.ToggleState, "hotkeys switch starts on");
        toggle.Toggle();
        Invoke(Find(settings!, "BtnSave"));
        Assert.Eventually(() => app.WindowContaining("BtnSave") == null, "settings closed");

        PressWinAlt('N');
        Thread.Sleep(1500);
        Assert.Equal(2, app.NoteWindows().Count, "note windows after pressing Win+Alt+N with hotkeys switched off");
    }

    [Test(E2E = true)]
    public static void NewNoteHotkey_WhileNotesAreHidden_ShowsTheNewNote()
    {
        using var app = Start(nameof(NewNoteHotkey_WhileNotesAreHidden_ShowsTheNewNote), new[] { Note("Hidden one") },
            new() { ["EnableGlobalHotkeys"] = true, ["HideAllNotes"] = true });
        Thread.Sleep(3000);
        Assert.Equal(0, app.NoteWindows().Count, "precondition: notes hidden");

        PressWinAlt('N');
        Assert.Eventually(() => app.NoteWindows().Count >= 1, "the new note is visible");
    }

    [Test(E2E = true)]
    public static void DamagedSettings_DoNotLeaveAnInvisibleZombieProcess()
    {
        using var app = Start(nameof(DamagedSettings_DoNotLeaveAnInvisibleZombieProcess), new[] { Note("A"), Note("B", x: 520) },
            rawSettingsJson: """{"LaunchOnStartup":false,"EnableGlobalHotkeys":false,"LastLanguagePromptVersion":"99.0.0","SkinDataVersion":0,"SkinTitleFonts":null,"SkinTextFonts":null}""");
        app.WaitForNoteWindows(2);
    }

    [Test(E2E = true)]
    public static void OneDamagedNote_DoesNotHideTheOthers()
    {
        string json = $$"""[{"Id":"{{Guid.NewGuid()}}","Title":"Damaged","ViewMode":1,"Checklist":null,"X":140,"Y":160},{"Id":"{{Guid.NewGuid()}}","Title":"Fine","X":520,"Y":160}]""";
        using var app = Start(nameof(OneDamagedNote_DoesNotHideTheOthers), rawNotesJson: json);
        app.WaitForNoteWindows(2);
        app.NoteWithTitle("Fine");
    }

    [Test(E2E = true)]
    public static void SecondLaunch_HandsOverToTheRunningCopy()
    {
        using var app = Start(nameof(SecondLaunch_HandsOverToTheRunningCopy), new[] { Note("Only") });
        app.WaitForNoteWindows(1);
        using var second = app.Launch();
        Assert.True(second.WaitForExit(8000), "second copy exits");
        Assert.False(app.Process.HasExited, "first copy keeps running");
        app.WaitForNoteWindows(1);
    }
}
