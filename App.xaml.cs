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
                result.Add(win.GetWindowScreenRect());
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

        // Restore active notes on the desktop at their exact saved coordinates
        RunStartupStep("Desktop notes restore", RestoreAllDesktopNotes);

        // Keep "Desktop Stuck" notes visible through Show Desktop / Win+D (GitHub issue #1)
        RunStartupStep("ShowDesktopWatcher", () => _showDesktopWatcher = new ShowDesktopWatcher(() => _activeNoteWindows.Values.ToList()));

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

            // Win + Alt + N = New Sticky Note
            if (_hotKeyManager.Register(
                Win32Api.MOD_WIN | Win32Api.MOD_ALT,
                Keys.N,
                () => Dispatcher.Invoke(CreateNewStickyNote)
            ) < 0)
            {
                LogStartup("[Warning] Win+Alt+N is already taken by another program; the New Note hotkey is not available.");
            }

            // Win + Alt + D = Toggle Show / Hide All Desktop Notes
            if (_hotKeyManager.Register(
                Win32Api.MOD_WIN | Win32Api.MOD_ALT,
                Keys.D,
                () => Dispatcher.Invoke(ToggleHideAllNotes)
            ) < 0)
            {
                LogStartup("[Warning] Win+Alt+D is already taken by another program; the Show/Hide hotkey is not available.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Registering global hotkeys", ex);
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
                SpawnStickyNoteWindow(note, bringToFront: true);
            }
            catch (Exception ex)
            {
                // One note that cannot be shown must not keep the others off the desktop
                LogStartup($"[Error] Note {note.Id} could not be shown: {ex}");
            }
        }

        UpdateTrayTooltip();
    }

    private void SpawnStickyNoteWindow(NoteItem note, bool bringToFront = false)
    {
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
                _trayManager?.RebuildContextMenu();
                MemoryOptimizer.TrimMemory();
            }),
            startInForeground: bringToFront
        );

        _activeNoteWindows[note.Id] = win;

        if (!_settingsService.Settings.HideAllNotes)
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
    /// A new or restored note must be seen: while all notes are hidden (Win+Alt+D) it would otherwise be created
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

    public void LocateNote(Guid id)
    {
        if (_activeNoteWindows.TryGetValue(id, out var win))
        {
            win.Show();
            win.Activate();
            win.DesktopWindowManager.BringToFront();
            win.TxtContent.Focus();
        }
        else
        {
            var note = _storageService.Notes.FirstOrDefault(n => n.Id == id && !n.IsDeleted);
            if (note != null)
            {
                SpawnStickyNoteWindow(note, bringToFront: true);
            }
        }
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
            else
            {
                win.Show();
            }
        }

        _trayManager.RebuildContextMenu();
    }

    public void ArrangeNotesOnDesktop()
    {
        double screenWidth = SystemParameters.WorkArea.Width;
        double screenHeight = SystemParameters.WorkArea.Height;

        double curX = 40;
        double curY = 40;
        double cardW = 340;
        double cardH = 360;
        double gap = 20;

        foreach (var win in _activeNoteWindows.Values)
        {
            win.Left = curX;
            win.Top = curY;
            win.Width = cardW;
            win.Height = cardH;

            curX += cardW + gap;
            if (curX + cardW > screenWidth - 40)
            {
                curX = 40;
                curY += cardH + gap;
                if (curY + cardH > screenHeight - 40)
                {
                    curY = 40;
                }
            }
        }

        _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_NotesArranged"), ToolTipIcon.Info);
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

    public void OpenSettingsDialog() => OpenSettingsDialog(null);

    public void OpenSettingsDialog(Window? owner)
    {
        var dlg = new SettingsDialog(_settingsService, _storageService, () =>
        {
            UpdateAppLanguage(_settingsService.Settings.AppLanguage);
            _trayManager.RebuildContextMenu();
            _hotKeyManager?.Dispose();
            InitGlobalHotkeys();
            NotifyTransparencySettingsChanged();
            NotifyProofingSettingsChanged();
            NotifyAppearanceSettingsChanged();
            MemoryOptimizer.TrimMemory();
        });

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
        _trayManager?.RebuildContextMenu();
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
            _trayManager.RebuildContextMenu();
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
            string suffix = restored.Count == 1 ? "" : (LocalizationService.Instance.CurrentLanguage == "de" ? "n" : "s");
            _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_NotesRestoredCount", restored.Count, suffix), ToolTipIcon.Info);
        }
        _trayManager.RebuildContextMenu();
    }

    public void EmptyTrash()
    {
        _storageService.ClearTrash();
        _trayManager.ShowBalloon("SmartNotes", LocalizationService.T("Balloon_TrashEmptied"), ToolTipIcon.Info);
        _trayManager.RebuildContextMenu();
    }

    private void UpdateTrayTooltip()
    {
        int count = _activeNoteWindows.Count;
        string suffix = count == 1 ? "" : (LocalizationService.Instance.CurrentLanguage == "de" ? "n" : "s");
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
        try { _showDesktopWatcher?.Dispose(); } catch { }
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

