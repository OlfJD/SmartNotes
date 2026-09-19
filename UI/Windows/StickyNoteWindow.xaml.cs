using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;

namespace SmartNotes.UI.Windows;

public partial class StickyNoteWindow : Window
{
    private readonly NoteStorageService _storageService;
    private readonly SettingsService _settingsService;
    private readonly Action<NoteItem> _onSpawnNewNote;
    private readonly Action<StickyNoteWindow> _onClosedCallback;
    private readonly DesktopWindowManager _desktopWindowManager;

    private NoteItem _note;
    private bool _isLoaded = false;
    private DispatcherTimer? _saveDebounceTimer;
    private ObservableCollection<TodoCheckItem> _checklistItems = new();
    private ObservableCollection<CopySnippetItem> _copyItems = new();

    public NoteItem Note => _note;

    public StickyNoteWindow(
        NoteItem note,
        NoteStorageService storageService,
        SettingsService settingsService,
        Action<NoteItem> onSpawnNewNote,
        Action<StickyNoteWindow> onClosedCallback)
    {
        _note = note;
        _storageService = storageService;
        _settingsService = settingsService;
        _onSpawnNewNote = onSpawnNewNote;
        _onClosedCallback = onClosedCallback;
        _desktopWindowManager = new DesktopWindowManager(this);

        InitializeComponent();

        // Position window according to saved note coordinates with virtual screen safety bounds
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        Left = Math.Clamp(_note.X, vLeft, Math.Max(vLeft, vLeft + vWidth - 120));
        Top = Math.Clamp(_note.Y, vTop, Math.Max(vTop, vTop + vHeight - 120));
        Width = Math.Max(220, _note.Width);
        Height = Math.Max(180, _note.Height);
        Opacity = Math.Clamp(_note.Opacity, 0.3, 1.0);

        InitDebounceTimer();
        ApplyTheme(_note.ColorKey);
        ApplyNoteData();

        Loaded += OnWindowLoaded;
        LocationChanged += OnWindowPositionChanged;
        SizeChanged += OnWindowSizeChanged;
        Activated += (s, e) => _desktopWindowManager.SetInteracting(true);
        Deactivated += (s, e) => _desktopWindowManager.SetInteracting(false);
        PreviewMouseDown += (s, e) => _desktopWindowManager.SetInteracting(true);
    }

    private void InitDebounceTimer()
    {
        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _saveDebounceTimer.Tick += (s, e) =>
        {
            _saveDebounceTimer.Stop();
            SaveNoteState();
        };
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        WindowBlurHelper.ApplyModernWindowStyles(this);
        _desktopWindowManager.ApplyPinMode(_note.PinMode);
        _isLoaded = true;
    }

    private void ApplyNoteData()
    {
        TxtTitle.Text = _note.Title;
        TxtContent.Text = _note.Content;
        TxtTitlePlaceholder.Visibility = string.IsNullOrEmpty(_note.Title) ? Visibility.Visible : Visibility.Collapsed;
        TxtContentPlaceholder.Visibility = string.IsNullOrEmpty(_note.Content) ? Visibility.Visible : Visibility.Collapsed;

        TxtContent.FontSize = Math.Max(11, _note.FontSize);
        TxtTitle.FontSize = Math.Max(12, _note.FontSize + 1);

        // Checklist setup
        _checklistItems = new ObservableCollection<TodoCheckItem>(_note.Checklist);
        ChecklistItemsControl.ItemsSource = _checklistItems;

        // Copy list setup
        _copyItems = new ObservableCollection<CopySnippetItem>(_note.CopyList);
        CopyItemsControl.ItemsSource = _copyItems;

        // Ensure backward compatibility
        if (_note.IsChecklistMode && _note.ViewMode == NoteViewMode.Text)
        {
            _note.ViewMode = NoteViewMode.Checklist;
        }

        UpdateViewMode(_note.ViewMode);
        UpdatePinModeUI(_note.PinMode);
        UpdateLockUI(_note.IsLocked);
        UpdateModifiedTime();
    }

