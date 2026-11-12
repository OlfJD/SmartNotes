using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfWindow = System.Windows.Window;

namespace SmartNotes.Core.Native;

/// <summary>
/// Tray icon. The icon itself is a WinForms NotifyIcon, but its menu is the same WPF menu component
/// (AppMenu + DarkTheme styles) used by the note "more" menu and the text right-click menu.
/// </summary>
public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SettingsService _settingsService;
    private readonly Action _onNewNote;
    private readonly Action _onBringAllToFront;
    private readonly Action _onToggleHideAll;
    private readonly Action _onArrangeNotes;
    private readonly Action _onOpenSettings;
    private readonly Action _onExit;

    private readonly Func<IReadOnlyList<NoteItem>> _getDeletedNotes;
    private readonly Action<Guid> _onRestoreNote;
    private readonly Action _onRestoreAllNotes;
    private readonly Action _onEmptyTrash;
    private readonly Action _onOpenTrashFolder;
    private readonly Action _onTrimMemory;

    // Invisible 1px window that owns the menu: a tray menu needs a foreground window of ours,
    // otherwise it would not close when the user clicks somewhere else.
    private WpfWindow? _menuHost;
    private WpfContextMenu? _openMenu;

    public TrayManager(
        SettingsService settingsService,
        Action onNewNote,
        Action onBringAllToFront,
        Action onToggleHideAll,
        Action onArrangeNotes,
        Action onOpenSettings,
        Action onExit,
        Func<IReadOnlyList<NoteItem>> getDeletedNotes,
        Action<Guid> onRestoreNote,
        Action onRestoreAllNotes,
        Action onEmptyTrash,
        Action onOpenTrashFolder,
        Action onTrimMemory)
    {
        _settingsService = settingsService;
        _onNewNote = onNewNote;
        _onBringAllToFront = onBringAllToFront;
        _onToggleHideAll = onToggleHideAll;
        _onArrangeNotes = onArrangeNotes;
        _onOpenSettings = onOpenSettings;
        _onExit = onExit;

        _getDeletedNotes = getDeletedNotes;
        _onRestoreNote = onRestoreNote;
        _onRestoreAllNotes = onRestoreAllNotes;
        _onEmptyTrash = onEmptyTrash;
        _onOpenTrashFolder = onOpenTrashFolder;
        _onTrimMemory = onTrimMemory;

        Icon? appIcon = null;
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                appIcon = new Icon(iconPath, SystemInformation.SmallIconSize);
            }
            else if (Environment.ProcessPath != null)
            {
                appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
        }
        catch
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    appIcon = new Icon(iconPath);
                }
            }
            catch { }
        }

        _notifyIcon = new NotifyIcon
        {
            Text = "SmartNotes - Desktop Sticky Notes",
            Visible = true,
            Icon = appIcon ?? SystemIcons.Application
        };

        _notifyIcon.DoubleClick += (s, e) => _onBringAllToFront();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _onBringAllToFront();
            }
        };
        _notifyIcon.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
            }
        };
    }

    /// <summary>
    /// Kept for callers that change state shown in the menu. The menu is built fresh every time it
    /// opens, so there is nothing to do here.
    /// </summary>
    public void RebuildContextMenu()
    {
    }

    private void ShowMenu()
    {
        try
        {
            if (_openMenu != null) _openMenu.IsOpen = false;

            if (_menuHost == null)
            {
                _menuHost = new WpfWindow
                {
                    Width = 1,
                    Height = 1,
                    Left = -10000,
                    Top = -10000,
                    WindowStyle = System.Windows.WindowStyle.None,
                    ResizeMode = System.Windows.ResizeMode.NoResize,
                    AllowsTransparency = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    ShowInTaskbar = false,
                    Topmost = true
                };
                _menuHost.Deactivated += (s, e) =>
                {
                    if (_openMenu != null) _openMenu.IsOpen = false;
                };
            }

            _menuHost.Show();
            Win32Api.SetForegroundWindow(new WindowInteropHelper(_menuHost).Handle);

            var menu = BuildMenu();
            menu.PlacementTarget = _menuHost;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.Closed += (s, e) =>
            {
                if (ReferenceEquals(_openMenu, menu)) _openMenu = null;
                _menuHost?.Hide();
            };
            _openMenu = menu;
            menu.IsOpen = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray menu error: {ex.Message}");
        }
    }

    /// <summary>Runs a menu action after the menu has closed (some actions open dialogs).</summary>
    private static Action Later(Action action) =>
        () => Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, action);

    /// <summary>Builds the tray menu from the shared menu component.</summary>
    public WpfContextMenu BuildMenu()
    {
        var menu = AppMenu.Create();
        var settings = _settingsService.Settings;

        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_NewNote"), "plus", Later(_onNewNote), MenuTone.Accent));
        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_BringAllToFront"), "layers", Later(_onBringAllToFront)));
        menu.Items.Add(AppMenu.Separator());

        menu.Items.Add(AppMenu.Item(
            settings.HideAllNotes ? LocalizationService.T("Tray_ShowAll") : LocalizationService.T("Tray_HideAll"),
            settings.HideAllNotes ? "eye" : "eye-off",
            Later(_onToggleHideAll)));
        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_Arrange"), "monitor", Later(_onArrangeNotes)));

        // 48-hour trash & note recovery
        var deleted = _getDeletedNotes();
        var trash = AppMenu.Sub($"{LocalizationService.T("Tray_Trash")} ({deleted.Count})", "trash-2");
        if (deleted.Count > 0)
        {
            foreach (var note in deleted)
            {
                TimeSpan remaining = NoteStorageService.TrashRetentionPeriod - (DateTime.Now - (note.DeletedAt ?? DateTime.Now));
                string timeLeft = remaining.TotalHours >= 1
                    ? LocalizationService.T("Tray_TimeLeftHours", (int)remaining.TotalHours)
                    : LocalizationService.T("Tray_TimeLeftMinutes", (int)Math.Max(1, remaining.TotalMinutes));
                string title = string.IsNullOrWhiteSpace(note.Title) ? note.SnippetPreview : note.Title;
                if (title.Length > 24) title = title.Substring(0, 22) + "...";

                Guid id = note.Id;
                trash.Items.Add(AppMenu.Item(LocalizationService.T("Tray_RestoreNote", title, timeLeft), "undo-2", Later(() => _onRestoreNote(id))));
            }

            trash.Items.Add(AppMenu.Separator());
            trash.Items.Add(AppMenu.Item(LocalizationService.T("Tray_RestoreAll"), "undo-2", Later(_onRestoreAllNotes), MenuTone.Accent));
            trash.Items.Add(AppMenu.Item(LocalizationService.T("Tray_EmptyTrash"), "trash", Later(_onEmptyTrash), MenuTone.Danger));
        }
        else
        {
            trash.Items.Add(AppMenu.Item(LocalizationService.T("Tray_NoTrashNotes"), null, null, MenuTone.Muted, isEnabled: false));
        }
        trash.Items.Add(AppMenu.Separator());
        trash.Items.Add(AppMenu.Item(LocalizationService.T("Tray_OpenTrashFolder"), "folder", Later(_onOpenTrashFolder)));
        menu.Items.Add(trash);

        menu.Items.Add(AppMenu.Separator());

        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_Startup"), "zap", () =>
        {
            settings.LaunchOnStartup = !settings.LaunchOnStartup;
            _settingsService.Save();
        }, isChecked: settings.LaunchOnStartup));
        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_Settings"), "settings", Later(_onOpenSettings)));
        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_OptimizeRam"), "cpu", Later(() =>
        {
            var (before, after, freed) = MemoryOptimizer.TrimMemory();
            ShowBalloon("SmartNotes", LocalizationService.T("Tray_RamOptimizedBalloon", after, freed), ToolTipIcon.Info);
        })));
        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_CheckUpdates", UpdateService.CurrentVersion), "refresh-cw", Later(() =>
        {
            _ = UpdateService.CheckForUpdatesAsync(isManualCheck: true);
        })));

        menu.Items.Add(AppMenu.Separator());
        menu.Items.Add(AppMenu.Item(LocalizationService.T("Tray_Exit"), "x", Later(_onExit), MenuTone.Danger));

        return menu;
    }

    public void UpdateTooltip(string text)
    {
        try
        {
            if (text.Length > 63) text = text.Substring(0, 60) + "...";
            _notifyIcon.Text = text;
        }
        catch { }
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(3000, title, text, icon);
        }
        catch { }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        try { _menuHost?.Close(); } catch { }
        GC.SuppressFinalize(this);
    }
}
