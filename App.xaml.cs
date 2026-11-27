using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using SmartNotes.Core;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;
using SmartNotes.UI.Windows;
using Application = System.Windows.Application;

namespace SmartNotes;

public partial class App : Application
{
    private static readonly string AppMutexName = @"Local\SmartNotes_SingleInstance_Mutex_987654" + AppPaths.InstanceSuffix;
    private static readonly string AppEventName = @"Local\SmartNotes_SingleInstance_Event_987654" + AppPaths.InstanceSuffix;
    private static readonly string AppExitEventName = @"Local\SmartNotes_Exit_Event_987654" + AppPaths.InstanceSuffix;
    private EventWaitHandle? _exitEvent;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _instanceEvent;

    private SettingsService _settingsService = null!;
    private NoteStorageService _storageService = null!;
    private TrayManager _trayManager = null!;
    private ShowDesktopWatcher? _showDesktopWatcher;
    private ForegroundWatcher? _foregroundWatcher;
    /// <summary>Exe name of the last program other than SmartNotes that was in front (app-linked notes).</summary>
    private string? _foregroundExe;
    private GlobalHotKeyManager? _hotKeyManager;
    private Window? _dummyHwndHost;

    private readonly Dictionary<Guid, StickyNoteWindow> _activeNoteWindows = new();

    public static App? Instance => Current as App;

    public IReadOnlyCollection<StickyNoteWindow> ActiveNoteWindows => _activeNoteWindows.Values;

    public List<Win32Api.RECT> GetOtherNoteRects(Guid excludeNoteId)
    {
        return GetOtherNoteRects(new[] { excludeNoteId });
    }

    public List<Win32Api.RECT> GetOtherNoteRects(IEnumerable<Guid> excludeNoteIds)
    {
        var excludeSet = new HashSet<Guid>(excludeNoteIds);
        var result = new List<Win32Api.RECT>();
        foreach (var kvp in _activeNoteWindows)
        {
            if (excludeSet.Contains(kvp.Key)) continue;
            var win = kvp.Value;
            if (win.IsVisible)
            {
                result.Add(win.GetVisibleScreenRect()); // snapping lines up visible edges, not window edges
            }
        }
        return result;
    }

    private static void LogStartup(string msg) => AppLog.Write(msg);

    protected override void OnStartup(StartupEventArgs e)
    {
        LogStartup("OnStartup triggered.");

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            LogStartup($"[AppDomain UnhandledException] {ev.ExceptionObject}");
        };

        DispatcherUnhandledException += (s, ev) =>
        {
            LogStartup($"[DispatcherUnhandledException] {ev.Exception}\n{ev.Exception.StackTrace}");
            // Keep the tray app (and any unsaved note edits) alive instead of crashing on a stray UI exception
            ev.Handled = true;
        };

        // "SmartNotes.exe --exit": ask the running instance to quit normally (it saves every note first), then leave.
        // Used by "Launch SmartNotes.bat" so a rebuild can replace the files.
        if (e.Args.Any(a => string.Equals(a, "--exit", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var quit = EventWaitHandle.OpenExisting(AppExitEventName);
                quit.Set();
                LogStartup("--exit: asked the running instance to quit.");
            }
            catch (Exception ex)
            {
                LogStartup($"--exit: no running instance to signal ({ex.Message}).");
            }
            Shutdown();
            return;
        }

        bool ownsMutex = false;
        try
        {
            _instanceMutex = new Mutex(true, AppMutexName, out bool createdNew);
            ownsMutex = createdNew;
            LogStartup($"Mutex created, createdNew: {createdNew}");
            if (!ownsMutex)
            {
                try
                {
                    ownsMutex = _instanceMutex.WaitOne(0, false);
                    LogStartup($"Mutex WaitOne(0): {ownsMutex}");
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                    LogStartup("Mutex AbandonedMutexException in WaitOne");
                }
            }
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
            LogStartup("Mutex AbandonedMutexException on create");
        }
        catch (Exception ex)
        {
            ownsMutex = true;
            LogStartup($"Mutex exception: {ex.Message}");
        }

        if (!ownsMutex)
        {
            LogStartup("Not owning mutex, signaling existing instance and shutting down...");
            try
            {
                // This launch came from the user, so it may take the foreground; pass that right on to the
                // running instance so it can actually bring its notes forward.
                Win32Api.AllowSetForegroundWindow(Win32Api.ASFW_ANY);
                using var evt = EventWaitHandle.OpenExisting(AppEventName);
                evt.Set();
            }
            catch (Exception ex)
            {
                LogStartup($"Event signal exception: {ex.Message}");
            }

            Shutdown();
            return;
        }

        LogStartup("Mutex owned successfully. Initializing application services...");

        try
        {
            _instanceEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppEventName);
        }
        catch (Exception ex)
        {
            LogStartup($"EventWaitHandle create exception: {ex.Message}");
        }