    public void ApplyTheme(string colorKey)
    {
        _note.ColorKey = colorKey;
        var theme = NoteColorTheme.Get(colorKey);

        try
        {
            var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BgHex));
            var headerBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.HeaderBgHex));
            var borderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BorderHex));
            var primaryBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.PrimaryHex));
            var glowBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.GlowHex));
            var textPrimaryBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TextPrimaryHex));
            var textMutedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TextMutedHex));

            NoteCardBorder.Background = bgBrush;
            NoteCardBorder.BorderBrush = borderBrush;
            HeaderBorder.Background = headerBgBrush;
            HeaderBorder.BorderBrush = borderBrush;
            LeftCompartmentBorder.BorderBrush = borderBrush;
            ModeCompartmentBorder.BorderBrush = borderBrush;
            ToolsCompartmentBorder.BorderBrush = borderBrush;

            TxtTitle.Foreground = textPrimaryBrush;
            TxtContent.Foreground = textPrimaryBrush;
            TxtTitle.CaretBrush = glowBrush;
            TxtContent.CaretBrush = glowBrush;
            IconPlus.Foreground = glowBrush;
        }
        catch { }
    }

    public void UpdateViewMode(NoteViewMode mode)
    {
        _note.ViewMode = mode;
        _note.IsChecklistMode = (mode == NoteViewMode.Checklist);

        TextModeContainer.Visibility = (mode == NoteViewMode.Text) ? Visibility.Visible : Visibility.Collapsed;
        ChecklistModeContainer.Visibility = (mode == NoteViewMode.Checklist) ? Visibility.Visible : Visibility.Collapsed;
        CopyModeContainer.Visibility = (mode == NoteViewMode.CopyCompartments) ? Visibility.Visible : Visibility.Collapsed;

        var amberBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
        var emeraldBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
        var cyanBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06B6D4"));
        var mutedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A8B5CD"));

        IconTextMode.Foreground = (mode == NoteViewMode.Text) ? amberBrush : mutedBrush;
        IconChecklistMode.Foreground = (mode == NoteViewMode.Checklist) ? emeraldBrush : mutedBrush;
        IconCopyMode.Foreground = (mode == NoteViewMode.CopyCompartments) ? cyanBrush : mutedBrush;

        BtnTextMode.ToolTip = (mode == NoteViewMode.Text) ? "Active: Plain Text Mode" : "Switch to Plain Text Mode";
        BtnChecklistMode.ToolTip = (mode == NoteViewMode.Checklist) ? "Active: Checklist Mode" : "Switch to Checklist Mode";
        BtnCopyMode.ToolTip = (mode == NoteViewMode.CopyCompartments) ? "Active: Copy Compartments Mode" : "Switch to Copy Compartments Mode";
    }

    private void UpdatePinModeUI(NotePinMode mode)
    {
        _note.PinMode = mode;
        _desktopWindowManager.ApplyPinMode(mode);

        switch (mode)
        {
            case NotePinMode.DesktopStuck:
                TxtPinIndicator.Text = "📌 Stuck to Desktop";
                IconPin.IconKey = "pin";
                IconPin.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                BtnPinMode.ToolTip = "Mode: Stuck to Desktop (Behind all apps). Click to Float Always on Top";
                break;

            case NotePinMode.AlwaysOnTop:
                TxtPinIndicator.Text = "📌 Always on Top";
                IconPin.IconKey = "pin";
                IconPin.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                BtnPinMode.ToolTip = "Mode: Always on Top (Floating). Click to Stick to Desktop";
                break;

            case NotePinMode.Normal:
                TxtPinIndicator.Text = "📌 Normal Window";
                IconPin.IconKey = "pin-off";
                IconPin.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A8B5CD"));
                BtnPinMode.ToolTip = "Mode: Normal Window. Click to Stick to Desktop";
                break;
        }
    }

    private void UpdateLockUI(bool isLocked)
    {
        _note.IsLocked = isLocked;
        TxtTitle.IsReadOnly = isLocked;
        TxtContent.IsReadOnly = isLocked;
        TxtNewTask.IsEnabled = !isLocked;
        TxtNewCopyItem.IsEnabled = !isLocked;

        if (isLocked)
        {
            IconLock.IconKey = "lock";
            IconLock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            LockBadge.Visibility = Visibility.Visible;
            BtnLock.ToolTip = "Note is Locked (Click to Unlock)";
        }
        else
        {
            IconLock.IconKey = "unlock";
            IconLock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A8B5CD"));
            LockBadge.Visibility = Visibility.Collapsed;
            BtnLock.ToolTip = "Lock note position & text";
        }
    }

    private void UpdateModifiedTime()
    {
        var elapsed = DateTime.Now - _note.ModifiedAt;
        if (elapsed.TotalMinutes < 1)
        {
            TxtModifiedTime.Text = "Just now";
        }
        else if (elapsed.TotalMinutes < 60)
        {
            TxtModifiedTime.Text = $"{(int)elapsed.TotalMinutes}m ago";
        }
        else if (elapsed.TotalHours < 24)
        {
            TxtModifiedTime.Text = $"{(int)elapsed.TotalHours}h ago";
        }
        else
        {
            TxtModifiedTime.Text = _note.ModifiedAt.ToString("MMM d");
        }
    }

    private void RequestSave()
    {
        if (!_isLoaded) return;
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer?.Start();
    }

    private void SaveNoteState()
    {
        _note.Title = TxtTitle.Text;
        _note.Content = TxtContent.Text;
        _note.Checklist = new List<TodoCheckItem>(_checklistItems);
        _note.CopyList = new List<CopySnippetItem>(_copyItems);
        _note.IsChecklistMode = (_note.ViewMode == NoteViewMode.Checklist);
        _note.X = Left;
        _note.Y = Top;
        _note.Width = Width;
        _note.Height = Height;
        _note.Opacity = Opacity;
        _note.FontSize = TxtContent.FontSize;
        _note.ModifiedAt = DateTime.Now;

        _storageService.UpdateNote(_note);
        UpdateModifiedTime();
    }

    // Window Dragging
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !_note.IsLocked)
        {
            _desktopWindowManager.SetInteracting(true);
            try
            {
                DragMove();
            }
            catch { }
            RequestSave();
        }
    }

    private void OnWindowPositionChanged(object? sender, EventArgs e)
    {
        RequestSave();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RequestSave();
    }

    // User interactions / Text Input
    private void TxtTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtTitlePlaceholder.Visibility = string.IsNullOrEmpty(TxtTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
        RequestSave();
    }

    private void TxtContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtContentPlaceholder.Visibility = string.IsNullOrEmpty(TxtContent.Text) ? Visibility.Visible : Visibility.Collapsed;
        RequestSave();
    }

    private void Input_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.SetInteracting(true);
    }

    private void Input_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveNoteState();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
        {
            CopyAllContent();
            e.Handled = true;
        }
    }

    // Header Mode Buttons
    private void BtnTextMode_Click(object sender, RoutedEventArgs e)
    {
        UpdateViewMode(NoteViewMode.Text);
        RequestSave();
    }

    private void BtnChecklistMode_Click(object sender, RoutedEventArgs e)
    {
        if (_checklistItems.Count == 0 && !string.IsNullOrWhiteSpace(TxtContent.Text))
        {
            var lines = TxtContent.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _checklistItems.Add(new TodoCheckItem { Text = line.Trim(), IsDone = false });
                }
            }
        }
        UpdateViewMode(NoteViewMode.Checklist);
        RequestSave();
    }

    private void BtnCopyMode_Click(object sender, RoutedEventArgs e)
    {
        if (_copyItems.Count == 0 && !string.IsNullOrWhiteSpace(TxtContent.Text))
        {
            var lines = TxtContent.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _copyItems.Add(new CopySnippetItem { Text = line.Trim() });
                }
            }
        }
        UpdateViewMode(NoteViewMode.CopyCompartments);
        RequestSave();
    }

    // Header Action Buttons
    private void BtnNewNote_Click(object sender, RoutedEventArgs e)
    {
        var newNote = new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "",
            Content = "",
            X = Left + 28,
            Y = Top + 28,
            Width = Width,
            Height = Height,
            ColorKey = _note.ColorKey,
            PinMode = _note.PinMode,
            ViewMode = _note.ViewMode,
            FontSize = _note.FontSize,
            Opacity = _note.Opacity,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
        _storageService.AddNote(newNote);
        _onSpawnNewNote(newNote);
    }

    private void BtnPinMode_Click(object sender, RoutedEventArgs e)
    {
        var nextMode = _note.PinMode == NotePinMode.DesktopStuck ? NotePinMode.AlwaysOnTop : NotePinMode.DesktopStuck;
        UpdatePinModeUI(nextMode);
        RequestSave();
    }

    private void BtnColorPicker_Click(object sender, RoutedEventArgs e)
    {
        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string colorKey)
        {
            ApplyTheme(colorKey);
            ColorPopup.IsOpen = false;
            RequestSave();
        }
    }

    private void BtnLock_Click(object sender, RoutedEventArgs e)
    {
        UpdateLockUI(!_note.IsLocked);
        RequestSave();
    }

    private void BtnMore_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        // Font Size Submenu
        var fontMenu = new MenuItem { Header = $"Font Size ({TxtContent.FontSize:F0}pt)" };
        fontMenu.Items.Add(CreateMenuItem("Increase Font Size (+)", () => AdjustFontSize(2)));
        fontMenu.Items.Add(CreateMenuItem("Decrease Font Size (-)", () => AdjustFontSize(-2)));
        fontMenu.Items.Add(CreateMenuItem("Reset Font Size (16pt)", () => SetFontSize(16)));
        menu.Items.Add(fontMenu);

        // Opacity Submenu
        var opacityMenu = new MenuItem { Header = $"Opacity ({(int)(Opacity * 100)}%)" };
        opacityMenu.Items.Add(CreateMenuItem("100% Solid", () => SetNoteOpacity(1.0)));
        opacityMenu.Items.Add(CreateMenuItem("90% Crisp", () => SetNoteOpacity(0.9)));
        opacityMenu.Items.Add(CreateMenuItem("80% Glass", () => SetNoteOpacity(0.8)));
        opacityMenu.Items.Add(CreateMenuItem("65% Translucent", () => SetNoteOpacity(0.65)));
        opacityMenu.Items.Add(CreateMenuItem("50% Stealth", () => SetNoteOpacity(0.5)));
        menu.Items.Add(opacityMenu);

        menu.Items.Add(new Separator());

        // Copy Note Content
        menu.Items.Add(CreateMenuItem("Copy All Content (Ctrl+Shift+C)", CopyAllContent));

        // Export Note as Markdown
        menu.Items.Add(CreateMenuItem("Export as Markdown (.md)", ExportNoteAsMarkdown));

        // Duplicate Note
        menu.Items.Add(CreateMenuItem("Duplicate Note", () =>
        {
            var clone = _note.Clone();
            _storageService.AddNote(clone);
            _onSpawnNewNote(clone);
        }));

        menu.Items.Add(new Separator());

        // Clear completed checklist items if in checklist mode
        if (_note.ViewMode == NoteViewMode.Checklist)
        {
            menu.Items.Add(CreateMenuItem("Clear Completed Tasks", ClearCompletedTasks));
            menu.Items.Add(new Separator());
        }

        // Delete Note
        menu.Items.Add(CreateMenuItem("Delete Note", DeleteNote));

        menu.PlacementTarget = BtnMore;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (s, e) => action();
        return item;
    }

    private void AdjustFontSize(double delta)
    {
        double newSize = Math.Clamp(TxtContent.FontSize + delta, 10, 32);
        SetFontSize(newSize);
    }

    private void SetFontSize(double size)
    {
        TxtContent.FontSize = size;
        TxtTitle.FontSize = size + 1;
        RequestSave();
    }

    private void SetNoteOpacity(double opacity)
    {
        Opacity = opacity;
        RequestSave();
    }

    public void CopyAllContent()
    {
        try
        {
            string bodyText;
            if (_note.ViewMode == NoteViewMode.Checklist)
            {
                bodyText = GetChecklistAsText();
            }
            else if (_note.ViewMode == NoteViewMode.CopyCompartments)
            {
                bodyText = GetCopyListAsText();
            }
            else
            {
                bodyText = TxtContent.Text ?? "";
            }

            string fullText = string.IsNullOrWhiteSpace(TxtTitle.Text) ? bodyText : $"# {TxtTitle.Text.Trim()}\n\n{bodyText}";
            Clipboard.SetText(string.IsNullOrEmpty(fullText) ? " " : fullText);
        }
        catch
        {
            try
            {
                Clipboard.SetDataObject(TxtContent.Text ?? "", true);
            }
            catch { }
        }
    }

    private string GetChecklistAsText()
    {
        var sb = new StringBuilder();
        foreach (var item in _checklistItems)
        {
            sb.AppendLine($"- [{(item.IsDone ? "x" : " ")}] {item.Text}");
        }
        return sb.ToString();
    }

    private string GetCopyListAsText()
    {
        var sb = new StringBuilder();
        foreach (var item in _copyItems)
        {
            sb.AppendLine(item.Text);
        }
        return sb.ToString();
    }

    private void ExportNoteAsMarkdown()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Markdown File (*.md)|*.md|Text File (*.txt)|*.txt",
                FileName = string.IsNullOrWhiteSpace(_note.Title) ? "SmartNote.md" : $"{_note.Title}.md"
            };
            if (sfd.ShowDialog() == true)
            {
                string text = _note.ViewMode switch
                {
                    NoteViewMode.Checklist => GetChecklistAsText(),
                    NoteViewMode.CopyCompartments => GetCopyListAsText(),
                    _ => _note.Content
                };
                string fullDoc = string.IsNullOrEmpty(_note.Title) ? text : $"# {_note.Title}\n\n{text}";
                File.WriteAllText(sfd.FileName, fullDoc);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export note: {ex.Message}", "SmartNotes Export", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearCompletedTasks()
    {
        for (int i = _checklistItems.Count - 1; i >= 0; i--)
        {
            if (_checklistItems[i].IsDone)
            {
                _checklistItems.RemoveAt(i);
            }
        }
        RequestSave();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DeleteNote();
    }

    private void DeleteNote()
    {
        SaveNoteState();
        _storageService.DeleteNote(_note.Id, permanent: false);
        _onClosedCallback(this);
        Close();
    }

    // Checklist Item Events
    private void TxtNewTask_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddNewTask();
            e.Handled = true;
        }
    }

    private void TxtNewTask_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtNewTaskPlaceholder.Visibility = string.IsNullOrEmpty(TxtNewTask.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnAddTask_Click(object sender, RoutedEventArgs e)
    {
        AddNewTask();
    }

    private void AddNewTask()
    {
        string text = TxtNewTask.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var item = new TodoCheckItem { Text = text, IsDone = false };
            _checklistItems.Add(item);
            TxtNewTask.Text = "";
            TxtNewTaskPlaceholder.Visibility = Visibility.Visible;
            TxtNewTask.Focus();
            RequestSave();
        }
    }

    private void CheckItem_Click(object sender, RoutedEventArgs e)
    {
        RequestSave();
    }

    private void TaskText_Changed(object sender, TextChangedEventArgs e)
    {
        RequestSave();
    }

    private void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TodoCheckItem item)
        {
            _checklistItems.Remove(item);
            RequestSave();
        }
    }

    // Copy Compartment Item Events
    private void BtnCopyRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CopySnippetItem item)
        {
            try
            {
                Clipboard.SetText(string.IsNullOrEmpty(item.Text) ? " " : item.Text);

                // Animate the button's LucideIcon to checkmark
                if (btn.Content is LucideIcon icon)
                {
                    string oldKey = icon.IconKey;
                    Brush oldBrush = icon.Foreground;
                    icon.IconKey = "check";
                    icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    btn.ToolTip = "Copied! ✓";

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        icon.IconKey = oldKey;
                        icon.Foreground = oldBrush;
                        btn.ToolTip = "Copy this line to clipboard";
                    };
                    timer.Start();
                }
            }
            catch
            {
                try
                {
                    Clipboard.SetDataObject(item.Text ?? "", true);
                }
                catch { }
            }
        }
    }

    private void CopyItemText_Changed(object sender, TextChangedEventArgs e)
    {
        RequestSave();
    }

    private void BtnDeleteCopyItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CopySnippetItem item)
        {
            _copyItems.Remove(item);
            RequestSave();
        }
    }

    private void TxtNewCopyItem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddNewCopyItem();
            e.Handled = true;
        }
    }

    private void TxtNewCopyItem_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtNewCopyItemPlaceholder.Visibility = string.IsNullOrEmpty(TxtNewCopyItem.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnAddCopyItem_Click(object sender, RoutedEventArgs e)
    {
        AddNewCopyItem();
    }

    private void AddNewCopyItem()
    {
        string text = TxtNewCopyItem.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var item = new CopySnippetItem { Text = text };
            _copyItems.Add(item);
            TxtNewCopyItem.Text = "";
            TxtNewCopyItemPlaceholder.Visibility = Visibility.Visible;
            TxtNewCopyItem.Focus();
            RequestSave();
        }
    }
}
