using System.Diagnostics;
using System.Windows;
using SmartNotes.Core.Models;
using SmartNotes.UI.Skins;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>
/// <c>dotnet run --project tests\SmartNotes.Tests -- --perf</c>: how long each theme's painter takes, and what the real
/// app costs with ten notes, idle and while a note with a living animation has the focus. Prints a small report.
/// The app part starts the real exe with its own data folder and takes the focus for a moment.
/// </summary>
public static class PerfReport
{
    public static int Run()
    {
        Console.WriteLine("Painters (ms per paint, median of 30):");
        Application.ResourceAssembly ??= typeof(SmartNotes.App).Assembly;
        foreach (var skin in NoteSkin.All.Where(s => s.CustomArt))
        {
            var theme = skin.Tint(NoteColorTheme.Get("Cyan"));
            var note = new NoteItem { Title = "Today", SkinKey = skin.Key };
            foreach (var (w, h) in new[] { (340.0, 300.0), (800.0, 700.0) })
            {
                var times = new List<double>();
                for (int i = 0; i < 30; i++)
                {
                    var sw = Stopwatch.StartNew();
                    SkinArt.Build(skin, w, h, theme, "7F3A", 1.0, 40, note, "Today", new Rect(13, 55, w - 26, 38));
                    times.Add(sw.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                Console.WriteLine($"  {skin.Key,-10} {w}x{h}: {times[15],6:0.00} ms");
            }
        }

        var notes = new List<NoteItem>();
        // SMARTNOTES_PERF_OLDSKINS=1: only themes 2.6.0 has too, to compare with that build (SMARTNOTES_TEST_EXE)
        string[] skins = Environment.GetEnvironmentVariable("SMARTNOTES_PERF_OLDSKINS") == "1"
            ? new[] { "Classic", "Paper", "Cyberpunk", "Medieval", "EldenRing", "Astral", "Classic", "Paper", "Cyberpunk", "Medieval" }
            : new[] { "Classic", "Paper", "Cyberpunk", "Medieval", "EldenRing", "Astral", "Terminal", "Blueprint", "InkWashi", "DeepSea" };
        for (int i = 0; i < skins.Length; i++)
        {
            notes.Add(new NoteItem
            {
                Title = skins[i], Content = "Some text in the note\nand a second line", SkinKey = skins[i], ColorKey = "Cyan",
                X = 60 + (i % 5) * 300, Y = 60 + (i / 5) * 330, Width = 290, Height = 310,
            });
        }
        // as users have it; SMARTNOTES_PERF_LIVING=0 measures without living themes
        using var app = Start("Perf", notes, new()
        {
            ["AutoOptimizeMemory"] = true,
            ["LivingThemes"] = Environment.GetEnvironmentVariable("SMARTNOTES_PERF_LIVING") != "0",
            ["LivingThemesDefaultApplied"] = true,
        });
        app.WaitForNoteWindows(10, 20000);
        Thread.Sleep(8000); // startup work and the first memory trim settle

        Console.WriteLine();
        Console.WriteLine("Real app, 10 notes (one per theme):");
        Console.WriteLine($"  memory: working set {Refresh(app).WorkingSet64 / 1048576.0:0} MB, private {app.Process.PrivateMemorySize64 / 1048576.0:0} MB");
        Console.WriteLine($"  just started (last note focused): {Cpu(app, 10):0.00} % of one core");
        // Idle: the focus on the Classic note, which has no animation. (A window of this test program can't take the
        // foreground from the app - Windows' foreground lock - so "another program in front" can't be staged here.)
        try { Find(app.NoteWithTitle("Classic"), "TxtContent").SetFocus(); } catch { }
        Thread.Sleep(3000);
        Console.WriteLine($"  idle (Classic note focused):      {Cpu(app, 20):0.00} % of one core");

        foreach (var name in new[] { "EldenRing", "Astral", "Cyberpunk", "DeepSea" })
        {
            var w = app.NoteWithTitle(name);
            try { Find(w, "TxtContent").SetFocus(); } catch { }
            Thread.Sleep(1500);
            Console.WriteLine($"  {name + " note focused (living)",-34}{Cpu(app, 15):0.00} % of one core");
        }
        return 0;
    }

    private static Process Refresh(AppDriver app)
    {
        app.Process.Refresh();
        return app.Process;
    }

    /// <summary>The app's CPU use over a few seconds, in percent of one core.</summary>
    private static double Cpu(AppDriver app, int seconds)
    {
        var p = Refresh(app);
        var before = p.TotalProcessorTime;
        var sw = Stopwatch.StartNew();
        Thread.Sleep(seconds * 1000);
        p = Refresh(app);
        return (p.TotalProcessorTime - before).TotalMilliseconds / sw.Elapsed.TotalMilliseconds * 100;
    }
}
