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
    public static void FocusAndMoving_DoNotCountAsAnEdit()
    {
        var note = Note("Old note", "unchanged");
        note.ModifiedAt = DateTime.Now.AddDays(-3);
        using var app = Start(nameof(FocusAndMoving_DoNotCountAsAnEdit), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        // click into the text and out again, then move the note: both save the note
        Find(w, "TxtContent").SetFocus();
        Find(w, "TxtTitle").SetFocus();
        var r = w.Current.BoundingRectangle;
        ((TransformPattern)w.GetCurrentPattern(TransformPattern.Pattern)).Move(r.X + 60, r.Y + 40);
        Assert.Eventually(() => app.ReadNotes().Single().X != note.X, "the move was saved");
        Thread.Sleep(800);
        var saved = app.ReadNotes().Single();
        Assert.True(Math.Abs((saved.ModifiedAt - note.ModifiedAt).TotalMinutes) < 1,
            $"modified time unchanged by focus and moving, got {saved.ModifiedAt:g}");

        SetText(Find(w, "TxtContent"), "now edited");
        Assert.Eventually(() => (DateTime.Now - app.ReadNotes().Single().ModifiedAt).TotalMinutes < 1, "a real edit updates the modified time");
    }

    [Test(E2E = true)]
    public static void History_EditAfterAPause_KeepsTheOldText_AndRestoreCanBeUndone()
    {
        var note = Note("Draft", "first draft");
        note.ModifiedAt = DateTime.Now.AddDays(-3); // last edited three days ago
        using var app = Start(nameof(History_EditAfterAPause_KeepsTheOldText_AndRestoreCanBeUndone), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        SetText(Find(w, "TxtContent"), "second draft");
        Assert.Eventually(() => app.ReadNotes().Single().Content == "second draft", "the edit is saved");
        Assert.Eventually(() => app.ReadHistory(note.Id).FirstOrDefault()?.Content == "first draft", "the text before the edit is kept as a version");

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("Earlier Versions");
        var entry = app.FindMenuItem("first draft");
        string day = note.ModifiedAt.ToString("d MMM", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        Assert.True(entry.Current.Name.StartsWith(day + " ", StringComparison.Ordinal), $"labelled with when the note looked like that: '{entry.Current.Name}'");
        Assert.Contains("first draft", entry.Current.HelpText, "hovering shows the whole version");
        Invoke(entry);

        Assert.Eventually(() => GetText(Find(w, "TxtContent")) == "first draft", "the old text is back in the note");
        Assert.Eventually(() => app.ReadNotes().Single().Content == "first draft", "and saved");
        Assert.Equal("second draft", app.ReadHistory(note.Id).First().Content, "the replaced text became a version, so the restore can be undone");
    }

    [Test(E2E = true)]
    public static void History_EditsInTheSameRound_DoNotPileUpVersions()
    {
        var note = Note("Busy", "a");
        note.ModifiedAt = DateTime.Now.AddMinutes(-1); // still the same round of editing
        using var app = Start(nameof(History_EditsInTheSameRound_DoNotPileUpVersions), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        SetText(Find(w, "TxtContent"), "ab");
        Assert.Eventually(() => app.ReadNotes().Single().Content == "ab", "saved");
        SetText(Find(w, "TxtContent"), "abc");
        Assert.Eventually(() => app.ReadNotes().Single().Content == "abc", "saved again");
        Assert.Equal(0, app.ReadHistory(note.Id).Count, "no versions within one round of editing");

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("Earlier Versions");
        Assert.False(app.FindMenuItem("No earlier versions yet").Current.IsEnabled, "the empty list says so");
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
    public static void Checklist_ShowsProgress_AndSavesTicks()
    {
        var note = Note("Tasks");
        note.ViewMode = NoteViewMode.Checklist;
        note.Checklist = new() { new TodoCheckItem { Text = "done already", IsDone = true }, new TodoCheckItem { Text = "still open" } };
        using var app = Start(nameof(Checklist_ShowsProgress_AndSavesTicks), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        Assert.Eventually(() => TryFind(w, "TxtChecklistProgress")?.Current.Name == "1/2", "footer shows 1/2");
        var boxes = Find(w, "ChecklistItemsControl").FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)).Cast<AutomationElement>().ToList();
        Assert.Equal(2, boxes.Count, "task checkboxes");

        // ticked without a mouse click (keyboard / accessibility tools): must still count and be saved
        ((TogglePattern)boxes[1].GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        Assert.Eventually(() => Find(w, "TxtChecklistProgress").Current.Name == "2/2", "footer shows 2/2 after ticking");
        Assert.Eventually(() => app.ReadNotes().Single().Checklist.All(c => c.IsDone), "the tick is saved");
    }

    [Test(E2E = true)]
    public static void CopyAsPicture_PutsTheNoteOnTheClipboard()
    {
        var note = Note("Groceries");
        note.SkinKey = "Paper";
        note.ViewMode = NoteViewMode.Checklist;
        note.Checklist = new() { new TodoCheckItem { Text = "Milk", IsDone = true }, new TodoCheckItem { Text = "Bread" } };
        using var app = Start(nameof(CopyAsPicture_PutsTheNoteOnTheClipboard), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        string? usersText = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;
        try
        {
            // the confirmation below only appears after a successful copy, so an older picture can't pass the test
            Invoke(Find(w, "BtnMore"));
            app.InvokeMenuItem("Copy as Picture");
            Assert.Eventually(() => TryFind(w, "TxtTypingStatus")?.Current.Name == "Picture copied", "the note confirms the copy");

            MemoryStream? png = null;
            Assert.Eventually(() => System.Windows.Clipboard.ContainsData("PNG") &&
                (png = System.Windows.Clipboard.GetData("PNG") as MemoryStream) != null, "a PNG on the clipboard");
            var frame = new System.Windows.Media.Imaging.PngBitmapDecoder(png!,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            Assert.True(frame.PixelWidth >= 2 * 340 - 4, $"rendered at least at 2x: {frame.PixelWidth} px wide");
            Assert.True(Math.Abs(frame.PixelWidth / (double)frame.PixelHeight - 340.0 / 320.0) < 0.05,
                $"same shape as the note: {frame.PixelWidth}x{frame.PixelHeight}");
            Assert.True(System.Windows.Clipboard.ContainsImage(), "a plain image as well, for older programs");
            File.WriteAllBytes(Path.Combine(TestData.Root, "copied-note.png"), png!.ToArray()); // for a look by eye
        }
        finally
        {
            Thread.Sleep(500); // let Windows' clipboard history finish reading the picture
            try { if (usersText != null) System.Windows.Clipboard.SetText(usersText); } catch { /* best effort */ }
        }
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
        Assert.False(Find(w, "BtnClose").Current.IsEnabled, "a locked note cannot be deleted with its × button");
        Assert.Equal("Locked: unlock the note to delete it", Find(w, "BtnClose").Current.HelpText, "the × button explains why");

        Invoke(Find(w, "BtnMore"));
        Assert.False(app.FindMenuItem("Earlier Versions").Current.IsEnabled, "earlier versions can't be restored into a locked note");
        Assert.False(app.FindMenuItem("Delete Note").Current.IsEnabled, "and it can't be deleted from the menu either");

        // unlocking makes it deletable again
        Invoke(Find(w, "BtnLock"));
        Assert.Eventually(() => Find(w, "BtnClose").Current.IsEnabled, "× enabled after unlocking");
    }

    [Test(E2E = true)]
    public static void DesktopStickingSwitch_DecidesHowNewNotesStart()
    {
        using var app = Start(nameof(DesktopStickingSwitch_DecidesHowNewNotesStart), new[] { Note("Only") });
        var w = app.WaitForNoteWindows(1)[0];
        Invoke(Find(w, "BtnMore"));
        app.InvokeMenuItem("Settings & Preferences...");
        AutomationElement? settings = null;
        Assert.Eventually(() => (settings = app.WindowContaining("BtnSave")) != null, "settings window");
        var stuck = (TogglePattern)Find(settings!, "ChkDesktopStuck").GetCurrentPattern(TogglePattern.Pattern);
        Assert.Equal(ToggleState.On, stuck.Current.ToggleState, "on by default: new notes stick to the desktop");
        stuck.Toggle();
        Invoke(Find(settings!, "BtnSave"));
        Assert.Eventually(() => app.WindowContaining("BtnSave") == null, "settings closed");
        Assert.Eventually(() => File.ReadAllText(app.PathIn("settings.json")).Contains("\"DefaultPinMode\": 1"), "saved as 'always on top' for new notes");
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
            // Like a user: go to the folder first (Save with a folder name opens it), then save under a plain name
            SetText(nameBox, app.DataDir);
            Invoke(Find(dialog!, "1"));
            Thread.Sleep(1200);
            SetText(Find(dialog!, "1001"), "exported.md");
            Invoke(Find(dialog!, "1"));
            try
            {
                Assert.Eventually(() => File.Exists(target), "file written");
            }
            catch (TestFailure)
            {
                throw new TestFailure($"file not written; windows: {app.DescribeWindows()}");
            }
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
    public static void Settings_CancelDoesNotKeepThePreviewedLanguage()
    {
        using var app = Start(nameof(Settings_CancelDoesNotKeepThePreviewedLanguage), new[] { Note("Only") });
        var w = app.WaitForNoteWindows(1)[0];
        string english = Find(w, "BtnNewNote").Current.HelpText;

        Invoke(Find(w, "BtnMore"));
        app.InvokeMenuItem("Settings & Preferences...");
        AutomationElement? settings = null;
        Assert.Eventually(() => (settings = app.WindowContaining("BtnSave")) != null, "settings window");
        var combo = Find(settings!, "CmbAppLanguage");
        var expand = (ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        expand.Expand();
        AutomationElement? german = null;
        Assert.Eventually(() => (german = combo.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Deutsch (German)"))) != null, "German in the list");
        ((SelectionItemPattern)german!.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        expand.Collapse();
        Assert.Eventually(() => Find(w, "BtnNewNote").Current.HelpText != english, "precondition: the note previews German");

        Invoke(Find(settings!, "BtnCancel"));
        Assert.Eventually(() => app.WindowContaining("BtnSave") == null, "settings closed");
        Assert.Eventually(() => Find(w, "BtnNewNote").Current.HelpText == english, "note back in English after Cancel");
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
    public static void TakenHotkey_ByAnotherSmartNotes_IsQuiet_AndTakenOverLater()
    {
        // Two copies with separate data folders run side by side; only the first can own Win+Alt+N
        using var first = Start("HotkeyOwner", new[] { Note("A") }, new() { ["EnableGlobalHotkeys"] = true });
        first.WaitForNoteWindows(1);
        Assert.Eventually(() => File.ReadAllText(first.PathIn("startup.log")).Contains("Shortcut NewNote is on Win+Alt+N"), "the first copy owns Win+Alt+N");
        using var second = Start("HotkeyLate", new[] { Note("B") }, new() { ["EnableGlobalHotkeys"] = true }, alongsideOthers: true);
        second.WaitForNoteWindows(1);
        Assert.Eventually(() => File.ReadAllText(second.PathIn("startup.log")).Contains("Win+Alt+N is already taken (another SmartNotes is running)"),
            "the second copy logs that another SmartNotes holds it");
        string log = File.ReadAllText(second.PathIn("startup.log"));
        Assert.False(log.Contains("Told the user"), "and says nothing: the other copy is SmartNotes, not some program");
        Assert.False(log.Contains("Shortcut NewNote is on Win+Alt+J"), "it does not grab the spare key either");

        // once the first copy is gone, the second takes the shortcuts over (it checks every 20 s)
        first.Exit();
        Assert.Eventually(() => File.ReadAllText(second.PathIn("startup.log")).Contains("Shortcut NewNote is on Win+Alt+N"),
            "the second copy took Win+Alt+N over", 35000);
        Assert.Equal("", ReportedConflicts(second), "nothing to report");
    }

    private static string ReportedConflicts(AppDriver app)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(app.PathIn("settings.json")));
        return doc.RootElement.TryGetProperty("ReportedHotkeyConflicts", out var v) ? v.GetString() ?? "" : "";
    }

    [Test(E2E = true)]
    public static void NoteLeftOffScreen_ComesBackWhenTheScreensChange()
    {
        var note = Note("Lost");
        using var app = Start(nameof(NoteLeftOffScreen_ComesBackWhenTheScreensChange), new[] { note });
        IntPtr hwnd = Handle(app.WaitForNoteWindows(1)[0]);

        MoveWindow(hwnd, 30000, 200); // as if its monitor had been unplugged
        Assert.Eventually(() => !TitleOnAScreen(hwnd), "precondition: the note is off-screen");

        SendDisplayChange(hwnd);
        Assert.Eventually(() => TitleOnAScreen(hwnd), "the note is back on a screen after the display change");
        Assert.Eventually(() => app.ReadNotes().Single().X < 10000, "its new position is saved");
    }

    [Test(E2E = true)]
    public static void ArrangeNotes_KeepsSizesStacksAndLockedNotes()
    {
        var a = Note("A", x: 100);
        var b = Note("B", x: 440);            // docked to the right of A (100 + 340)
        var big = Note("Big", x: 900);
        big.Y = 600; big.Width = 500; big.Height = 420;
        var locked = Note("Locked", x: 1300);
        locked.Y = 100; locked.IsLocked = true;
        using var app = Start(nameof(ArrangeNotes_KeepsSizesStacksAndLockedNotes), new[] { a, b, big, locked });
        app.WaitForNoteWindows(4);

        app.OpenTrayMenu();
        app.InvokeMenuItem("Arrange Notes on Desktop");

        var work = System.Windows.SystemParameters.WorkArea;
        Assert.Eventually(() =>
        {
            var n = app.ReadNotes().ToDictionary(x => x.Title);
            return Math.Abs(n["A"].X - (work.Left + 40)) < 2 && Math.Abs(n["Big"].Y - (work.Top + 40)) < 2;
        }, "notes moved into rows");
        var notes = app.ReadNotes().ToDictionary(x => x.Title);
        Assert.Equal(500.0, notes["Big"].Width, "the big note keeps its width (arrange used to make every note 340 wide)");
        Assert.Equal(420.0, notes["Big"].Height, "and its height");
        Assert.True(Math.Abs(notes["B"].X - (notes["A"].X + notes["A"].Width)) < 2 && Math.Abs(notes["B"].Y - notes["A"].Y) < 2,
            "the docked stack A+B stays together");
        Assert.Equal(1300.0, notes["Locked"].X, "the locked note stays where it is");
    }

    [Test(E2E = true)]
    public static void ShowHideHotkey_HidesAndShowsAllNotes()
    {
        using var app = Start(nameof(ShowHideHotkey_HidesAndShowsAllNotes), new[] { Note("One"), Note("Two", x: 520) },
            new() { ["EnableGlobalHotkeys"] = true });
        app.WaitForNoteWindows(2);
        PressWinAlt('S');
        app.WaitForNoteWindows(0);
        PressWinAlt('S');
        app.WaitForNoteWindows(2);
    }

    [Test(E2E = true)]
    public static void BringForwardHotkey_PutsNotesAboveOpenWindows()
    {
        using var app = Start(nameof(BringForwardHotkey_PutsNotesAboveOpenWindows), new[] { Note("Behind") },
            new() { ["EnableGlobalHotkeys"] = true });
        IntPtr note = Handle(app.WaitForNoteWindows(1)[0]);
        var cover = OpenCoverWindow();
        try
        {
            IntPtr coverHandle = new System.Windows.Interop.WindowInteropHelper(cover).Handle;
            Assert.True(IsAbove(coverHandle, note), "precondition: the other window covers the stuck-to-desktop note");

            PressWinAlt('A');
            Assert.Eventually(() => { PumpUi(50); return IsAbove(note, coverHandle); }, "Win+Alt+A puts the note above the other window");
        }
        finally
        {
            cover.Close();
        }
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
