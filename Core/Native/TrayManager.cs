using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using DrawColor = System.Drawing.Color;
using DrawRectangle = System.Drawing.Rectangle;
using DrawPointF = System.Drawing.PointF;
using DrawGraphicsPath = System.Drawing.Drawing2D.GraphicsPath;

namespace SmartNotes.Core.Native;

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

        RebuildContextMenu();

        _notifyIcon.DoubleClick += (s, e) => _onBringAllToFront();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _onBringAllToFront();
            }
            else if (e.Button == MouseButtons.Right)
            {
                RebuildContextMenu();
            }
        };
    }

    public void RebuildContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = true,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = DrawColor.FromArgb(241, 245, 249),
            BackColor = DrawColor.FromArgb(13, 16, 23)
        };
        menu.Renderer = new ModernMenuRenderer();

        var newNoteItem = new ToolStripMenuItem("New Sticky Note", null, (s, e) => _onNewNote())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = DrawColor.FromArgb(255, 255, 255)
        };
        menu.Items.Add(newNoteItem);

        menu.Items.Add(new ToolStripSeparator());

        string hideText = _settingsService.Settings.HideAllNotes ? "Show All Notes" : "Hide All Notes";
        var toggleHideItem = new ToolStripMenuItem(hideText, null, (s, e) => _onToggleHideAll())
        {
            ForeColor = DrawColor.FromArgb(226, 232, 240)
        };
        menu.Items.Add(toggleHideItem);

        var arrangeItem = new ToolStripMenuItem("Arrange Notes on Desktop", null, (s, e) => _onArrangeNotes())
        {
            ForeColor = DrawColor.FromArgb(226, 232, 240)
        };
        menu.Items.Add(arrangeItem);

        // 48-Hour Temporary Trash & Note Recovery Submenu
        var deletedList = _getDeletedNotes();
        var trashMenu = new ToolStripMenuItem($"Recently Deleted ({deletedList.Count})")
        {
            ForeColor = deletedList.Count > 0 ? DrawColor.FromArgb(245, 158, 11) : DrawColor.FromArgb(168, 181, 205)
        };
        trashMenu.DropDown.Renderer = new ModernMenuRenderer();
        trashMenu.DropDown.BackColor = DrawColor.FromArgb(13, 16, 23);

        if (deletedList.Count > 0)
        {
            foreach (var note in deletedList)
            {
                TimeSpan remaining = NoteStorageService.TrashRetentionPeriod - (DateTime.Now - (note.DeletedAt ?? DateTime.Now));
                string timeLeft = remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours}h left" : $"{(int)Math.Max(1, remaining.TotalMinutes)}m left";
                string title = string.IsNullOrWhiteSpace(note.Title) ? note.SnippetPreview : note.Title;
                if (title.Length > 24) title = title.Substring(0, 22) + "...";

                var noteItem = new ToolStripMenuItem($"Restore: \"{title}\" ({timeLeft})", null, (s, e) =>
                {
                    _onRestoreNote(note.Id);
                    RebuildContextMenu();
                })
                {
                    ForeColor = DrawColor.FromArgb(241, 245, 249)
                };
                trashMenu.DropDownItems.Add(noteItem);
            }

            trashMenu.DropDownItems.Add(new ToolStripSeparator());

            var restoreAllItem = new ToolStripMenuItem("Restore All Notes", null, (s, e) =>
            {
                _onRestoreAllNotes();
                RebuildContextMenu();
            })
            {
                ForeColor = DrawColor.FromArgb(52, 211, 153)
            };
            trashMenu.DropDownItems.Add(restoreAllItem);

            var emptyTrashItem = new ToolStripMenuItem("Empty Trash Now", null, (s, e) =>
            {
                _onEmptyTrash();
                RebuildContextMenu();
            })
            {
                ForeColor = DrawColor.FromArgb(248, 113, 113)
            };
            trashMenu.DropDownItems.Add(emptyTrashItem);
        }
        else
        {
            var emptyItem = new ToolStripMenuItem("(No recently deleted notes)")
            {
                Enabled = false,
                ForeColor = DrawColor.FromArgb(120, 136, 164)
            };
            trashMenu.DropDownItems.Add(emptyItem);
        }

        trashMenu.DropDownItems.Add(new ToolStripSeparator());
        var openFolderItem = new ToolStripMenuItem("Open Trash Folder in Explorer...", null, (s, e) => _onOpenTrashFolder())
        {
            ForeColor = DrawColor.FromArgb(147, 197, 253)
        };
        trashMenu.DropDownItems.Add(openFolderItem);

        menu.Items.Add(trashMenu);
        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            bool newVal = !_settingsService.Settings.LaunchOnStartup;
            _settingsService.Settings.LaunchOnStartup = newVal;
            _settingsService.Save();
            RebuildContextMenu();
        })
        {
            Checked = _settingsService.Settings.LaunchOnStartup,
            ForeColor = DrawColor.FromArgb(226, 232, 240)
        };
        menu.Items.Add(startupItem);

        var settingsItem = new ToolStripMenuItem("Settings & Preferences...", null, (s, e) => _onOpenSettings())
        {
            ForeColor = DrawColor.FromArgb(226, 232, 240)
        };
        menu.Items.Add(settingsItem);

        var optimizeItem = new ToolStripMenuItem("Optimize Memory (RAM)", null, (s, e) =>
        {
            _onTrimMemory();
            double mb = MemoryOptimizer.GetCurrentMemoryUsageMb();
            ShowBalloon("SmartNotes", $"RAM footprint optimized! Current usage: {mb:F1} MB", ToolTipIcon.Info);
        })
        {
            ForeColor = DrawColor.FromArgb(245, 158, 11)
        };
        menu.Items.Add(optimizeItem);

        var updateItem = new ToolStripMenuItem("Check for Updates...", null, (s, e) =>
        {
            _ = UpdateService.CheckForUpdatesAsync(isManualCheck: true);
        })
        {
            ForeColor = DrawColor.FromArgb(226, 232, 240)
        };
        menu.Items.Add(updateItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit SmartNotes", null, (s, e) => _onExit())
        {
            ForeColor = DrawColor.FromArgb(248, 113, 113) // Alert Red
        };
        menu.Items.Add(exitItem);

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

internal class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected && e.Item.Enabled)
        {
            var rc = new DrawRectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using var b = new SolidBrush(DrawColor.FromArgb(30, 39, 60)); // Obsidian Dark hover
            using var borderPen = new Pen(DrawColor.FromArgb(160, 245, 158, 11), 1); // Subtle amber glow border
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectangle(rc, 4);
            e.Graphics.FillPath(b, path);
            e.Graphics.DrawPath(borderPen, path);
        }
        else
        {
            base.OnRenderMenuItemBackground(e);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
        {
            e.TextColor = DrawColor.FromArgb(100, 116, 139);
        }
        else if (e.Item.Selected)
        {
            e.TextColor = DrawColor.White;
        }
        else if (e.Item.ForeColor != DrawColor.Empty && e.Item.ForeColor != SystemColors.ControlText)
        {
            e.TextColor = e.Item.ForeColor;
        }
        else
        {
            e.TextColor = DrawColor.FromArgb(241, 245, 249);
        }

        e.TextFont = e.Item.Font;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rc = new DrawRectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2, e.ImageRectangle.Width - 4, e.ImageRectangle.Height - 4);

        // Modern circular checkmark matching SoundSwitcher / MacroMaster
        using var brush = new SolidBrush(DrawColor.FromArgb(16, 185, 129)); // Neon Emerald
        e.Graphics.FillEllipse(brush, rc);

        using var pen = new Pen(DrawColor.FromArgb(13, 16, 23), 2);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;

        float cx = rc.X + rc.Width / 2f;
        float cy = rc.Y + rc.Height / 2f;

        e.Graphics.DrawLines(pen, new DrawPointF[]
        {
            new DrawPointF(cx - 3.5f, cy),
            new DrawPointF(cx - 1f, cy + 2.5f),
            new DrawPointF(cx + 3.5f, cy - 2.5f)
        });
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(DrawColor.FromArgb(42, 55, 82), 1);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(DrawColor.FromArgb(13, 16, 23));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    private static DrawGraphicsPath CreateRoundedRectangle(DrawRectangle rect, int radius)
    {
        var path = new DrawGraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class ModernColorTable : ProfessionalColorTable
{
    public override DrawColor ToolStripDropDownBackground => DrawColor.FromArgb(13, 16, 23);
    public override DrawColor MenuBorder => DrawColor.FromArgb(42, 55, 82);
    public override DrawColor MenuItemBorder => DrawColor.Transparent;
    public override DrawColor MenuItemSelected => DrawColor.FromArgb(30, 39, 60);
    public override DrawColor MenuStripGradientBegin => DrawColor.FromArgb(13, 16, 23);
    public override DrawColor MenuStripGradientEnd => DrawColor.FromArgb(13, 16, 23);
    public override DrawColor MenuItemSelectedGradientBegin => DrawColor.FromArgb(30, 39, 60);
    public override DrawColor MenuItemSelectedGradientEnd => DrawColor.FromArgb(30, 39, 60);
    public override DrawColor MenuItemPressedGradientBegin => DrawColor.FromArgb(24, 30, 48);
    public override DrawColor MenuItemPressedGradientEnd => DrawColor.FromArgb(24, 30, 48);
    public override DrawColor ImageMarginGradientBegin => DrawColor.FromArgb(13, 16, 23);
    public override DrawColor ImageMarginGradientMiddle => DrawColor.FromArgb(13, 16, 23);
    public override DrawColor ImageMarginGradientEnd => DrawColor.FromArgb(13, 16, 23);
    public override DrawColor CheckBackground => DrawColor.FromArgb(22, 29, 46);
    public override DrawColor CheckSelectedBackground => DrawColor.FromArgb(30, 39, 60);
    public override DrawColor CheckPressedBackground => DrawColor.FromArgb(24, 30, 48);
    public override DrawColor SeparatorDark => DrawColor.FromArgb(42, 55, 82);
    public override DrawColor SeparatorLight => DrawColor.Transparent;
}
