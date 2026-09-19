using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SmartNotes.Core.Services;

namespace SmartNotes.Core.Native;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SettingsService _settingsService;
    private readonly Action _onNewNote;
    private readonly Action _onOpenHub;
    private readonly Action _onToggleHideAll;
    private readonly Action _onArrangeNotes;
    private readonly Action _onOpenSettings;
    private readonly Action _onExit;

    public TrayManager(
        SettingsService settingsService,
        Action onNewNote,
        Action onOpenHub,
        Action onToggleHideAll,
        Action onArrangeNotes,
        Action onOpenSettings,
        Action onExit)
    {
        _settingsService = settingsService;
        _onNewNote = onNewNote;
        _onOpenHub = onOpenHub;
        _onToggleHideAll = onToggleHideAll;
        _onArrangeNotes = onArrangeNotes;
        _onOpenSettings = onOpenSettings;
        _onExit = onExit;

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

        RebuildContextMenu();

        _notifyIcon.DoubleClick += (s, e) => _onOpenHub();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _onOpenHub();
            }
        };
    }

    public void RebuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var newNoteItem = new ToolStripMenuItem("New Sticky Note", null, (s, e) => _onNewNote());
        newNoteItem.Font = new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(newNoteItem);

        menu.Items.Add("Notes Hub (Manager)", null, (s, e) => _onOpenHub());
        menu.Items.Add(new ToolStripSeparator());

        string hideText = _settingsService.Settings.HideAllNotes ? "Show All Notes" : "Hide All Notes";
        menu.Items.Add(hideText, null, (s, e) => _onToggleHideAll());
        menu.Items.Add("Arrange Notes on Desktop", null, (s, e) => _onArrangeNotes());
        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            bool newVal = !_settingsService.Settings.LaunchOnStartup;
            _settingsService.Settings.LaunchOnStartup = newVal;
            _settingsService.Save();
            RebuildContextMenu();
        });
        startupItem.Checked = _settingsService.Settings.LaunchOnStartup;
        menu.Items.Add(startupItem);

        menu.Items.Add("Settings...", null, (s, e) => _onOpenSettings());
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Exit SmartNotes", null, (s, e) => _onExit());

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void UpdateTooltip(string text)
    {
        if (text.Length > 63) text = text.Substring(0, 60) + "...";
        _notifyIcon.Text = text;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
