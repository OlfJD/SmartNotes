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
    public static void Snap_ShapedSkins_LineUpTheirVisibleEdges()
    {
        // Medieval scrolls draw 9 px inside their windows on the left and right
        var inset = new Win32Api.RECT(9, 2, 9, 2);
        var target = ScreenRects.Deflate(Rect(500, 100), inset);       // what the other note shows
        var moving = Rect(200, 140);                                    // window; visible right edge at 491
        var snapped = ScreenRects.Inflate(MagneticSnapEngine.CalculateSnap(ScreenRects.Deflate(moving, inset), new[] { target }, null, threshold: 22), inset);
        Assert.Equal(target.left, snapped.right - inset.right, "the visible edges touch (no 18 px gap between the scrolls)");
        Assert.Equal(300, snapped.Width, "window size unchanged");
        Assert.True(NoteClusterEngine.AreNotesAdjacent(ScreenRects.Deflate(snapped, inset), target), "and count as docked");
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
    public static void Screens_NotesOnAScreenStayWhereTheyAre()
    {
        var main = Rect(0, 0, 1920, 1040);
        Assert.True(ScreenPlacement.Rescue(Rect(100, 100, 340, 320), new[] { main }) == null, "fully on screen");
        Assert.True(ScreenPlacement.Rescue(Rect(1800, 100, 340, 320), new[] { main }) == null, "120 px of the title strip still on screen");
    }

    [Test]
    public static void Screens_LostNotesComeBackToTheNearestScreen()
    {
        var main = Rect(0, 0, 1920, 1040);
        var right = Rect(1920, 0, 1280, 700); // a smaller second screen: there is a gap below it

        var inGap = ScreenPlacement.Rescue(Rect(2000, 800, 340, 320), new[] { main, right });
        Assert.NotNull(inGap, "a note in the gap below the smaller screen is moved");
        Assert.True(inGap!.Value.left >= main.left && inGap.Value.right <= main.right && inGap.Value.bottom <= main.bottom, "onto the main screen");
        Assert.Equal(340, inGap.Value.Width, "width kept");

        var unplugged = ScreenPlacement.Rescue(Rect(-2500, 300, 340, 320), new[] { main });
        Assert.Equal(0, unplugged!.Value.left, "a note from an unplugged left screen comes in at the left edge");
        Assert.Equal(300, unplugged.Value.top, "and keeps its height on screen");

        var titleAbove = ScreenPlacement.Rescue(Rect(400, -30, 340, 320), new[] { main });
        Assert.Equal(0, titleAbove!.Value.top, "a title bar above the screen is brought down");

        var huge = ScreenPlacement.Rescue(Rect(5000, 50, 2500, 300), new[] { main });
        Assert.Equal(0, huge!.Value.left, "a note wider than the screen starts at its left edge");
    }

    [Test]
    public static void Arrange_KeepsSizesAndFillsRows()
    {
        var area = new System.Windows.Rect(0, 0, 1000, 800);
        var sizes = new[] { new System.Windows.Size(340, 320), new System.Windows.Size(500, 300), new System.Windows.Size(300, 200) };
        var spots = NoteArranger.Layout(sizes, area);

        Assert.Equal(new System.Windows.Point(40, 40), spots[0], "first group at the margin");
        Assert.Equal(new System.Windows.Point(400, 40), spots[1], "second next to it");
        Assert.Equal(new System.Windows.Point(40, 380), spots[2], "third on the next row (the first row is full)");
        for (int i = 0; i < spots.Count; i++)
        for (int j = i + 1; j < spots.Count; j++)
        {
            var shared = System.Windows.Rect.Intersect(new System.Windows.Rect(spots[i], sizes[i]), new System.Windows.Rect(spots[j], sizes[j]));
            Assert.True(shared.IsEmpty || shared.Width * shared.Height == 0, $"groups {i} and {j} must not overlap");
        }
    }

    [Test]
    public static void Arrange_StartsAgainOffsetWhenTheScreenIsFull()
    {
        var area = new System.Windows.Rect(0, 0, 800, 500);
        var sizes = Enumerable.Repeat(new System.Windows.Size(340, 320), 3).ToArray();
        var spots = NoteArranger.Layout(sizes, area);
        Assert.Equal(new System.Windows.Point(400, 40), spots[1], "two fit side by side");
        Assert.Equal(new System.Windows.Point(40 + NoteArranger.Cascade, 40 + NoteArranger.Cascade), spots[2],
            "the third would go below the screen, so it starts again on top, shifted");
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
    public static void Strings_PluralsAreCorrectInBothLanguages()
    {
        var loc = LocalizationService.Instance;
        string before = loc.CurrentLanguage;
        try
        {
            loc.SetLanguage("de");
            Assert.Equal("SmartNotes - 1 Notiz auf dem Desktop", LocalizationService.T("Tray_TooltipCount", 1, LocalizationService.PluralSuffix(1)), "one note");
            Assert.Equal("SmartNotes - 3 Notizen auf dem Desktop", LocalizationService.T("Tray_TooltipCount", 3, LocalizationService.PluralSuffix(3)), "three notes (was 'Notizn')");
            Assert.Equal("0 Notizen im Papierkorb", LocalizationService.T("Trash_CountFormat", 0, LocalizationService.PluralSuffix(0)), "empty trash (was 'Notizs')");
            loc.SetLanguage("en");
            Assert.Equal("SmartNotes - 3 sticky notes on desktop", LocalizationService.T("Tray_TooltipCount", 3, LocalizationService.PluralSuffix(3)), "English plural");
            Assert.Equal("1 Note in Trash", LocalizationService.T("Trash_CountFormat", 1, LocalizationService.PluralSuffix(1)), "English singular");
        }
        finally
        {
            loc.SetLanguage(before);
        }
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
    public static void Updates_DownloadMustMatchGitHubsChecksum()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("SmartNotes release zip");
        string digest = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
        Assert.True(UpdateService.MatchesDigest(data, digest), "matching checksum");
        Assert.False(UpdateService.MatchesDigest(data.Concat(new byte[] { 1 }).ToArray(), digest), "a changed download is refused");
        Assert.True(UpdateService.MatchesDigest(data, null), "older releases without a checksum still update");
        Assert.True(UpdateService.MatchesDigest(data, "md5:abc"), "an unknown algorithm does not block updates");
    }

    [Test]
    public static void Updates_ReadOnlyFoldersAreDetected()
    {
        Assert.True(UpdateService.IsFolderWritable(TestData.UnitDataDir), "a normal folder");
        Assert.False(UpdateService.IsFolderWritable(Path.Combine(TestData.UnitDataDir, "missing")), "a missing folder");
        bool elevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        if (!elevated)
        {
            Assert.False(UpdateService.IsFolderWritable(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)), "Program Files");
        }
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
