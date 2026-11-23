using System.Reflection;
using System.Text.RegularExpressions;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;

namespace SmartNotes.Tests;

/// <summary>Pure logic: snapping, docking, colours, strings, versions.</summary>
public static class LogicTests
{
    private static Win32Api.RECT Rect(int x, int y, int w = 300, int h = 300) => new(x, y, x + w, y + h);

    [Test]
    public static void Snap_PullsTheNoteOntoANeighboursEdge()
    {
        var target = Rect(500, 100);
        var moving = Rect(185, 140); // right edge at 485: 15 px from the target's left edge
        var snapped = MagneticSnapEngine.CalculateSnap(moving, new[] { target }, null, threshold: 22);
        Assert.Equal(500, snapped.right, "right edge snapped to the neighbour's left edge");
        Assert.Equal(300, snapped.Width, "width unchanged");
    }

    [Test]
    public static void Snap_LeavesFarAwayNotesAlone()
    {
        var moving = Rect(100, 100);
        var snapped = MagneticSnapEngine.CalculateSnap(moving, new[] { Rect(900, 700) }, null, threshold: 22);
        Assert.Equal(100, snapped.left, "left");
        Assert.Equal(100, snapped.top, "top");
    }

    [Test]
    public static void Docking_OnlyTouchingEdgesCount()
    {
        Assert.True(NoteClusterEngine.AreNotesAdjacent(Rect(0, 0), Rect(300, 50)), "side by side");
        Assert.True(NoteClusterEngine.AreNotesAdjacent(Rect(0, 0), Rect(40, 302)), "stacked (2 px gap)");
        Assert.False(NoteClusterEngine.AreNotesAdjacent(Rect(0, 0), Rect(330, 0)), "30 px apart");
        Assert.False(NoteClusterEngine.AreNotesAdjacent(Rect(0, 0), Rect(100, 100)), "overlapping");
        Assert.False(NoteClusterEngine.AreNotesAdjacent(Rect(0, 0), Rect(300, 290)), "corner contact only");
    }

    [Test]
    public static void Colors_UnknownOrBrokenKeysFallBackSafely()
    {
        Assert.Equal("Violet", NoteColorTheme.Get("Violet").Key, "named theme");
        Assert.Equal("#12AB34", NoteColorTheme.Get("#12ab34").PrimaryHex, "custom hex");
        Assert.NotNull(NoteColorTheme.Get("#zzzzzz"), "invalid hex");
        Assert.NotNull(NoteColorTheme.Get("no-such-theme"), "unknown key");
    }

    private static Dictionary<string, Dictionary<string, string>> AllStrings() =>
        (Dictionary<string, Dictionary<string, string>>)typeof(LocalizationService)
            .GetField("_strings", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(LocalizationService.Instance)!;

    [Test]
    public static void Strings_GermanHasEveryEnglishKey_WithTheSamePlaceholders()
    {
        var all = AllStrings();
        var en = all["en"];
        var de = all["de"];
        var missing = en.Keys.Where(k => !de.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "German strings missing: " + string.Join(", ", missing));

        static string Placeholders(string s) => string.Join(",", Regex.Matches(s, @"\{\d+(:[^}]*)?\}").Select(m => m.Value).OrderBy(x => x));
        var mismatched = en.Keys.Where(k => de.ContainsKey(k) && Placeholders(en[k]) != Placeholders(de[k])).ToList();
        Assert.True(mismatched.Count == 0, "placeholders differ between English and German: " + string.Join(", ", mismatched));
    }

    [Test]
    public static void Settings_VersionBadgeShowsTheRealVersion()
    {
        string badge = LocalizationService.T("App_VersionBadge", UpdateService.CurrentVersion.ToString(2));
        Assert.Contains(UpdateService.CurrentVersion.ToString(2), badge, "settings header badge");
    }

    [Test]
    public static void Export_FileNameIsAlwaysValid()
    {
        Assert.Equal("To-do- today.md", NoteExport.SuggestFileName("To-do: today"), "colon");
        Assert.Equal("a-b-c-d.md", NoteExport.SuggestFileName("a/b\\c?d"), "slashes and question mark");
        Assert.Equal("SmartNote.md", NoteExport.SuggestFileName("   "), "empty title");
        Assert.Equal("SmartNote.md", NoteExport.SuggestFileName("..."), "only dots");
        Assert.Equal("CON-note.md", NoteExport.SuggestFileName("CON"), "reserved device name");
        Assert.Equal("Plan.md", NoteExport.SuggestFileName("Plan. "), "trailing dot and space");
        Assert.True(NoteExport.SuggestFileName(new string('x', 300)).Length <= 104, "long titles are shortened");
    }

    [Test]
    public static void Updates_TagsAreParsedLoosely()
    {
        var parse = typeof(UpdateService).GetMethod("TryParseLooseVersion", BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] args = { "2.4", null };
        Assert.True((bool)parse.Invoke(null, args)!, "2.4 parsed");
        Assert.Equal(new Version(2, 4, 0), (Version)args[1]!, "2.4");
        args = new object?[] { "2.5.1-beta", null };
        parse.Invoke(null, args);
        Assert.Equal(new Version(2, 5, 0), (Version)args[1]!, "pre-release suffix");
    }

    [Test]
    public static void Updates_DownloadsAllowSlowConnections()
    {
        // ~2 MB within the API's 15 s limit needs > 1 Mbit/s; downloads must have their own, longer limit
        var field = typeof(UpdateService).GetField("DownloadClient", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field, "a separate HttpClient for downloads");
        var client = (System.Net.Http.HttpClient)field!.GetValue(null)!;
        Assert.True(client.Timeout >= TimeSpan.FromMinutes(2), $"download timeout is {client.Timeout}");
    }

    [Test]
    public static void NoteClone_CopiesContentWithNewIds()
    {
        var note = new NoteItem { Title = "T", Content = "C", Checklist = new() { new TodoCheckItem { Text = "x" } } };
        var clone = note.Clone();
        Assert.True(clone.Id != note.Id, "new note id");
        Assert.Equal("T (Copy)", clone.Title, "title");
        Assert.Equal("x", clone.Checklist.Single().Text, "checklist copied");
        Assert.True(clone.Checklist[0].Id != note.Checklist[0].Id, "new item ids");
    }
}