        // Quit request from "SmartNotes.exe --exit": a normal shutdown, so OnExit flushes every note's pending save
        try
        {
            _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppExitEventName);
            var exitEvent = _exitEvent;
            new Thread(() =>
            {
                try
                {
                    if (exitEvent.WaitOne())
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            LogStartup("Quit requested by --exit.");
                            Shutdown();
                        });
                    }
                }
                catch { }
            })
            { IsBackground = true, Name = "SmartNotes_Exit_Listener" }.Start();
        }
        catch (Exception ex)
        {
            LogStartup($"Exit event create exception: {ex.Message}");
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            // First start of a new version (e.g. after an update): copy notes + settings aside before anything loads them
            DataBackupService.SnapshotIfNewVersion(UpdateService.CurrentVersion);
            _settingsService = new SettingsService();
            LogStartup("SettingsService loaded.");
            _storageService = new NoteStorageService();
            LogStartup($"NoteStorageService loaded. Active notes: {_storageService.Notes.Count}");

            LocalizationService.Instance.SetLanguage(_settingsService.Settings.AppLanguage);
            LogStartup("LocalizationService initialized.");
            InitDummyHwndHost();
            LogStartup("DummyHwndHost initialized.");
            InitTrayManager();
            LogStartup("TrayManager initialized.");
        }
        catch (Exception ex)
        {
            // Without settings, notes or a tray icon the user could not even quit the app. Say what happened and
            // exit, instead of leaving an invisible process behind that also blocks every later start (single instance).
            LogStartup($"[Error] Startup failed: {ex}");
            ShowStartupFailure(ex);
            Shutdown(1);
            return;
        }

        // Everything below is optional: one failing step must not take the others down with it
        RunStartupStep("Skin key migration", MigrateSkinKeys);
        RunStartupStep("MemoryOptimizer", () => MemoryOptimizer.Initialize(_settingsService.Settings.AutoOptimizeMemory));
        RunStartupStep("GlobalHotkeys", InitGlobalHotkeys);
        RunStartupStep("SingleInstanceListener", StartSingleInstanceListener);

        // Which program is in front: app-linked notes show only over theirs (before the notes are restored)
        RunStartupStep("Foreground tracking", StartForegroundTracking);

        // Restore active notes on the desktop at their exact saved coordinates
        RunStartupStep("Desktop notes restore", RestoreAllDesktopNotes);
        // A note saved on a screen that is gone, or in a gap between screens, would be out of reach
        RunStartupStep("Notes onto screens", BringNotesOntoScreens);

        // Reminders and "repeat daily" checklists (anything missed while the PC was off happens now)
        RunStartupStep("Note clock", StartNoteClock);

        // Sync folder (off unless the user chose one in Settings)
        RunStartupStep("Sync folder", StartSync);

        // Keep "Desktop Stuck" notes visible through Show Desktop / Win+D (GitHub issue #1)
        RunStartupStep("ShowDesktopWatcher", () => _showDesktopWatcher = new ShowDesktopWatcher(
            () => _activeNoteWindows.Values.ToList(), _foregroundWatcher ??= new ForegroundWatcher()));

        // Silent updates: download in the background, restart into the new version only while the
        // user is away (3 min without input) and no note has focus. No dialogs.
        RunStartupStep("Automatic updates", () => UpdateService.StartAutomaticUpdates(() =>
            Win32Api.GetIdleTime() >= TimeSpan.FromMinutes(3) &&
            !_activeNoteWindows.Values.Any(w => w.IsActive)));

        RunStartupStep("Language prompt", CheckAndPromptLanguageSelection);
    }

    private static void RunStartupStep(string name, Action step)
    {
        try
        {
            step();
            LogStartup($"{name}: ok.");
        }
        catch (Exception ex)
        {
            LogStartup($"[Error] {name} failed: {ex}");
        }
    }

    private static void ShowStartupFailure(Exception ex)
    {
        try
        {
            MessageBox.Show(LocalizationService.T("Startup_FailedMsg", ex.Message, AppLog.FilePath),
                LocalizationService.T("Startup_FailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // nothing more we can do; the log has the details
        }
    }

    /// <summary>
    /// v1: the parchment-scroll skin was renamed from "EldenRing" to "Medieval" when the game-style
    /// Elden Ring skin took over that key. Runs once.
    /// </summary>
    private void MigrateSkinKeys()
    {
        var s = _settingsService.Settings;
        if (s.SkinDataVersion >= 1) return;

        bool notesChanged = false;
        foreach (var note in _storageService.Notes.Concat(_storageService.GetDeletedNotes()))
        {
            if (note.SkinKey == "EldenRing")
            {
                note.SkinKey = NoteSkin.MedievalKey;
                notesChanged = true;
            }
        }
        if (s.DefaultSkinKey == "EldenRing") s.DefaultSkinKey = NoteSkin.MedievalKey;
        if (s.SkinTitleFonts.Remove("EldenRing", out var titleFont)) s.SkinTitleFonts[NoteSkin.MedievalKey] = titleFont;
        if (s.SkinTextFonts.Remove("EldenRing", out var textFont)) s.SkinTextFonts[NoteSkin.MedievalKey] = textFont;

        s.SkinDataVersion = 1;
        if (notesChanged) _storageService.Save();
        _settingsService.Save();
    }

    private void InitDummyHwndHost()
    {
        _dummyHwndHost = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Visibility = Visibility.Hidden
        };
        _dummyHwndHost.Show();
        _dummyHwndHost.Hide();
    }

    private void InitGlobalHotkeys()
    {
        if (_dummyHwndHost == null || !_settingsService.Settings.EnableGlobalHotkeys) return;

        try
        {
            IntPtr hwnd = new WindowInteropHelper(_dummyHwndHost).EnsureHandle();
            _hotKeyManager = new GlobalHotKeyManager(hwnd);
            var taken = new List<string>();

            void Register(Keys key, Action action)
            {
                if (_hotKeyManager.Register(Win32Api.MOD_WIN | Win32Api.MOD_ALT, key, () => Dispatcher.Invoke(action)) < 0)
                {
                    taken.Add($"Win+Alt+{key}");
                }
            }

            // Windows itself owns Win+Alt+D (date and time), B, G, K, M, R and T (HDR, Game Bar, microphone), so the
            // show/hide shortcut moved from D, which could never be registered, to S.
            Register(Keys.N, CreateNewStickyNote);   // new note
            Register(Keys.S, ToggleHideAllNotes);    // show / hide all notes
            Register(Keys.A, BringAllNotesToFront);  // all notes in front of the open windows (they sit behind them otherwise)

            ReportTakenHotkeys(taken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Registering global hotkeys", ex);
        }
    }

    /// <summary>
    /// A shortcut another program already uses can't be ours. Say so once in a tray message (it used to be only in
    /// the log, so the shortcut just seemed broken); the same taken keys are not reported again on every start.
    /// </summary>
    private void ReportTakenHotkeys(List<string> taken)
    {
        foreach (var key in taken) LogStartup($"[Warning] {key} is already taken by another program; that shortcut is not available.");

        string current = string.Join(", ", taken);
        var settings = _settingsService.Settings;
        if (current == settings.ReportedHotkeyConflicts) return;
        settings.ReportedHotkeyConflicts = current;
        _settingsService.Save();

        if (taken.Count > 0)
        {
            _trayManager?.ShowBalloon("SmartNotes", LocalizationService.T("Hotkeys_TakenBalloon", current), ToolTipIcon.Warning);
            LogStartup($"Told the user that {current} is taken.");
        }
    }

    private void InitTrayManager()
    {
        _trayManager = new TrayManager(
            _settingsService,
            onNewNote: () => Dispatcher.Invoke(CreateNewStickyNote),
            onBringAllToFront: () => Dispatcher.Invoke(BringAllNotesToFront),
            onToggleHideAll: () => Dispatcher.Invoke(ToggleHideAllNotes),
            onArrangeNotes: () => Dispatcher.Invoke(ArrangeNotesOnDesktop),
            onOpenSettings: () => Dispatcher.Invoke(OpenSettingsDialog),
            onExit: () => Dispatcher.Invoke(ExitApplication),
            getDeletedNotes: () => _storageService.GetDeletedNotes(),
            onRestoreNote: id => Dispatcher.Invoke(() => RestoreNoteFromTrash(id)),
            onRestoreAllNotes: () => Dispatcher.Invoke(RestoreAllNotesFromTrash),
            onEmptyTrash: () => Dispatcher.Invoke(EmptyTrash),
            onOpenTrashFolder: () => _storageService.OpenTrashFolderInExplorer(),
            onTrimMemory: () => Dispatcher.Invoke(() => MemoryOptimizer.TrimMemory())
        );

        UpdateTrayTooltip();
    }

    private void StartForegroundTracking()
    {
        IntPtr fg = Win32Api.GetForegroundWindow();
        Win32Api.GetWindowThreadProcessId(fg, out uint pid);
        if (pid != (uint)Environment.ProcessId) _foregroundExe = ProgramWindows.ExeNameOf(Win32Api.GetAncestor(fg, Win32Api.GA_ROOT));
        _foregroundWatcher = new ForegroundWatcher();
        _foregroundWatcher.Changed += OnForegroundChanged;
    }

    /// <summary>Another program came to the front: app-linked notes appear over their program and hide over any other.</summary>
    private void OnForegroundChanged(IntPtr window)
    {
        _foregroundExe = ProgramWindows.ExeNameOf(window);
        ApplyAppLinks();
    }

    // ---------------------------------------------------------------- sync folder

    private SyncService? _sync;

    /// <summary>The sync folder service (null if it could not start); Settings shows its status.</summary>
    public SyncService? Sync => _sync;

    private void StartSync()
    {
        _sync = new SyncService(_storageService, _settingsService, Dispatcher);
        _sync.PlanReady += ApplySyncPlan;
        _sync.Restart();
    }

    /// <summary>Changes made on another PC: new notes appear (without taking the focus), changed ones update, deleted ones go to the trash.</summary>
    private void ApplySyncPlan(SyncPlan plan)
    {
        foreach (var note in plan.Added)
        {
            if (_activeNoteWindows.ContainsKey(note.Id)) continue;
            _storageService.AddNote(note);
            SpawnStickyNoteWindow(note, bringToFront: false, quiet: true);
        }
        foreach (var note in plan.Updated)
        {
            if (_activeNoteWindows.TryGetValue(note.Id, out var win)) win.ApplyRemote(note);
            else _storageService.UpdateNote(note);
        }
        foreach (var id in plan.Deleted)
        {
            if (_activeNoteWindows.TryGetValue(id, out var win)) win.RemoveBySync();
            else _storageService.DeleteNote(id);
        }
        if (plan.Added.Count > 0) BringNotesOntoScreens(); // another PC's screens may be laid out differently
        UpdateTrayTooltip();
    }

    // ---------------------------------------------------------------- reminders and daily checklists

    private System.Windows.Threading.DispatcherTimer? _clockTimer;

    private void StartNoteClock()
    {
        _clockTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background);
        _clockTimer.Tick += (s, e) => CheckNoteClock();
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Microsoft.Win32.SystemEvents.TimeChanged += OnSystemTimeChanged;
        CheckNoteClock();
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(CheckNoteClock); // woke up: reminders due while asleep
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(CheckNoteClock);

    /// <summary>A reminder was set, changed or snoozed: check now and plan the next check from it.</summary>
    public void NotifyScheduleChanged() => CheckNoteClock();

    /// <summary>
    /// Fires due reminders and starts new days for "Repeat Daily" checklists, then sleeps until the next thing is due,
    /// at most a minute (a cheap tick that also catches sleep and clock changes the events missed).
    /// </summary>
    private void CheckNoteClock()
    {
        if (_clockTimer == null) return;
        _clockTimer.Stop();
        var now = DateTime.Now;
        int dayStart = _settingsService.Settings.DayStartHour;
        DateTime next = now.AddMinutes(1);
        foreach (var win in _activeNoteWindows.Values.ToList())
        {
            try
            {
                var note = win.Note;
                if (note.RepeatDaily)
                {
                    win.ApplyDailyReset(now);
                    var dayStarts = NoteSchedule.NextDayStart(now, dayStart);
                    if (dayStarts < next) next = dayStarts;
                }
                if (NoteSchedule.IsDue(note.Reminder, now)) FireReminder(win, now);
                if (NoteSchedule.NextDue(note.Reminder) is { } due && due < next) next = due;
                win.RefreshTimeBadges();
            }
            catch (Exception ex)
            {
                AppLog.Error($"Clock check for note {win.Note.Id}", ex);
            }
        }
        var wait = next - DateTime.Now;
        _clockTimer.Interval = wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait;
        _clockTimer.Start();
    }

    private void FireReminder(StickyNoteWindow win, DateTime now)
    {
        win.FireReminder(now); // shows the note even while all notes are hidden or its program is not in front
        string title = string.IsNullOrWhiteSpace(win.Note.Title) ? win.Note.SnippetPreview : win.Note.Title;
        title = string.Join(" ", title.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (title.Length > 40) title = title.Substring(0, 39) + "…";
        _trayManager?.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_Reminder", title), ToolTipIcon.Info);
        LogStartup($"Reminder for note {win.Note.Id}.");
    }

    private void ApplyAppLinks()
    {
        if (_settingsService.Settings.HideAllNotes) return;
        foreach (var win in _activeNoteWindows.Values.ToList())
        {
            if (!win.Note.IsAppLinked || win.IsReminding) continue; // a ringing reminder stays until Done or Snooze
            if (AppLink.Matches(win.Note.LinkedApp, _foregroundExe)) win.ShowWithoutFocus();
            else if (win.IsVisible) win.Hide();
        }
    }

    private void RestoreAllDesktopNotes()
    {
        var activeNotes = _storageService.Notes.Where(n => !n.IsDeleted).ToList();
        if (activeNotes.Count == 0)
        {
            CreateNewStickyNote();
            return;
        }

        foreach (var note in activeNotes)
        {
            try
            {
                SpawnStickyNoteWindow(note, bringToFront: true, startup: true);
            }
            catch (Exception ex)
            {
                // One note that cannot be shown must not keep the others off the desktop
                LogStartup($"[Error] Note {note.Id} could not be shown: {ex}");
            }
        }

        UpdateTrayTooltip();
    }

    /// <param name="startup">
    /// Restoring the notes at startup: an app-linked note then only appears if its program is in front, and without
    /// taking the focus. Otherwise (a new, duplicated or restored note) the user just asked for it, so it is shown.
    /// </param>
    /// <param name="quiet">A note that arrived through the sync folder: shown without taking the focus, and an
    /// app-linked one only over its program (like at startup).</param>
    private void SpawnStickyNoteWindow(NoteItem note, bool bringToFront = false, bool startup = false, bool quiet = false)
    {
        if (quiet)
        {
            startup = true;
            bringToFront = false;
        }
        if (startup && note.IsAppLinked)
        {
            bringToFront = false;
        }
        if (_activeNoteWindows.ContainsKey(note.Id))
        {
            var existingWin = _activeNoteWindows[note.Id];
            existingWin.Show();
            if (bringToFront)
            {
                existingWin.Activate();
                existingWin.DesktopWindowManager.BringToFront();
            }
            else if (note.PinMode == NotePinMode.DesktopStuck)
            {
                existingWin.DesktopWindowManager.SendToDesktopBottom();
            }
            return;
        }

        var win = new StickyNoteWindow(
            note,
            _storageService,
            _settingsService,
            onSpawnNewNote: n => Dispatcher.Invoke(() =>
            {
                SpawnStickyNoteWindow(n, bringToFront: true);
            }),
            onClosedCallback: w => Dispatcher.Invoke(() =>
            {
                _activeNoteWindows.Remove(w.Note.Id);
                UpdateTrayTooltip();
                MemoryOptimizer.TrimMemory();
            }),
            startInForeground: bringToFront
        );

        _activeNoteWindows[note.Id] = win;

        if (startup && note.IsAppLinked)
        {
            if (!_settingsService.Settings.HideAllNotes && AppLink.Matches(note.LinkedApp, _foregroundExe)) win.ShowWithoutFocus();
        }
        else if (quiet)
        {
            if (!_settingsService.Settings.HideAllNotes)
            {
                win.ShowWithoutFocus();
                if (note.PinMode == NotePinMode.DesktopStuck) win.DesktopWindowManager.SendToDesktopBottom();
            }
        }
        else if (!_settingsService.Settings.HideAllNotes)
        {
            win.Show();
            if (bringToFront)
            {
                win.Activate();
                win.DesktopWindowManager.BringToFront();
            }
            else if (note.PinMode == NotePinMode.DesktopStuck)
            {
                win.DesktopWindowManager.SendToDesktopBottom();
            }
        }

        UpdateTrayTooltip();
    }

    /// <summary>
    /// A new or restored note must be seen: while all notes are hidden (Win+Alt+S) it would otherwise be created
    /// invisible, and the hotkey or tray item looked like it did nothing.
    /// </summary>
    private void ShowNotesIfHidden()
    {
        if (_settingsService.Settings.HideAllNotes) ToggleHideAllNotes();
    }

    public void CreateNewStickyNote()
    {
        ShowNotesIfHidden();

        // Compute smart spawn coordinate on desktop
        double spawnX = 140;
        double spawnY = 140;

        if (_activeNoteWindows.Count > 0)
        {
            var last = _activeNoteWindows.Values.Last();
            spawnX = (last.Left + 40) % (SystemParameters.PrimaryScreenWidth - 360);
            spawnY = (last.Top + 40) % (SystemParameters.PrimaryScreenHeight - 380);
        }

        var newNote = new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "",
            Content = "",
            X = Math.Max(40, spawnX),
            Y = Math.Max(40, spawnY),
            ColorKey = _settingsService.Settings.DefaultColorKey,
            SkinKey = _settingsService.Settings.DefaultSkinKey,
            PinMode = _settingsService.Settings.DefaultPinMode,
            FontSize = _settingsService.Settings.DefaultFontSize,
            Opacity = _settingsService.Settings.DefaultOpacity,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };

        _storageService.AddNote(newNote);
        SpawnStickyNoteWindow(newNote, bringToFront: true);

        if (_activeNoteWindows.TryGetValue(newNote.Id, out var win))
        {
            win.DesktopWindowManager.BringToFront();
            win.TxtContent.Focus();
        }
    }

    public void BringAllNotesToFront()
    {
        ShowNotesIfHidden(); // the windows alone would appear while the "hidden" setting stayed on
        var activeNotes = _storageService.Notes.Where(n => !n.IsDeleted).ToList();
        if (activeNotes.Count == 0)
        {
            CreateNewStickyNote();
            return;
        }

        // Activating the notes one by one made each take focus from the previous one, which then dropped back to
        // the bottom (GitHub issue #3). Instead: one note takes the foreground (Windows ignores raising windows of
        // an app that is not in the foreground), then the others are raised without taking focus.
        var windows = new List<StickyNoteWindow>();
        foreach (var note in activeNotes)
        {
            if (!_activeNoteWindows.TryGetValue(note.Id, out var win))
            {
                SpawnStickyNoteWindow(note, bringToFront: false);
                if (!_activeNoteWindows.TryGetValue(note.Id, out win)) continue;
            }
            win.Show();
            windows.Add(win);
        }
        if (windows.Count == 0) return;

        var focused = windows[^1];
        focused.Activate();
        focused.DesktopWindowManager.BringToFront();
        foreach (var win in windows) win.DesktopWindowManager.RaiseWithoutActivating();
        focused.DesktopWindowManager.RaiseWithoutActivating();
    }

    public void ToggleHideAllNotes()
    {
        bool hide = !_settingsService.Settings.HideAllNotes;
        _settingsService.Settings.HideAllNotes = hide;
        _settingsService.Save();

        foreach (var win in _activeNoteWindows.Values)
        {
            if (hide)
            {
                win.Hide();
            }
            else if (win.Note.IsAppLinked)
            {
                // back only over its own program
                if (AppLink.Matches(win.Note.LinkedApp, _foregroundExe)) win.ShowWithoutFocus();
            }
            else
            {
                win.Show();
            }
        }

        if (!hide) BringNotesOntoScreens(); // the screens may have changed while they were hidden
    }

    /// <summary>
    /// Puts the notes in neat rows on the main screen. Every note keeps its size (it used to make them all 340x360),
    /// docked stacks stay together and move as one, and locked notes stay where they are (they can't be moved).
    /// </summary>
    public void ArrangeNotesOnDesktop()
    {
        ShowNotesIfHidden();
        var movable = _activeNoteWindows.Values.Where(w => w.IsVisible && !w.Note.IsLocked).ToList();

        // docked stacks, found the same way dragging finds them, in reading order
        var groups = new List<List<StickyNoteWindow>>();
        var grouped = new HashSet<StickyNoteWindow>();
        foreach (var win in movable.OrderBy(w => w.Top).ThenBy(w => w.Left))
        {
            if (grouped.Contains(win)) continue;
            var stack = NoteClusterEngine.GetConnectedCluster(win, movable);
            foreach (var member in stack) grouped.Add(member);
            groups.Add(stack);
        }

        var bounds = groups.Select(g =>
        {
            double left = g.Min(w => w.Left), top = g.Min(w => w.Top);
            return new Rect(left, top, g.Max(w => w.Left + w.Width) - left, g.Max(w => w.Top + w.Height) - top);
        }).ToList();
        var positions = NoteArranger.Layout(bounds.Select(b => b.Size).ToList(), SystemParameters.WorkArea);

        for (int i = 0; i < groups.Count; i++)
        {
            double dx = positions[i].X - bounds[i].Left, dy = positions[i].Y - bounds[i].Top;
            foreach (var win in groups[i])
            {
                win.Left += dx;
                win.Top += dy;
            }
        }

        _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_NotesArranged"), ToolTipIcon.Info);
    }

    /// <summary>Notes whose title strip is on no screen are moved onto the nearest one (see ScreenPlacement).</summary>
    public void BringNotesOntoScreens()
    {
        var areas = ScreenPlacement.WorkAreas();
        foreach (var win in _activeNoteWindows.Values.ToList())
        {
            if (!win.IsVisible) continue;
            if (ScreenPlacement.Rescue(win.GetWindowScreenRect(), areas) is { } target)
            {
                win.MoveToScreenPosition(target.left, target.top);
                LogStartup($"Moved note {win.Note.Id} back onto a screen.");
            }
        }
    }

    private System.Windows.Threading.DispatcherTimer? _screenCheckTimer;

    /// <summary>Screens were added, removed or rearranged: check the notes once Windows has settled.</summary>
    public void ScheduleNotesOntoScreens()
    {
        if (_screenCheckTimer == null)
        {
            _screenCheckTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _screenCheckTimer.Tick += (s, e) =>
            {
                _screenCheckTimer.Stop();
                BringNotesOntoScreens();
            };
        }
        _screenCheckTimer.Stop();
        _screenCheckTimer.Start();
    }

    public void NotifyTransparencySettingsChanged()
    {
        foreach (var win in _activeNoteWindows.Values)
        {
            win.ApplyFocusOpacity(win.IsActive);
        }
    }

    /// <summary>Re-applies skin, colour and fonts on every open note (e.g. after a font was changed in Settings).</summary>
    public void NotifyAppearanceSettingsChanged()
    {
        foreach (var win in _activeNoteWindows.Values)
        {
            win.ApplyTheme(win.Note.ColorKey);
        }
    }

    public void NotifyProofingSettingsChanged()
    {
        foreach (var win in _activeNoteWindows.Values)
        {
            win.ApplyProofingSettings();
        }
    }

    private string _syncFolderAtOpen = "";

    public void OpenSettingsDialog() => OpenSettingsDialog(null);

    public void OpenSettingsDialog(Window? owner)
    {
        var dlg = new SettingsDialog(_settingsService, _storageService, () =>
        {
            UpdateAppLanguage(_settingsService.Settings.AppLanguage);
            _hotKeyManager?.Dispose();
            InitGlobalHotkeys();
            NotifyTransparencySettingsChanged();
            NotifyProofingSettingsChanged();
            NotifyAppearanceSettingsChanged();
            if (_sync != null && _syncFolderAtOpen != _settingsService.Settings.SyncFolder) _sync.Restart(); // a new sync folder, or sync turned off
            MemoryOptimizer.TrimMemory();
        });
        _syncFolderAtOpen = _settingsService.Settings.SyncFolder;

        var dialogOwner = owner ?? _activeNoteWindows.Values.FirstOrDefault(w => w.IsVisible);
        if (dialogOwner != null)
        {
            dlg.Owner = dialogOwner;
        }

        dlg.ShowDialog();
    }

    private void CheckAndPromptLanguageSelection()
    {
        try
        {
            bool isUpdateOrFirstRun = string.IsNullOrEmpty(_settingsService.Settings.LastLanguagePromptVersion) ||
                (Version.TryParse(_settingsService.Settings.LastLanguagePromptVersion, out var lastVer) && lastVer < new Version(2, 1, 0));

            if (!isUpdateOrFirstRun) return;

            var sysCulture = System.Globalization.CultureInfo.InstalledUICulture ?? System.Globalization.CultureInfo.CurrentUICulture;
            string sysTwoLetter = sysCulture.TwoLetterISOLanguageName.ToLowerInvariant();

            if (sysTwoLetter != "en")
            {
                var promptDlg = new LanguagePromptDialog(sysCulture);
                promptDlg.ShowDialog();
                if (promptDlg.UserSwitchedLanguage)
                {
                    _settingsService.Settings.AppLanguage = promptDlg.SelectedLanguageCode;
                    _settingsService.Settings.ProofingLanguage = promptDlg.SelectedProofingTag;
                }
                else
                {
                    _settingsService.Settings.AppLanguage = "en";
                    _settingsService.Settings.ProofingLanguage = "en-US";
                }
            }

            _settingsService.Settings.LastLanguagePromptVersion = "2.1.0";
            _settingsService.Save();
            UpdateAppLanguage(_settingsService.Settings.AppLanguage);
            NotifyProofingSettingsChanged();
        }
        catch (Exception ex)
        {
            LogStartup($"Error during language prompt check: {ex.Message}");
        }
    }

    public void UpdateAppLanguage(string langCode)
    {
        LocalizationService.Instance.SetLanguage(langCode);
        UpdateTrayTooltip();
        foreach (var win in _activeNoteWindows.Values)
        {
            win.ApplyLocalizedStrings();
        }
    }

    public void RestoreNoteFromTrash(Guid id)
    {
        var restored = _storageService.RestoreNoteFromTrash(id);
        if (restored != null)
        {
            ShowNotesIfHidden();
            SpawnStickyNoteWindow(restored, bringToFront: true);
            string title = string.IsNullOrWhiteSpace(restored.Title) ? restored.SnippetPreview : restored.Title;
            if (title.Length > 25) title = title.Substring(0, 22) + "...";
            _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_NoteRestored", title), ToolTipIcon.Info);
        }
    }

    public void RestoreAllNotesFromTrash()
    {
        var restored = _storageService.RestoreAllTrash();
        if (restored.Count > 0) ShowNotesIfHidden();
        foreach (var note in restored)
        {
            SpawnStickyNoteWindow(note, bringToFront: true);
        }
        if (restored.Count > 0)
        {
            string suffix = LocalizationService.PluralSuffix(restored.Count);
            _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_NotesRestoredCount", restored.Count, suffix), ToolTipIcon.Info);
        }
    }

    public void EmptyTrash()
    {
        _storageService.ClearTrash();
        _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_TrashEmptied"), ToolTipIcon.Info);
    }

    private void UpdateTrayTooltip()
    {
        int count = _activeNoteWindows.Count;
        string suffix = LocalizationService.PluralSuffix(count);
        _trayManager.UpdateTooltip(LocalizationService.T("Tray_TooltipCount", count, suffix));
    }

    private void StartSingleInstanceListener()
    {
        if (_instanceEvent == null) return;

        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (_instanceEvent.WaitOne())
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (_settingsService.Settings.HideAllNotes)
                            {
                                ToggleHideAllNotes();
                            }
                            else
                            {
                                BringAllNotesToFront();
                            }
                            _trayManager?.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_BroughtToFront"), ToolTipIcon.Info);
                        });
                    }
                }
                catch
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "SmartNotes_SingleInstance_Listener"
        };
        thread.Start();
    }

    public void ExitApplication()
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogStartup($"OnExit called with ExitCode: {e.ApplicationExitCode}");
        // Edits made within the last 400 ms are still sitting in each note's save debounce
        foreach (var win in _activeNoteWindows.Values.ToList())
        {
            try { win.FlushPendingSave(); } catch { }
        }
        try { _sync?.Dispose(); } catch { } // writes this PC's sync file if the last edits are not in it yet
        try { _showDesktopWatcher?.Dispose(); } catch { }
        try { _foregroundWatcher?.Dispose(); } catch { }
        try
        {
            _clockTimer?.Stop();
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            Microsoft.Win32.SystemEvents.TimeChanged -= OnSystemTimeChanged;
        }
        catch { }
        try { _trayManager?.Dispose(); } catch { }
        try { _hotKeyManager?.Dispose(); } catch { }
        try { _instanceEvent?.Dispose(); } catch { }
        try { _exitEvent?.Dispose(); } catch { }
        _instanceEvent = null;
        try
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
        catch { }
        _instanceMutex = null;

        base.OnExit(e);
    }
}

