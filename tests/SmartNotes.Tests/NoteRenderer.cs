using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using SmartNotes.UI.Skins;
using SmartNotes.UI.Windows;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace SmartNotes.Tests;

/// <summary>
/// Draws real note windows to PNG files, for checking a look without clicking through the app:
/// <c>dotnet run --project tests\SmartNotes.Tests -- --render &lt;folder&gt; [skins|features|menu]</c>.
/// Uses the unit-test data folder, never your notes. The windows are moved far off-screen while they are drawn.
/// </summary>
public static class NoteRenderer
{
    public static int Run(string outDir, string set)
    {
        Directory.CreateDirectory(outDir);
        TestData.ResetUnitDataDir();
        // pack://application:,,,/ must mean the app's resources (fonts, themes), not this test program's. WPF fixes it on
        // first use (to the entry assembly), so it is set through its field.
        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, typeof(SmartNotes.App).Assembly);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "UI/Icons/LucideIcons.xaml", "UI/Icons/SkinIcons.xaml", "UI/Themes/DarkTheme.xaml", "UI/Themes/ThemedControls.xaml" })
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + dict) });
        }
        LocalizationService.Instance.SetLanguage(Environment.GetEnvironmentVariable("SMARTNOTES_RENDER_LANG") ?? "en");

        var storage = new NoteStorageService();
        var settings = new SettingsService();
        settings.Settings.EnableSpellCheck = false; // no red squiggles in pictures

        if (set == "settings") return RenderSettingsPreviews(outDir, storage, settings);
        if (set == "settingspages") return RenderSettingsPages(outDir, storage, settings);

        var shots = set switch
        {
            "features" => FeatureShots(),
            "menu" => MenuShots(),
            "living" => LivingShots(settings),
            "creatures" => DeepSeaArt.CreatureKeys.Select((key, i) =>
            {
                var note = Sample(NoteSkin.DeepSeaKey, new[] { "Cyan", "Violet", "Amber" }[i], key, "A short note\nwith two lines");
                note.SetEmblem(NoteSkin.DeepSeaKey, key);
                note.Height = 340;
                return ("creature-" + key, note, (Func<StickyNoteWindow, BitmapSource?>?)null);
            }).ToList(),
            _ => SkinShots(),
        };

        var tiles = new List<(string Label, BitmapSource Image)>();
        foreach (var (name, note, prepare) in shots)
        {
            storage.AddNote(note);
            var win = new StickyNoteWindow(note, storage, settings, _ => { }, _ => { }) { ShowActivated = false };
            win.Show();
            win.MoveToScreenPosition(-30000, -30000);
            Pump(400);
            BitmapSource? image;
            try
            {
                image = prepare?.Invoke(win) ?? Render((FrameworkElement)win.Content);
            }
            finally
            {
                win.Close();
            }
            if (image == null) continue;
            Save(image, Path.Combine(outDir, name + ".png"));
            tiles.Add((name, image));
        }
        Save(Sheet(tiles), Path.Combine(outDir, "sheet-" + set + ".png"));
        Console.WriteLine($"Rendered {tiles.Count} picture(s) to {outDir}");
        return 0;
    }

    /// <summary>The Settings > Note Appearance live preview, once per skin.</summary>
    private static int RenderSettingsPreviews(string outDir, NoteStorageService storage, SettingsService settings)
    {
        var tiles = new List<(string Label, BitmapSource Image)>();
        var dialog = new SettingsDialog(settings, storage, () => { }) { ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual };
        dialog.Show();
        dialog.PanelGeneral.Visibility = Visibility.Collapsed; // the Note Appearance page, where the preview is
        dialog.PanelAppearance.Visibility = Visibility.Visible;
        Pump(400);
        foreach (var skin in NoteSkin.All)
        {
            dialog.CmbDefaultSkin.SelectedValue = skin.Key;
            Pump(300);
            var image = Render(dialog.PreviewNoteHost);
            Save(image, Path.Combine(outDir, "settings-" + skin.Key + ".png"));
            tiles.Add(("settings-" + skin.Key, image));
        }
        dialog.Close();
        Save(Sheet(tiles), Path.Combine(outDir, "sheet-settings.png"));
        Console.WriteLine($"Rendered {tiles.Count} picture(s) to {outDir}");
        return 0;
    }

    /// <summary>The whole Settings window on the Desktop &amp; System and Note Appearance pages.</summary>
    private static int RenderSettingsPages(string outDir, NoteStorageService storage, SettingsService settings)
    {
        settings.Settings.SyncFolder = @"C:\Users\you\OneDrive";
        settings.Settings.LivingThemes = true;
        settings.Settings.DefaultSkinKey = NoteSkin.InkWashiKey;
        settings.Settings.DefaultColorKey = "Rose";
        var tiles = new List<(string Label, BitmapSource Image)>();
        var dialog = new SettingsDialog(settings, storage, () => { }) { ShowActivated = false, Left = -30000, Top = -30000, WindowStartupLocation = WindowStartupLocation.Manual };
        dialog.Show();
        Pump(400);
        foreach (var (name, page) in new[] { ("general", (FrameworkElement)dialog.PanelGeneral), ("appearance", dialog.PanelAppearance) })
        {
            dialog.PanelGeneral.Visibility = Visibility.Collapsed;
            dialog.PanelAppearance.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
            Pump(300);
            var image = Render((FrameworkElement)dialog.Content);
            Save(image, Path.Combine(outDir, "settings-page-" + name + ".png"));
            tiles.Add(("settings-page-" + name, image));
        }
        var preview = Render(dialog.PreviewNoteHost); // as it opens, nothing changed
        tiles.Add(("settings-preview-on-open (default: Ink & Washi, Rose)", preview));
        dialog.Close();
        Save(Sheet(tiles), Path.Combine(outDir, "sheet-settingspages.png"));
        Console.WriteLine($"Rendered {tiles.Count} picture(s) to {outDir}");
        return 0;
    }

    // ---------------------------------------------------------------- what to draw

    private static NoteItem Sample(string skin, string color, string title, string text = "", NoteViewMode mode = NoteViewMode.Text) => new()
    {
        Title = title,
        Content = text,
        SkinKey = skin,
        ColorKey = color,
        ViewMode = mode,
        Width = 340,
        Height = 300,
        X = 100,
        Y = 100,
        ModifiedAt = DateTime.Now.AddMinutes(-12),
    };

    private static List<TodoCheckItem> Tasks(params (string Text, bool Done)[] tasks) =>
        tasks.Select(t => new TodoCheckItem { Text = t.Text, IsDone = t.Done }).ToList();

    private static List<(string, NoteItem, Func<StickyNoteWindow, BitmapSource?>?)> SkinShots()
    {
        var list = new List<(string, NoteItem, Func<StickyNoteWindow, BitmapSource?>?)>();
        string[] colors = { "Amber", "Cyan", "Violet", "Emerald", "Rose", "Gold", "Mint", "Obsidian" };
        int i = 0;
        foreach (var skin in NoteSkin.All)
        {
            var note = Sample(skin.Key, colors[i++ % colors.Length], "Today's Priorities", mode: NoteViewMode.Checklist);
            note.Checklist = Tasks(("Reply to Sam", true), ("Book the dentist", false), ("Water the plants", false));
            list.Add(("skin-" + skin.Key, note, null));
        }
        return list;
    }

    private static List<(string, NoteItem, Func<StickyNoteWindow, BitmapSource?>?)> FeatureShots()
    {
        var reminder = Sample("Classic", "Amber", "Call the bank", "Ask about the transfer fee.\nAccount number is on the letter.");
        reminder.Reminder = new NoteReminder { At = DateTime.Now.AddMinutes(-1) };

        var daily = Sample("Classic", "Emerald", "Morning", mode: NoteViewMode.Checklist);
        daily.RepeatDaily = true;
        daily.Streak = 6;
        daily.DailyDay = NoteSchedule.DayOf(DateTime.Now, 4);
        daily.Checklist = Tasks(("Stretch", true), ("Water the plants", true), ("Vitamins", true));

        var linked = Sample("Cyberpunk", "Cyan", "Build keys", "F5  run\nF9  breakpoint\nF10 step over\nCtrl+K C  comment");
        linked.LinkedApp = "Code.exe";
        linked.LinkedAppName = "Visual Studio Code";
        linked.Reminder = new NoteReminder { At = DateTime.Today.AddDays(1).AddHours(9), Repeat = ReminderRepeat.Weekdays };

        var sketch = Sample("Classic", "Cyan", "Doodle", mode: NoteViewMode.Sketch);
        sketch.Sketch = SmartNotes.UI.Controls.SketchData.ToBase64(SketchTests.Doodle());
        var sketchInk = Sample("InkWashi", "Rose", "Garden plan", mode: NoteViewMode.Sketch);
        sketchInk.Sketch = sketch.Sketch;

        var paperReminder = Sample("Paper", "Rose", "Stand-up", "Notes for the team meeting");
        paperReminder.Reminder = new NoteReminder { At = DateTime.Now.AddMinutes(-1) };

        return new()
        {
            ("feature-reminder-ringing", reminder, w => { w.FireReminder(DateTime.Now); Pump(1200); return Render((FrameworkElement)w.Content); }),
            ("feature-reminder-paper", paperReminder, w => { w.FireReminder(DateTime.Now); Pump(1200); return Render((FrameworkElement)w.Content); }),
            ("feature-daily-streak", daily, null),
            ("feature-linked-and-badge", linked, null),
            ("feature-sketch", sketch, null),
            ("feature-sketch-inkwashi", sketchInk, null),
        };
    }

    private static List<(string, NoteItem, Func<StickyNoteWindow, BitmapSource?>?)> MenuShots()
    {
        var list = new List<(string, NoteItem, Func<StickyNoteWindow, BitmapSource?>?)>();
        foreach (var skin in new[] { "Terminal", "Blueprint", "InkWashi", "DeepSea" })
        {
            var note = Sample(skin, "Amber", "Menu", mode: NoteViewMode.Checklist);
            note.Checklist = Tasks(("one", false));
            list.Add(("menu-" + skin, note, OpenMoreMenu));
        }
        return list;
    }

    /// <summary>Living themes caught mid-animation (the hover / focus state is simulated).</summary>
    private static List<(string, NoteItem, Func<StickyNoteWindow, BitmapSource?>?)> LivingShots(SettingsService settings)
    {
        settings.Settings.LivingThemes = true;
        BitmapSource? After(StickyNoteWindow w, string method, int ms)
        {
            // animations only advance while WPF renders the window: put it on screen, all but invisible
            w.Opacity = 0.01;
            w.MoveToScreenPosition(40, 40);
            Pump(150);
            Call(w, "Window_MouseEnter_Living");
            if (method.Length > 0) Call(w, method);
            Pump(ms);
            return Render((FrameworkElement)w.Content);
        }
        var sealNote = Sample("Medieval", "Rose", "Last Will", "Sealed and locked.");
        sealNote.IsLocked = true;
        return new()
        {
            ("living-medieval-seal", sealNote, w => After(w, "", 900)),
            ("living-astral-star", Sample("Astral", "Violet", "Night watch", "Look up."), w => After(w, "LaunchShootingStar", 260)),
            ("living-elden-rune", Sample("EldenRing", "Gold", "Grace", "The rune breathes."), w => After(w, "", 2000)),
            ("living-cyberpunk-glitch", Sample("Cyberpunk", "Rose", "Saved!", "glitch on save"), w => After(w, "GlitchIfLiving", 50)),
        };
    }

    private static void Call(StickyNoteWindow w, string method) =>
        typeof(StickyNoteWindow).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w, null);

    /// <summary>Opens the note's ⋯ menu and draws it next to the note.</summary>
    private static BitmapSource? OpenMoreMenu(StickyNoteWindow win)
    {
        typeof(StickyNoteWindow).GetMethod("BtnMore_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(win, new object[] { win.BtnMore, new RoutedEventArgs() });
        Pump(500);
        var menu = PresentationSource.CurrentSources.OfType<PresentationSource>()
            .Select(s => s.RootVisual).OfType<FrameworkElement>()
            .Select(FindMenu).FirstOrDefault(m => m != null);
        if (menu == null) return null;
        var note = Render((FrameworkElement)win.Content);
        var menuImage = Render(menu);
        menu.IsOpen = false;
        return SideBySide(note, menuImage);
    }

    private static ContextMenu? FindMenu(DependencyObject root)
    {
        if (root is ContextMenu { IsOpen: true } cm) return cm;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindMenu(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        }
        return null;
    }

    // ---------------------------------------------------------------- drawing

    private static void Pump(int ms)
    {
        var until = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < until)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Thread.Sleep(15);
        }
    }

    private const double Scale = 2.0;

    private static BitmapSource Render(FrameworkElement element)
    {
        element.UpdateLayout();
        int w = (int)Math.Ceiling(element.ActualWidth * Scale), h = (int)Math.Ceiling(element.ActualHeight * Scale);
        var bmp = new RenderTargetBitmap(Math.Max(1, w), Math.Max(1, h), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        // through a VisualBrush: rendering the element itself would include its offset inside its parent
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static BitmapSource SideBySide(BitmapSource a, BitmapSource b)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(a, new Rect(0, 0, a.Width, a.Height));
            dc.DrawImage(b, new Rect(a.Width + 12, 0, b.Width, b.Height));
        }
        var bmp = new RenderTargetBitmap((int)((a.Width + b.Width + 12) * Scale), (int)(Math.Max(a.Height, b.Height) * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>All pictures on one dark sheet with their names, three per row.</summary>
    private static BitmapSource Sheet(List<(string Label, BitmapSource Image)> tiles)
    {
        const int perRow = 3;
        const double pad = 24, labelH = 26;
        double cellW = tiles.Count == 0 ? 100 : tiles.Max(t => t.Image.Width), cellH = tiles.Count == 0 ? 100 : tiles.Max(t => t.Image.Height);
        int rows = Math.Max(1, (tiles.Count + perRow - 1) / perRow);
        double width = pad + Math.Min(perRow, Math.Max(1, tiles.Count)) * (cellW + pad), height = pad + rows * (cellH + labelH + pad);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3C)), null, new Rect(0, 0, width, height));
            var typeface = new Typeface("Segoe UI");
            for (int i = 0; i < tiles.Count; i++)
            {
                double x = pad + i % perRow * (cellW + pad), y = pad + i / perRow * (cellH + labelH + pad);
                var label = new FormattedText(tiles[i].Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.Gainsboro, 1.0);
                dc.DrawText(label, new Point(x, y));
                dc.DrawImage(tiles[i].Image, new Rect(x, y + labelH, tiles[i].Image.Width, tiles[i].Image.Height));
            }
        }
        var bmp = new RenderTargetBitmap((int)(width * 1.5), (int)(height * 1.5), 96 * 1.5, 96 * 1.5, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
