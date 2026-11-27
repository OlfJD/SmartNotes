using System.Windows.Automation;
using SmartNotes.Core.Models;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>Menus and messages: nothing only behind a shortcut, SmartNotes' own toasts, the program picker.</summary>
public static class UiTests
{
    private static NoteItem Note(string title, double x) => new() { Title = title, Content = "text", X = x, Y = 160, Width = 340, Height = 320 };

    [Test(E2E = true)]
    public static void AllNotesMenu_ListsEveryNote_TidiesUp_AndShowsAToast()
    {
        using var app = Start(nameof(AllNotesMenu_ListsEveryNote_TidiesUp_AndShowsAToast), new[] { Note("First", 140), Note("Second", 560) });
        var w = app.WaitForNoteWindows(2).First(n => GetText(Find(n, "TxtTitle")) == "First");

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("All Notes");
        Assert.NotNull(app.FindMenuItem("Bring All Notes to Front"), "bring all forward without the shortcut");
        Assert.NotNull(app.FindMenuItem("Hide All Desktop Notes"), "hide all without the shortcut");
        Assert.NotNull(app.FindMenuItem("Second"), "every note is listed by name");
        Invoke(app.FindMenuItem("Tidy Up Notes"));

        // the message is SmartNotes' own toast (a window of the app), no Windows balloon
        AutomationElement? toast = null;
        Assert.Eventually(() => (toast = app.WindowContaining("TxtMessage")) != null, "a SmartNotes toast appears");
        Assert.Contains("arranged", Find(toast!, "TxtMessage").Current.Name, "saying the notes were arranged");
    }

    [Test(E2E = true)]
    public static void ProgramPicker_OpensFromShowOnlyWith_AndSearches()
    {
        using var app = Start(nameof(ProgramPicker_OpensFromShowOnlyWith_AndSearches), new[] { Note("Keys", 140) });
        var w = app.WaitForNoteWindows(1)[0];

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("Show Only With");
        Invoke(app.FindMenuItem("Choose a Program"));
        AutomationElement? picker = null;
        Assert.Eventually(() => (picker = app.WindowContaining("TxtSearch")) != null, "the program picker opens");
        Assert.Eventually(() => Find(picker!, "TxtStatus").Current.Name.Contains("programs"), "installed programs are listed", 15000);
        SetText(Find(picker!, "TxtSearch"), "zzzz-no-such-program");
        Assert.Eventually(() => Find(picker!, "ProgramList").FindAll(TreeScope.Children, Condition.TrueCondition).Count == 0, "search filters the list");
        Invoke(Find(picker!, "BtnCancel"));
        Assert.Eventually(() => app.WindowContaining("TxtSearch") == null, "Cancel closes it");
        Assert.Equal("", app.ReadNotes().Single().LinkedApp, "nothing linked");
    }
}
