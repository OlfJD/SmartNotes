using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;
using SmartNotes.UI.Windows;
using Application = System.Windows.Application;

namespace SmartNotes;

public partial class App : Application
{
    private const string AppMutexName = "SmartNotes_SingleInstance_Mutex_987654";
    private Mutex? _appMutex;

    private SettingsService _settingsService = null!;
    private NoteStorageService _storageService = null!;
    private TrayManager _trayManager = null!;
    private GlobalHotKeyManager? _hotKeyManager;
    private Window? _dummyHwndHost;

    private readonly Dictionary<Guid, StickyNoteWindow> _activeNoteWindows = new();
    private bool _ownsMutex = false;

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

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            try
            {
                string crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(crashPath, $"[AppDomain UnhandledException] {ev.ExceptionObject}");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, ev) =>
        {
            try
            {
                string crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(crashPath, $"[DispatcherUnhandledException] {ev.Exception}\n{ev.Exception.StackTrace}");
            }
            catch { }
        };

        try
        {
            _appMutex = new Mutex(true, AppMutexName, out bool isNewInstance);
            _ownsMutex = isNewInstance;
            if (!_ownsMutex)
            {
                MessageBox.Show("SmartNotes is already running in your system tray / desktop!", "SmartNotes", MessageBoxButton.OK, MessageBoxImage.Information);
                _appMutex.Dispose();
                _appMutex = null;
                Shutdown();
                return;
            }
        }
        catch
        {
            _ownsMutex = false;
        }

        base.OnStartup(e);

        _settingsService = new SettingsService();
        _storageService = new NoteStorageService();

        MemoryOptimizer.Initialize();
        InitDummyHwndHost();
        InitGlobalHotkeys();
        InitTrayManager();

        // Restore active notes on the desktop at their exact saved coordinates
        RestoreAllDesktopNotes();

        // Check for updates asynchronously on startup
        _ = Task.Run(async () =>
        {
            await Task.Delay(2500); // Allow desktop notes to render smoothly before checking
            await UpdateService.CheckForUpdatesAsync(isManualCheck: false, notifyCallback: (title, msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _trayManager?.ShowBalloon(title, msg, ToolTipIcon.Info);
                });
            });
        });
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
            _hotKeyManager.Register(
                Win32Api.MOD_WIN | Win32Api.MOD_ALT,
                Keys.N,
                () => Dispatcher.Invoke(CreateNewStickyNote)
            );

            // Win + Alt + D = Toggle Show / Hide All Desktop Notes
            _hotKeyManager.Register(
                Win32Api.MOD_WIN | Win32Api.MOD_ALT,
                Keys.D,
                () => Dispatcher.Invoke(ToggleHideAllNotes)
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkeys: {ex.Message}");
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
            bool shouldBringFront = (note.PinMode == NotePinMode.AlwaysOnTop);
            SpawnStickyNoteWindow(note, bringToFront: shouldBringFront);
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

    public void CreateNewStickyNote()
    {
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

        foreach (var note in activeNotes)
        {
            if (_activeNoteWindows.TryGetValue(note.Id, out var win))
            {
                win.Show();
                win.Activate();
                win.DesktopWindowManager.BringToFront();
            }
            else
            {
                SpawnStickyNoteWindow(note, bringToFront: true);
            }
        }
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

        _trayManager.ShowBalloon("SmartNotes", "Sticky notes arranged neatly on your desktop!", ToolTipIcon.Info);
    }

    public void NotifyTransparencySettingsChanged()
    {
        foreach (var win in _activeNoteWindows.Values)
        {
            win.ApplyFocusOpacity(win.IsActive);
        }
    }

    public void OpenSettingsDialog()
    {
        var dlg = new SettingsDialog(_settingsService, _storageService, () =>
        {
            _trayManager.RebuildContextMenu();
            _hotKeyManager?.Dispose();
            InitGlobalHotkeys();
            NotifyTransparencySettingsChanged();
            MemoryOptimizer.TrimMemory();
        });

        if (_activeNoteWindows.Count > 0)
        {
            dlg.Owner = _activeNoteWindows.Values.FirstOrDefault();
        }

        dlg.ShowDialog();
    }

    public void RestoreNoteFromTrash(Guid id)
    {
        var restored = _storageService.RestoreNoteFromTrash(id);
        if (restored != null)
        {
            SpawnStickyNoteWindow(restored, bringToFront: true);
            string title = string.IsNullOrWhiteSpace(restored.Title) ? restored.SnippetPreview : restored.Title;
            if (title.Length > 25) title = title.Substring(0, 22) + "...";
            _trayManager.ShowBalloon("SmartNotes", $"Restored note \"{title}\" to desktop!", ToolTipIcon.Info);
            _trayManager.RebuildContextMenu();
        }
    }

    public void RestoreAllNotesFromTrash()
    {
        var restored = _storageService.RestoreAllTrash();
        foreach (var note in restored)
        {
            SpawnStickyNoteWindow(note, bringToFront: true);
        }
        if (restored.Count > 0)
        {
            _trayManager.ShowBalloon("SmartNotes", $"Restored {restored.Count} note{(restored.Count == 1 ? "" : "s")} to desktop!", ToolTipIcon.Info);
        }
        _trayManager.RebuildContextMenu();
    }

    public void EmptyTrash()
    {
        _storageService.ClearTrash();
        _trayManager.ShowBalloon("SmartNotes", "Trash folder emptied.", ToolTipIcon.Info);
        _trayManager.RebuildContextMenu();
    }

    private void UpdateTrayTooltip()
    {
        int count = _activeNoteWindows.Count;
        _trayManager.UpdateTooltip($"SmartNotes - {count} sticky note{(count == 1 ? "" : "s")} on desktop");
    }

    public void ExitApplication()
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _trayManager?.Dispose(); } catch { }
        try { _hotKeyManager?.Dispose(); } catch { }
        try
        {
            if (_ownsMutex && _appMutex != null)
            {
                _appMutex.ReleaseMutex();
            }
        }
        catch { }
        try { _appMutex?.Dispose(); } catch { }
        _appMutex = null;

        base.OnExit(e);
    }
}

