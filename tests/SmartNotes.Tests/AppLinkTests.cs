using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>App-linked notes: a note that shows only while a chosen program is in front.</summary>
public static class AppLinkTests
{
    [Test]
    public static void AppLink_MatchesTheExeName_IgnoringCaseAndFolder()
    {
        Assert.True(AppLink.Matches("Code.exe", "code.EXE"), "case does not matter");
        Assert.True(AppLink.Matches(@"C:\Tools\Code.exe", "Code.exe"), "a full path counts by its file name");
        Assert.True(AppLink.Matches("Code", "Code.exe"), "the .exe may be left out");
        Assert.False(AppLink.Matches("Code.exe", "Codex.exe"), "another program");
        Assert.False(AppLink.Matches("", "Code.exe"), "not linked");
        Assert.False(AppLink.Matches("Code.exe", null), "nothing known in front");
    }

    [Test]
    public static void AppLink_OnlyLinkedNotesHide()
    {
        var plain = new NoteItem();
        var linked = new NoteItem { LinkedApp = "Code.exe" };
        Assert.True(AppLink.MayShow(plain, "Game.exe"), "a note that is not linked always shows");
        Assert.True(AppLink.MayShow(linked, "Code.exe"), "a linked note shows over its program");
        Assert.False(AppLink.MayShow(linked, "Game.exe"), "and hides over any other");
    }

    [Test]
    public static void ProgramWindows_KnowsWhichProgramAWindowBelongsTo()
    {
        var window = new System.Windows.Window { Title = "SmartNotes test window", Width = 200, Height = 120, ShowActivated = false, Left = -4000 };
        window.Show();
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            Assert.Equal(Path.GetFileName(Environment.ProcessPath!), ProgramWindows.ExeNameOf(hwnd), "this test program's exe");
            Assert.False(ProgramWindows.ListOpenPrograms().Any(p => p.ExeName == Path.GetFileName(Environment.ProcessPath!)),
                "the list leaves out the program asking (SmartNotes itself, when the app asks)");
        }
        finally
        {
            window.Close();
        }
    }

    // ---------------------------------------------------------------- end-to-end

    private static NoteItem Note(string title, double x) => new() { Title = title, X = x, Y = 160, Width = 340, Height = 320 };

    private static string ThisExe => Path.GetFileName(Environment.ProcessPath!);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int max);

    private static string DescribeWindow(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        var cls = new System.Text.StringBuilder(128);
        GetClassName(hwnd, cls, cls.Capacity);
        string process;
        try { process = Process.GetProcessById((int)pid).ProcessName; } catch { process = "?"; }
        return $"{process} / {cls}";
    }

    [Test(E2E = true)]
    public static void AppLinkedNote_ShowsOnlyWhileItsProgramIsInFront()
    {
        // This test program plays the linked program: its cover window is "the game".
        var linked = Note("Linked", 140);
        linked.LinkedApp = ThisExe;
        linked.LinkedAppName = "Test program";
        using var app = Start(nameof(AppLinkedNote_ShowsOnlyWhileItsProgramIsInFront), new[] { linked, Note("Plain", 520) });

        // started while another program (the terminal) is in front: only the plain note is on screen
        app.WaitForNoteWindows(1);
        app.NoteWithTitle("Plain");
        Thread.Sleep(500);
        Assert.Equal(1, app.NoteWindows().Count, "the linked note stays hidden");

        var cover = OpenCoverWindow();
        Process? other = null;
        try
        {
            var coverHandle = new WindowInteropHelper(cover).Handle;
            var note = app.NoteWithTitle("Linked");
            Assert.True(IsAbove(Handle(note), coverHandle), "the linked note is on top of its program");
            PumpUi(300);
            var fg = GetForegroundWindow();
            Assert.True(fg == coverHandle, $"and its program keeps the focus (in front: {DescribeWindow(fg)})");

            // another program comes to the front: the linked note goes away, the plain one stays
            other = OpenOtherProgramWindow();
            Assert.Eventually(() => app.NoteWindows().Count == 1, "the linked note hides over another program", 10000);
            app.NoteWithTitle("Plain");

            // back to the linked program
            cover.Activate();
            PumpUi(300);
            if (GetForegroundWindow() == coverHandle)
            {
                Assert.Eventually(() => app.NoteWindows().Count == 2, "and comes back with its program");
            }
        }
        finally
        {
            try { other?.Kill(); other?.WaitForExit(3000); } catch { }
            cover.Close();
        }
    }

    [Test(E2E = true)]
    public static void AppLink_IsChosenFromTheMenu_AndSaved()
    {
        var plain = Note("Plain", 140);
        using var app = Start(nameof(AppLink_IsChosenFromTheMenu_AndSaved), new[] { plain });
        var w = app.WaitForNoteWindows(1)[0];

        // a window of this test program, so it is in the list of open programs
        var target = new System.Windows.Window { Title = "SmartNotes link target", Width = 260, Height = 160, Left = 900, Top = 500, ShowActivated = false };
        target.Show();
        PumpUi(300);
        try
        {
            string name = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileDescription is { Length: > 0 } d ? d : Path.GetFileNameWithoutExtension(ThisExe);

            Invoke(Find(w, "BtnMore"));
            app.ExpandMenuItem("Show Only With");
            // the list is filled while the submenu opens, so look the row up again until it settles
            Assert.Eventually(() => app.FindMenuItem(name).Current.HelpText == ThisExe, "the program is listed, its exe as the tooltip");
            Invoke(app.FindMenuItem(name));

            Assert.Eventually(() => app.ReadNotes().Single().LinkedApp == ThisExe, "the link is saved");
            Assert.Contains("Only with", GetFooterText(w), "the footer names the program");
            Assert.False(Find(w, "BtnPinMode").Current.IsEnabled, "the pin waits while the note is linked");

            Invoke(Find(w, "BtnMore"));
            app.ExpandMenuItem("Show Only With");
            Invoke(app.FindMenuItem("Always (not linked)"));
            Assert.Eventually(() => app.ReadNotes().Single().LinkedApp == "", "unlinked again");
            Assert.True(Find(w, "BtnPinMode").Current.IsEnabled, "the pin works again");
        }
        finally
        {
            target.Close();
        }
    }

    private static string GetFooterText(System.Windows.Automation.AutomationElement w) => Find(w, "TxtPinIndicator").Current.Name;

    /// <summary>A small window of another program (PowerShell) that takes the front.</summary>
    private static Process OpenOtherProgramWindow()
    {
        const string script = "Add-Type -AssemblyName System.Windows.Forms; $f = New-Object System.Windows.Forms.Form; " +
            "$f.Text = 'SmartNotes test: another program'; $f.Width = 300; $f.Height = 200; $f.StartPosition = 'CenterScreen'; " +
            "$f.Add_Shown({ $f.Activate() }); [void]$f.ShowDialog()";
        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-Command", script }) psi.ArgumentList.Add(a);
        return Process.Start(psi)!;
    }
}
