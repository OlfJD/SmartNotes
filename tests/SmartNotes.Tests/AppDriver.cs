using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using SmartNotes.Core.Models;

namespace SmartNotes.Tests;

/// <summary>
/// Starts the real SmartNotes.exe with its own throw-away data folder (SMARTNOTES_DATA_DIR: separate single-instance
/// mutex, never the user's notes or autostart entry) and drives it through UI Automation - buttons are invoked and text
/// is set through the controls' automation patterns, so the mouse is never moved.
/// </summary>
public sealed class AppDriver : IDisposable
{
    public string DataDir { get; }
    public Process Process { get; private set; }

    public static string ExePath
    {
        get
        {
            // another build to compare with (perf report)
            if (Environment.GetEnvironmentVariable("SMARTNOTES_TEST_EXE") is { Length: > 0 } other && File.Exists(other)) return other;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartNotes.csproj"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("SmartNotes.csproj not found above " + AppContext.BaseDirectory);
#if DEBUG
            const string config = "Debug";
#else
            const string config = "Release";
#endif
            string exe = Path.Combine(dir.FullName, "bin", config, "net10.0-windows", "win-x64", "SmartNotes.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("Build SmartNotes first", exe);
            return exe;
        }
    }

    public static Dictionary<string, object?> DefaultSettings() => new()
    {
        ["LaunchOnStartup"] = false,
        ["EnableGlobalHotkeys"] = false,
        ["LastLanguagePromptVersion"] = "99.0.0",
        ["AutoOptimizeMemory"] = false,
        ["AppLanguage"] = "en",
        ["ProofingLanguage"] = "en-US",
        ["SkinDataVersion"] = 1,
    };

    private AppDriver(string dataDir)
    {
        DataDir = dataDir;
        Process = Launch();
    }

    public static AppDriver Start(string name, IEnumerable<NoteItem>? notes = null,
        Dictionary<string, object?>? settings = null, string? rawNotesJson = null, string? rawSettingsJson = null,
        bool alongsideOthers = false)
    {
        if (!alongsideOthers) KillLeftovers();
        string dir = Path.Combine(TestData.Root, "e2e-" + name);
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        Directory.CreateDirectory(dir);

        var s = DefaultSettings();
        if (settings != null) foreach (var kv in settings) s[kv.Key] = kv.Value;
        File.WriteAllText(Path.Combine(dir, "settings.json"), rawSettingsJson ?? JsonSerializer.Serialize(s));
        if (rawNotesJson != null || notes != null)
            File.WriteAllText(Path.Combine(dir, "notes.json"), rawNotesJson ?? JsonSerializer.Serialize(notes!.ToList()));
        // Pretend this version already ran here, so no "before update" snapshot clutters the folder
        File.WriteAllText(Path.Combine(dir, "last_version.txt"), FileVersionInfo.GetVersionInfo(ExePath).ProductVersion?.Split('+')[0] ?? "");
        return new AppDriver(dir);
    }

    /// <summary>Only copies started from this test build: the user's own SmartNotes runs from elsewhere.</summary>
    private static void KillLeftovers()
    {
        foreach (var p in Process.GetProcessesByName("SmartNotes"))
        {
            try
            {
                if (string.Equals(p.MainModule?.FileName, ExePath, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(3000); }
            }
            catch { }
        }
    }

    public Process Launch(params string[] args)
    {
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(ExePath)! };
        psi.Environment["SMARTNOTES_DATA_DIR"] = DataDir;
        foreach (var a in args) psi.ArgumentList.Add(a);
        return Process.Start(psi)!;
    }

    public void Restart()
    {
        Exit();
        Process = Launch();
    }

    public void Exit()
    {
        if (Process.HasExited) return;
        using (var quit = Launch("--exit")) quit.WaitForExit(5000);
        if (!Process.WaitForExit(8000))
        {
            try { Process.Kill(); Process.WaitForExit(3000); } catch { }
        }
    }

    public void Dispose() => Exit();

    // ---------------------------------------------------------------- files

    public string PathIn(string name) => Path.Combine(DataDir, name);

    public List<NoteItem> ReadNotes() =>
        JsonSerializer.Deserialize<List<NoteItem>>(File.ReadAllText(PathIn("notes.json"))) ?? new();

    // ---------------------------------------------------------------- windows

    public List<AutomationElement> TopWindows()
    {
        if (Process.HasExited) return new();
        return AutomationElement.RootElement
            .FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, Process.Id))
            .Cast<AutomationElement>().ToList();
    }

    /// <summary>Visible note windows (each has a title box).</summary>
    public List<AutomationElement> NoteWindows() =>
        TopWindows().Where(w => TryFind(w, "TxtTitle") != null && !w.Current.IsOffscreen).ToList();

    public List<AutomationElement> WaitForNoteWindows(int count, int timeoutMs = 10000)
    {
        Assert.Eventually(() => NoteWindows().Count == count, $"expected {count} note window(s), saw {NoteWindows().Count}", timeoutMs);
        return NoteWindows();
    }

    public AutomationElement NoteWithTitle(string title)
    {
        AutomationElement? found = null;
        Assert.Eventually(() => (found = NoteWindows().FirstOrDefault(w => GetText(Find(w, "TxtTitle")) == title)) != null,
            $"note window titled '{title}'");
        return found!;
    }

    /// <summary>A top-level window of the app that contains an element with this automation id (dialogs).</summary>
    public AutomationElement? WindowContaining(string automationId) =>
        TopWindows().FirstOrDefault(w => TryFind(w, automationId) != null);

    /// <summary>A window of this class, top-level or owned by one of the app's windows (UIA lists owned dialogs under their owner).</summary>
    public AutomationElement? WindowOfClass(string className)
    {
        var byClass = new PropertyCondition(AutomationElement.ClassNameProperty, className);
        foreach (var top in TopWindows())
        {
            if (top.Current.ClassName == className) return top;
            if (top.FindFirst(TreeScope.Children, byClass) is { } owned) return owned;
        }
        return null;
    }

    /// <summary>For failure messages: the app's windows and the windows they own.</summary>
    public string DescribeWindows() => string.Join(" | ", TopWindows().Select(w =>
        $"{w.Current.ClassName} '{w.Current.Name}'" + string.Concat(w.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)).Cast<AutomationElement>()
            .Select(c => $" > {c.Current.ClassName} '{c.Current.Name}'"))));

    // ---------------------------------------------------------------- elements

    public static AutomationElement? TryFind(AutomationElement scope, string automationId) =>
        scope.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));

    public static AutomationElement Find(AutomationElement scope, string automationId) =>
        TryFind(scope, automationId) ?? throw new TestFailure($"element '{automationId}' not found");

    public static void Invoke(AutomationElement e) => ((InvokePattern)e.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    public static void SetText(AutomationElement e, string text) => ((ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern)).SetValue(text);

    public static string GetText(AutomationElement e) => ((ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;

    public static bool IsReadOnly(AutomationElement e) => ((ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern)).Current.IsReadOnly;

    /// <summary>A menu item of an open menu whose name contains <paramref name="part"/> (menus are separate popup windows).</summary>
    public AutomationElement FindMenuItem(string part)
    {
        AutomationElement? item = null;
        Assert.Eventually(() =>
        {
            item = TopWindows()
                .SelectMany(w => w.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)).Cast<AutomationElement>())
                .FirstOrDefault(e => e.Current.Name.Contains(part, StringComparison.Ordinal));
            return item != null;
        }, $"menu item containing '{part}'");
        return item!;
    }

    /// <summary>Opens a submenu of an open menu.</summary>
    public void ExpandMenuItem(string header) =>
        ((ExpandCollapsePattern)FindMenuItem(header).GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();

    /// <summary>The saved earlier versions of a note, newest first (the app's history file).</summary>
    public List<NoteVersion> ReadHistory(Guid noteId)
    {
        string file = PathIn($"history\\note_{noteId:N}.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<List<NoteVersion>>(File.ReadAllText(file)) ?? new() : new();
    }

    /// <summary>Clicks a menu item of an open context menu (menus are separate popup windows).</summary>
    public void InvokeMenuItem(string header)
    {
        AutomationElement? item = null;
        Assert.Eventually(() =>
        {
            item = TopWindows()
                .Select(w => w.FindFirst(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                    new PropertyCondition(AutomationElement.NameProperty, header))))
                .FirstOrDefault(e => e != null);
            return item != null;
        }, $"menu item '{header}'");
        Invoke(item!);
    }

    // ---------------------------------------------------------------- window order

    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

    public static IntPtr Handle(AutomationElement window) => new(window.Current.NativeWindowHandle);

    /// <summary>True when <paramref name="upper"/> is above <paramref name="lower"/> in the Windows z-order.</summary>
    public static bool IsAbove(IntPtr upper, IntPtr lower)
    {
        const uint GW_HWNDPREV = 3;
        for (IntPtr h = GetWindow(lower, GW_HWNDPREV); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDPREV))
        {
            if (h == upper) return true;
        }
        return false;
    }

    /// <summary>An ordinary window of another program (this test process), maximized over the notes.</summary>
    public static System.Windows.Window OpenCoverWindow()
    {
        var cover = new System.Windows.Window
        {
            Title = "SmartNotes test cover",
            WindowStyle = System.Windows.WindowStyle.None,
            WindowState = System.Windows.WindowState.Maximized,
            Background = System.Windows.Media.Brushes.DarkSlateGray,
            ShowInTaskbar = false
        };
        cover.Show();
        cover.Activate();
        PumpUi(300);
        return cover;
    }

    /// <summary>Lets this test process's own windows draw and react for a moment.</summary>
    public static void PumpUi(int ms)
    {
        var until = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < until)
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
            Thread.Sleep(20);
        }
    }

    // ---------------------------------------------------------------- window position, display changes, tray menu

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);
    private delegate bool EnumProc(IntPtr hWnd, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr l);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int max);

    /// <summary>Moves a window the way a vanished monitor leaves it (no activation, no z-order change).</summary>
    public static void MoveWindow(IntPtr hWnd, int x, int y) => SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);

    /// <summary>What Windows sends every top-level window when screens are added, removed or rearranged.</summary>
    public static void SendDisplayChange(IntPtr hWnd) => SendMessage(hWnd, 0x007E /* WM_DISPLAYCHANGE */, (IntPtr)32, IntPtr.Zero);

    /// <summary>True when the window's title strip (top 40 px) is on some monitor.</summary>
    public static bool TitleOnAScreen(IntPtr hWnd)
    {
        GetWindowRect(hWnd, out var r);
        var strip = new RECT { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Top + 40 };
        return MonitorFromRect(ref strip, 0 /* MONITOR_DEFAULTTONULL */) != IntPtr.Zero;
    }

    /// <summary>
    /// Right-clicks the app's tray icon without the mouse: sends the icon's own callback message (WinForms NotifyIcon,
    /// WM_USER + 1024 with WM_RBUTTONUP) to the app's hidden WinForms windows. The menu then opens as usual.
    /// </summary>
    public void OpenTrayMenu()
    {
        var candidates = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint pid);
            var cls = new System.Text.StringBuilder(256);
            GetClassName(h, cls, cls.Capacity);
            if (pid == Process.Id && cls.ToString().StartsWith("WindowsForms10.Window", StringComparison.Ordinal)) candidates.Add(h);
            return true;
        }, IntPtr.Zero);
        foreach (var h in candidates) PostMessage(h, 0x0400 + 1024, IntPtr.Zero, (IntPtr)0x0205);
    }

    // ---------------------------------------------------------------- keyboard (global hotkeys only)

    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    /// <summary>Presses Win+Alt+&lt;key&gt;: a global hotkey only the test copy of SmartNotes listens to.</summary>
    public static void PressWinAlt(char key)
    {
        const byte LWin = 0x5B, Alt = 0x12;
        const uint KeyUp = 2;
        byte vk = (byte)char.ToUpperInvariant(key);
        keybd_event(LWin, 0, 0, IntPtr.Zero);
        keybd_event(Alt, 0, 0, IntPtr.Zero);
        keybd_event(vk, 0, 0, IntPtr.Zero);
        keybd_event(vk, 0, KeyUp, IntPtr.Zero);
        keybd_event(Alt, 0, KeyUp, IntPtr.Zero);
        keybd_event(LWin, 0, KeyUp, IntPtr.Zero);
    }
}
