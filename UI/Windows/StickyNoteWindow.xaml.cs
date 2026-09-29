using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;

namespace SmartNotes.UI.Windows;

public enum ResizeDirection
{
    Left = 1,
    Right = 2,
    Top = 3,
    TopLeft = 4,
    TopRight = 5,
    Bottom = 6,
    BottomLeft = 7,
    BottomRight = 8
}

public partial class StickyNoteWindow : Window
{
    private readonly NoteStorageService _storageService;
    private readonly SettingsService _settingsService;
    private readonly Action<NoteItem> _onSpawnNewNote;
    private readonly Action<StickyNoteWindow> _onClosedCallback;
    private readonly DesktopWindowManager _desktopWindowManager;

    private static readonly SolidColorBrush AmberBrush = NoteColorTheme.CreateFrozenBrush("#F59E0B");
    private static readonly SolidColorBrush EmeraldBrush = NoteColorTheme.CreateFrozenBrush("#10B981");
    private static readonly SolidColorBrush CyanBrush = NoteColorTheme.CreateFrozenBrush("#06B6D4");
    private static readonly SolidColorBrush MutedBrush = NoteColorTheme.CreateFrozenBrush("#A8B5CD");

    private NoteItem _note;
    private bool _isLoaded = false;
    private double _userConfiguredOpacity = 1.0;
    private DispatcherTimer? _saveDebounceTimer;
    private DispatcherTimer? _savedStatusResetTimer;
    private SolidColorBrush _currentGlowBrush = AmberBrush;
    private ObservableCollection<TodoCheckItem> _checklistItems = new();
    private ObservableCollection<CopySnippetItem> _copyItems = new();

    private double _currentHue = 25.0; // 0 to 360
    private double _currentSat = 0.90; // 0 to 1
    private double _currentVal = 0.95; // 0 to 1
    private bool _isDraggingSatVal = false;
    private bool _isDraggingHue = false;
    private bool _suppressHexChanged = false;
    private bool _suppressTransparencyEvents = false;

    private readonly bool _startInForeground;

    public NoteItem Note => _note;
    public DesktopWindowManager DesktopWindowManager => _desktopWindowManager;

    public StickyNoteWindow(
        NoteItem note,
        NoteStorageService storageService,
        SettingsService settingsService,
        Action<NoteItem> onSpawnNewNote,
        Action<StickyNoteWindow> onClosedCallback,
        bool startInForeground = false)
    {
        _note = note;
        _storageService = storageService;
        _settingsService = settingsService;
        _onSpawnNewNote = onSpawnNewNote;
        _onClosedCallback = onClosedCallback;
        _startInForeground = startInForeground;
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
        _userConfiguredOpacity = Math.Clamp(_note.Opacity, 0.3, 1.0);
        Opacity = _userConfiguredOpacity;

        InitDebounceTimer();
        ApplyTheme(_note.ColorKey);
        ApplyNoteData();

        Loaded += OnWindowLoaded;
        LocationChanged += OnWindowPositionChanged;
        SizeChanged += OnWindowSizeChanged;
        Activated += (s, e) => ApplyFocusOpacity(true);
        Deactivated += (s, e) =>
        {
            _desktopWindowManager.SetInteracting(false);
            ApplyFocusOpacity(false);
        };
        PreviewMouseDown += (s, e) =>
        {
            _desktopWindowManager.BringToFront();
            ApplyFocusOpacity(true);
        };
        PreviewKeyDown += (s, e) =>
        {
            // Suppress Alt+Space Windows system menu
            if ((e.Key == Key.System && e.SystemKey == Key.Space) ||
                ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && (e.Key == Key.Space || e.SystemKey == Key.Space)))
            {
                e.Handled = true;
            }
        };
        Closed += (s, e) =>
        {
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        };
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
        ApplyProofingSettings();
        ApplyLocalizedStrings();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        if (_startInForeground || _note.PinMode == NotePinMode.AlwaysOnTop)
        {
            _desktopWindowManager.BringToFront();
            TxtContent.Focus();
        }
        _isLoaded = true;
        ApplyFocusOpacity(IsActive || _startInForeground);
    }

    public void ApplyLocalizedStrings()
    {
        if (BtnNewNote != null) BtnNewNote.ToolTip = LocalizationService.T("Tooltip_NewNote");
        if (BtnColorPicker != null) BtnColorPicker.ToolTip = LocalizationService.T("Tooltip_ColorPalette");
        if (BtnClose != null) BtnClose.ToolTip = LocalizationService.T("Tooltip_DeleteNote");
        if (BtnMore != null) BtnMore.ToolTip = LocalizationService.T("Tooltip_MoreOptions");
        if (BtnAddTask != null) BtnAddTask.ToolTip = LocalizationService.T("Tooltip_AddTask");
        if (BtnAddCopyItem != null) BtnAddCopyItem.ToolTip = LocalizationService.T("Tooltip_AddSnippet");

        if (TxtQuickNeonThemes != null) TxtQuickNeonThemes.Text = LocalizationService.T("ColorPopup_QuickThemes");
        if (TxtCustomPaletteCanvas != null) TxtCustomPaletteCanvas.Text = LocalizationService.T("ColorPopup_CustomCanvas");
        if (BtnApplyCustomColor != null) BtnApplyCustomColor.Content = LocalizationService.T("ColorPopup_Apply");
        if (TxtTransparencyDimming != null) TxtTransparencyDimming.Text = LocalizationService.T("ColorPopup_TransparencyDimming");
        if (TxtNoteOpacityHeader != null) TxtNoteOpacityHeader.Text = LocalizationService.T("ColorPopup_NoteOpacity");
        if (TxtDimWhenUnfocusedHeader != null) TxtDimWhenUnfocusedHeader.Text = LocalizationService.T("ColorPopup_DimUnfocused");
        if (TxtUnfocusedOpacityHeader != null) TxtUnfocusedOpacityHeader.Text = LocalizationService.T("ColorPopup_UnfocusedOpacity");

        if (BtnSwatchAmber != null) BtnSwatchAmber.ToolTip = LocalizationService.T("Theme_Amber");
        if (BtnSwatchEmerald != null) BtnSwatchEmerald.ToolTip = LocalizationService.T("Theme_Emerald");
        if (BtnSwatchViolet != null) BtnSwatchViolet.ToolTip = LocalizationService.T("Theme_Violet");
        if (BtnSwatchCyan != null) BtnSwatchCyan.ToolTip = LocalizationService.T("Theme_Cyan");
        if (BtnSwatchRose != null) BtnSwatchRose.ToolTip = LocalizationService.T("Theme_Rose");
        if (BtnSwatchObsidian != null) BtnSwatchObsidian.ToolTip = LocalizationService.T("Theme_Obsidian");
        if (BtnSwatchGold != null) BtnSwatchGold.ToolTip = LocalizationService.T("Theme_Gold");
        if (BtnSwatchMint != null) BtnSwatchMint.ToolTip = LocalizationService.T("Theme_Mint");

        if (TxtLockedBadge != null) TxtLockedBadge.Text = LocalizationService.T("Status_Locked");

        UpdatePinModeUI(_note.PinMode);
        UpdateViewMode(_note.ViewMode);
        UpdateLockUI(_note.IsLocked);
        UpdateModifiedTime();

        if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Text = LocalizationService.T("Placeholder_Title");
        if (TxtContentPlaceholder != null) TxtContentPlaceholder.Text = LocalizationService.T("Placeholder_Content");
        if (TxtNewTaskPlaceholder != null) TxtNewTaskPlaceholder.Text = LocalizationService.T("Placeholder_NewChecklist");
        if (TxtNewCopyItemPlaceholder != null) TxtNewCopyItemPlaceholder.Text = LocalizationService.T("Placeholder_NewSnippet");
    }

    private void OnLanguageChanged()
    {
        Dispatcher.Invoke(ApplyLocalizedStrings);
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
            _currentGlowBrush = theme.GlowBrush;

            NoteCardBorder.Background = theme.BgBrush;
            NoteCardBorder.BorderBrush = theme.BorderBrush;
            HeaderBorder.Background = theme.HeaderBgBrush;
            HeaderBorder.BorderBrush = theme.BorderBrush;
            LeftCompartmentBorder.BorderBrush = theme.BorderBrush;
            ModeCompartmentBorder.BorderBrush = theme.BorderBrush;
            ToolsCompartmentBorder.BorderBrush = theme.BorderBrush;

            TxtTitle.Foreground = theme.TextPrimaryBrush;
            TxtContent.Foreground = theme.TextPrimaryBrush;
            TxtTitle.CaretBrush = theme.GlowBrush;
            TxtContent.CaretBrush = theme.GlowBrush;
            TxtNewTask.CaretBrush = theme.GlowBrush;
            TxtNewCopyItem.CaretBrush = theme.GlowBrush;
            IconPlus.Foreground = theme.GlowBrush;
        }
        catch { }
    }

    public void UpdateViewMode(NoteViewMode mode)
    {
        _note.ViewMode = mode;
        _note.IsChecklistMode = (mode == NoteViewMode.Checklist);

        if (TextModeContainer != null) TextModeContainer.Visibility = (mode == NoteViewMode.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (ChecklistModeContainer != null) ChecklistModeContainer.Visibility = (mode == NoteViewMode.Checklist) ? Visibility.Visible : Visibility.Collapsed;
        if (CopyModeContainer != null) CopyModeContainer.Visibility = (mode == NoteViewMode.CopyCompartments) ? Visibility.Visible : Visibility.Collapsed;

        if (IconTextMode != null) IconTextMode.Foreground = (mode == NoteViewMode.Text) ? AmberBrush : MutedBrush;
        if (IconChecklistMode != null) IconChecklistMode.Foreground = (mode == NoteViewMode.Checklist) ? EmeraldBrush : MutedBrush;
        if (IconCopyMode != null) IconCopyMode.Foreground = (mode == NoteViewMode.CopyCompartments) ? CyanBrush : MutedBrush;

        if (BtnTextMode != null) BtnTextMode.ToolTip = (mode == NoteViewMode.Text) ? LocalizationService.T("Tooltip_ActiveTextMode") : LocalizationService.T("Tooltip_TextMode");
        if (BtnChecklistMode != null) BtnChecklistMode.ToolTip = (mode == NoteViewMode.Checklist) ? LocalizationService.T("Tooltip_ActiveChecklistMode") : LocalizationService.T("Tooltip_ChecklistMode");
        if (BtnCopyMode != null) BtnCopyMode.ToolTip = (mode == NoteViewMode.CopyCompartments) ? LocalizationService.T("Tooltip_ActiveCopyMode") : LocalizationService.T("Tooltip_CopyMode");
    }

    private void UpdatePinModeUI(NotePinMode mode)
    {
        _note.PinMode = mode;
        _desktopWindowManager.ApplyPinMode(mode);

        if (TxtPinIndicator == null || IconPin == null || BtnPinMode == null) return;

        switch (mode)
        {
            case NotePinMode.DesktopStuck:
                TxtPinIndicator.Text = LocalizationService.T("Pin_StuckToDesktop");
                IconPin.IconKey = "pin";
                IconPin.Foreground = EmeraldBrush;
                BtnPinMode.ToolTip = LocalizationService.T("Tooltip_Pin_StuckToDesktop");
                break;

            case NotePinMode.AlwaysOnTop:
                TxtPinIndicator.Text = LocalizationService.T("Pin_AlwaysOnTop");
                IconPin.IconKey = "pin";
                IconPin.Foreground = AmberBrush;
                BtnPinMode.ToolTip = LocalizationService.T("Tooltip_Pin_AlwaysOnTop");
                break;

            case NotePinMode.Normal:
                TxtPinIndicator.Text = LocalizationService.T("Pin_Normal");
                IconPin.IconKey = "pin-off";
                IconPin.Foreground = MutedBrush;
                BtnPinMode.ToolTip = LocalizationService.T("Tooltip_Pin_Normal");
                break;
        }
    }

    private void UpdateLockUI(bool isLocked)
    {
        _note.IsLocked = isLocked;
        if (TxtTitle != null) TxtTitle.IsReadOnly = isLocked;
        if (TxtContent != null) TxtContent.IsReadOnly = isLocked;
        if (TxtNewTask != null) TxtNewTask.IsEnabled = !isLocked;
        if (TxtNewCopyItem != null) TxtNewCopyItem.IsEnabled = !isLocked;
        if (ResizeOverlayGrid != null)
        {
            ResizeOverlayGrid.IsHitTestVisible = !isLocked;
        }

        if (IconLock != null)
        {
            IconLock.IconKey = isLocked ? "lock" : "unlock";
            IconLock.Foreground = isLocked ? AmberBrush : MutedBrush;
        }
        if (LockBadge != null)
        {
            LockBadge.Visibility = isLocked ? Visibility.Visible : Visibility.Collapsed;
        }
        if (BtnLock != null)
        {
            BtnLock.ToolTip = isLocked ? LocalizationService.T("Tooltip_UnlockNote") : LocalizationService.T("Tooltip_LockNote");
        }
    }

    private void UpdateModifiedTime()
    {
        var elapsed = DateTime.Now - _note.ModifiedAt;
        if (elapsed.TotalMinutes < 1)
        {
            TxtModifiedTime.Text = LocalizationService.T("Time_JustNow");
        }
        else if (elapsed.TotalMinutes < 60)
        {
            TxtModifiedTime.Text = LocalizationService.T("Time_MinutesAgo", (int)elapsed.TotalMinutes);
        }
        else if (elapsed.TotalHours < 24)
        {
            TxtModifiedTime.Text = LocalizationService.T("Time_HoursAgo", (int)elapsed.TotalHours);
        }
        else
        {
            TxtModifiedTime.Text = _note.ModifiedAt.ToString("MMM d");
        }
    }

    private void ShowTypingIndicator()
    {
        if (!_isLoaded) return;
        _savedStatusResetTimer?.Stop();

        TxtModifiedTime.Visibility = Visibility.Collapsed;
        TypingIndicatorContainer.Visibility = Visibility.Visible;
        IconTypingStatus.IconKey = "pen-line";
        IconTypingStatus.Foreground = _currentGlowBrush;
        TxtTypingStatus.Text = LocalizationService.T("Status_Typing");
        TxtTypingStatus.Foreground = _currentGlowBrush;
    }

    private void ShowSavedIndicator()
    {
        if (!_isLoaded) return;
        _savedStatusResetTimer?.Stop();

        TxtModifiedTime.Visibility = Visibility.Collapsed;
        TypingIndicatorContainer.Visibility = Visibility.Visible;
        IconTypingStatus.IconKey = "check";
        IconTypingStatus.Foreground = EmeraldBrush;
        TxtTypingStatus.Text = LocalizationService.T("Status_Saved");
        TxtTypingStatus.Foreground = EmeraldBrush;

        _savedStatusResetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _savedStatusResetTimer.Tick += (s, e) =>
        {
            _savedStatusResetTimer.Stop();
            TypingIndicatorContainer.Visibility = Visibility.Collapsed;
            TxtModifiedTime.Visibility = Visibility.Visible;
            UpdateModifiedTime();
        };
        _savedStatusResetTimer.Start();
    }

    private void RequestSave(bool isTyping = true)
    {
        if (!_isLoaded) return;
        if (isTyping)
        {
            ShowTypingIndicator();
        }
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer?.Start();
    }

    public Win32Api.RECT GetWindowScreenRect()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && Win32Api.GetWindowRect(hwnd, out var rect))
        {
            return rect;
        }

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var matrix = source.CompositionTarget.TransformToDevice;
            var topLeft = matrix.Transform(new Point(Left, Top));
            var bottomRight = matrix.Transform(new Point(Left + Width, Top + Height));
            return new Win32Api.RECT((int)topLeft.X, (int)topLeft.Y, (int)bottomRight.X, (int)bottomRight.Y);
        }

        return new Win32Api.RECT(
            (int)Left,
            (int)Top,
            (int)(Left + Width),
            (int)(Top + Height)
        );
    }

    public void SaveNoteState()
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
        _note.Opacity = _userConfiguredOpacity;
        _note.FontSize = TxtContent.FontSize;
        _note.ModifiedAt = DateTime.Now;

        _storageService.UpdateNote(_note);
        ShowSavedIndicator();
    }

    // Window Dragging
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !_note.IsLocked)
        {
            _desktopWindowManager.BringToFront();
            _desktopWindowManager.StartDrag();
            try
            {
                DragMove();
            }
            catch { }
            _desktopWindowManager.EndDrag();
            RequestSave(isTyping: false);
        }
    }

    private void OnWindowPositionChanged(object? sender, EventArgs e)
    {
        RequestSave(isTyping: false);
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RequestSave(isTyping: false);
    }

    // User interactions / Text Input
    private void TxtTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtTitlePlaceholder.Visibility = string.IsNullOrEmpty(TxtTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
        RequestSave(isTyping: true);
    }

    private void TxtContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtContentPlaceholder.Visibility = string.IsNullOrEmpty(TxtContent.Text) ? Visibility.Visible : Visibility.Collapsed;
        RequestSave(isTyping: true);
    }

    private void TxtTitle_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        TxtTitlePlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtTitle.Text))
        {
            TxtTitlePlaceholder.Visibility = Visibility.Visible;
        }
        SaveNoteState();
    }

    private void TxtContent_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        TxtContentPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtContent_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtContent.Text))
        {
            TxtContentPlaceholder.Visibility = Visibility.Visible;
        }
        SaveNoteState();
    }

    private void Input_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
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
        // Smart placement: spawn docked to the right of current note
        double screenRight = SystemParameters.WorkArea.Right;
        double screenBottom = SystemParameters.WorkArea.Bottom;

        double targetX = Left + Width + 14;
        double targetY = Top;

        // If it goes off-screen to the right, place to the left or cascade downward
        if (targetX + Width > screenRight)
        {
            if (Left - Width - 14 >= SystemParameters.WorkArea.Left)
            {
                targetX = Left - Width - 14;
            }
            else
            {
                targetX = Math.Max(SystemParameters.WorkArea.Left + 20, Left + 30);
                targetY = Top + 35;
                if (targetY + Height > screenBottom)
                {
                    targetY = Math.Max(SystemParameters.WorkArea.Top + 20, Top - 35);
                }
            }
        }

        var newNote = new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "",
            Content = "",
            X = targetX,
            Y = targetY,
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

    private void ColorPopup_Opened(object? sender, EventArgs e)
    {
        // Parse current theme into HSV
        var theme = NoteColorTheme.Get(_note.ColorKey);
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(theme.PrimaryHex);
            RgbToHsv(color, out _currentHue, out _currentSat, out _currentVal);
        }
        catch
        {
            _currentHue = 25.0;
            _currentSat = 0.90;
            _currentVal = 0.95;
        }

        _suppressTransparencyEvents = true;
        try
        {
            if (SliderActiveOpacity != null)
            {
                SliderActiveOpacity.Value = Math.Round(_userConfiguredOpacity * 100);
            }
            if (TxtActiveOpacityPercent != null)
            {
                TxtActiveOpacityPercent.Text = $"{(int)Math.Round(_userConfiguredOpacity * 100)}%";
            }
            if (ChkUnfocusedDim != null)
            {
                ChkUnfocusedDim.IsChecked = _settingsService.Settings.EnableUnfocusedTransparency;
            }
            if (SliderUnfocusedOpacity != null)
            {
                SliderUnfocusedOpacity.Value = Math.Round(_settingsService.Settings.UnfocusedOpacity * 100);
                SliderUnfocusedOpacity.IsEnabled = _settingsService.Settings.EnableUnfocusedTransparency;
            }
            if (TxtUnfocusedOpacityPercent != null)
            {
                TxtUnfocusedOpacityPercent.Text = $"{(int)Math.Round(_settingsService.Settings.UnfocusedOpacity * 100)}%";
            }
            if (UnfocusedOpacityControlsPanel != null)
            {
                UnfocusedOpacityControlsPanel.Opacity = _settingsService.Settings.EnableUnfocusedTransparency ? 1.0 : 0.4;
            }
        }
        finally
        {
            _suppressTransparencyEvents = false;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            UpdateSatValDisplay();
            UpdateHueDisplay();
            UpdateColorFromHsv();
        });
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string colorKey)
        {
            ApplyTheme(colorKey);
            ColorPopup.IsOpen = false;
            RequestSave(isTyping: false);
        }
    }

    // 2D Saturation / Value Picker Events
    private void SatValPicker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDraggingSatVal = true;
            SatValPickerGrid.CaptureMouse();
            UpdateSatValFromPoint(e.GetPosition(SatValPickerGrid));
        }
    }

    private void SatValPicker_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingSatVal && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSatValFromPoint(e.GetPosition(SatValPickerGrid));
        }
    }

    private void SatValPicker_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingSatVal)
        {
            _isDraggingSatVal = false;
            SatValPickerGrid.ReleaseMouseCapture();
        }
    }

    private void UpdateSatValFromPoint(Point p)
    {
        double w = Math.Max(1, SatValPickerGrid.ActualWidth);
        double h = Math.Max(1, SatValPickerGrid.ActualHeight);

        _currentSat = Math.Clamp(p.X / w, 0.0, 1.0);
        _currentVal = Math.Clamp(1.0 - (p.Y / h), 0.0, 1.0);

        UpdateSatValDisplay();
        UpdateColorFromHsv();
    }

    private void UpdateSatValDisplay()
    {
        double w = Math.Max(1, SatValPickerGrid.ActualWidth);
        double h = Math.Max(1, SatValPickerGrid.ActualHeight);

        Canvas.SetLeft(SatValThumb, _currentSat * w);
        Canvas.SetTop(SatValThumb, (1.0 - _currentVal) * h);

        var pureHueColor = HsvToRgb(_currentHue, 1.0, 1.0);
        HueBaseRect.Fill = new SolidColorBrush(pureHueColor);
    }

    // Hue Bar Events
    private void HueBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDraggingHue = true;
            HueBarGrid.CaptureMouse();
            UpdateHueFromPoint(e.GetPosition(HueBarGrid));
        }
    }

    private void HueBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHue && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateHueFromPoint(e.GetPosition(HueBarGrid));
        }
    }

    private void HueBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingHue)
        {
            _isDraggingHue = false;
            HueBarGrid.ReleaseMouseCapture();
        }
    }

    private void UpdateHueFromPoint(Point p)
    {
        double w = Math.Max(1, HueBarGrid.ActualWidth);
        double ratio = Math.Clamp(p.X / w, 0.0, 1.0);
        _currentHue = ratio * 360.0;
        if (_currentHue >= 360.0) _currentHue = 0.0;

        UpdateHueDisplay();
        UpdateSatValDisplay();
        UpdateColorFromHsv();
    }

    private void UpdateHueDisplay()
    {
        double w = Math.Max(1, HueBarGrid.ActualWidth);
        Canvas.SetLeft(HueThumb, (_currentHue / 360.0) * w);
    }

    private void UpdateColorFromHsv()
    {
        var color = HsvToRgb(_currentHue, _currentSat, _currentVal);
        string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        _suppressHexChanged = true;
        if (TxtCustomHex != null) TxtCustomHex.Text = hex;
        _suppressHexChanged = false;

        if (CustomColorPreview != null)
        {
            CustomColorPreview.Background = new SolidColorBrush(color);
        }
    }

    private void TxtCustomHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressHexChanged || CustomColorPreview == null || TxtCustomHex == null) return;
        string hex = TxtCustomHex.Text.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length == 7)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                CustomColorPreview.Background = new SolidColorBrush(color);
                RgbToHsv(color, out _currentHue, out _currentSat, out _currentVal);
                UpdateSatValDisplay();
                UpdateHueDisplay();
            }
            catch { }
        }
    }

    private void BtnApplyCustomColor_Click(object sender, RoutedEventArgs e)
    {
        string hex = TxtCustomHex.Text.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        ApplyTheme(hex);
        ColorPopup.IsOpen = false;
        RequestSave(isTyping: false);
    }

    private static Color HsvToRgb(double hue, double saturation, double value)
    {
        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        double f = hue / 60 - Math.Floor(hue / 60);

        value = value * 255;
        byte v = Convert.ToByte(Math.Clamp(value, 0, 255));
        byte p = Convert.ToByte(Math.Clamp(value * (1 - saturation), 0, 255));
        byte q = Convert.ToByte(Math.Clamp(value * (1 - f * saturation), 0, 255));
        byte t = Convert.ToByte(Math.Clamp(value * (1 - (1 - f) * saturation), 0, 255));

        return hi switch
        {
            0 => Color.FromRgb(v, t, p),
            1 => Color.FromRgb(q, v, p),
            2 => Color.FromRgb(p, v, t),
            3 => Color.FromRgb(p, q, v),
            4 => Color.FromRgb(t, p, v),
            _ => Color.FromRgb(v, p, q)
        };
    }

    private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        value = max;
        saturation = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = (60 * ((g - b) / delta) + 360) % 360;
        }
        else if (max == g)
        {
            hue = (60 * ((b - r) / delta) + 120) % 360;
        }
        else
        {
            hue = (60 * ((r - g) / delta) + 240) % 360;
        }
    }

    private void BtnLock_Click(object sender, RoutedEventArgs e)
    {
        UpdateLockUI(!_note.IsLocked);
        RequestSave(isTyping: false);
    }

    private void BtnMore_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        // Font Size Submenu
        var fontMenu = new MenuItem { Header = LocalizationService.T("Menu_FontSize", $"{TxtContent.FontSize:F0}") };
        fontMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_IncreaseFontSize"), () => AdjustFontSize(2)));
        fontMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_DecreaseFontSize"), () => AdjustFontSize(-2)));
        fontMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_ResetFontSize"), () => SetFontSize(16)));
        menu.Items.Add(fontMenu);

        // Opacity Submenu
        var opacityMenu = new MenuItem { Header = LocalizationService.T("Menu_OpacityHeader", (int)(_userConfiguredOpacity * 100)) };
        opacityMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_Opacity100"), () => SetNoteOpacity(1.0)));
        opacityMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_Opacity90"), () => SetNoteOpacity(0.9)));
        opacityMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_Opacity80"), () => SetNoteOpacity(0.8)));
        opacityMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_Opacity65"), () => SetNoteOpacity(0.65)));
        opacityMenu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_Opacity50"), () => SetNoteOpacity(0.5)));
        opacityMenu.Items.Add(new Separator());

        bool isDimEnabled = _settingsService.Settings.EnableUnfocusedTransparency;
        var toggleDimItem = new MenuItem
        {
            Header = isDimEnabled ? $"✓ {LocalizationService.T("Menu_DimUnfocusedEnabled")}" : LocalizationService.T("Menu_DimUnfocusedDisabled")
        };
        toggleDimItem.Click += (s, ev) =>
        {
            _settingsService.Settings.EnableUnfocusedTransparency = !isDimEnabled;
            _settingsService.Save();
            App.Instance?.NotifyTransparencySettingsChanged();
        };
        opacityMenu.Items.Add(toggleDimItem);
        menu.Items.Add(opacityMenu);

        // Text Proofing Submenu
        var proofingMenu = new MenuItem { Header = LocalizationService.T("Menu_Proofing") };
        bool isSpellCheck = _settingsService.Settings.EnableSpellCheck;
        var toggleSpellCheckItem = new MenuItem
        {
            Header = isSpellCheck ? $"✓ {LocalizationService.T("Menu_SpellCheckEnabled")}" : LocalizationService.T("Menu_SpellCheckDisabled")
        };
        toggleSpellCheckItem.Click += (s, ev) =>
        {
            _settingsService.Settings.EnableSpellCheck = !isSpellCheck;
            _settingsService.Save();
            App.Instance?.NotifyProofingSettingsChanged();
        };
        proofingMenu.Items.Add(toggleSpellCheckItem);

        bool isAutocorrect = _settingsService.Settings.EnableAutocorrect;
        var toggleAutocorrectItem = new MenuItem
        {
            Header = isAutocorrect ? $"✓ {LocalizationService.T("Menu_AutocorrectEnabled")}" : LocalizationService.T("Menu_AutocorrectDisabled")
        };
        toggleAutocorrectItem.Click += (s, ev) =>
        {
            _settingsService.Settings.EnableAutocorrect = !isAutocorrect;
            _settingsService.Save();
        };
        proofingMenu.Items.Add(toggleAutocorrectItem);

        bool isAutoCap = _settingsService.Settings.AutoCapitalizeSentences;
        var toggleAutoCapItem = new MenuItem
        {
            Header = isAutoCap ? $"✓ {LocalizationService.T("Menu_AutoCapitalizeEnabled")}" : LocalizationService.T("Menu_AutoCapitalizeDisabled")
        };
        toggleAutoCapItem.Click += (s, ev) =>
        {
            _settingsService.Settings.AutoCapitalizeSentences = !isAutoCap;
            _settingsService.Save();
        };
        proofingMenu.Items.Add(toggleAutoCapItem);

        bool isSymbols = _settingsService.Settings.SmartSymbolReplacements;
        var toggleSymbolsItem = new MenuItem
        {
            Header = isSymbols ? $"✓ {LocalizationService.T("Menu_SmartSymbolsEnabled")}" : LocalizationService.T("Menu_SmartSymbolsDisabled")
        };
        toggleSymbolsItem.Click += (s, ev) =>
        {
            _settingsService.Settings.SmartSymbolReplacements = !isSymbols;
            _settingsService.Save();
        };
        proofingMenu.Items.Add(toggleSymbolsItem);

        proofingMenu.Items.Add(new Separator());

        var activeLangOption = TextProofingService.SupportedLanguages.FirstOrDefault(l => l.Code.Equals(_settingsService.Settings.ProofingLanguage, StringComparison.OrdinalIgnoreCase))
            ?? TextProofingService.SupportedLanguages[0];

        var langSubmenu = new MenuItem { Header = LocalizationService.T("Menu_ProofingLang", activeLangOption.DisplayName) };
        foreach (var lang in TextProofingService.SupportedLanguages)
        {
            bool isSelected = string.Equals(lang.Code, _settingsService.Settings.ProofingLanguage, StringComparison.OrdinalIgnoreCase);
            var langItem = new MenuItem
            {
                Header = isSelected ? $"✓ {lang.DisplayName}" : lang.DisplayName,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
            };
            langItem.Click += (s, ev) =>
            {
                _settingsService.Settings.ProofingLanguage = lang.Code;
                _settingsService.Save();
                App.Instance?.NotifyProofingSettingsChanged();
            };
            langSubmenu.Items.Add(langItem);
        }
        proofingMenu.Items.Add(langSubmenu);
        menu.Items.Add(proofingMenu);

        menu.Items.Add(new Separator());

        // Copy Note Content
        menu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_CopyAllContent"), CopyAllContent));

        // Export Note as Markdown
        menu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_ExportMarkdown"), ExportNoteAsMarkdown));

        // Duplicate Note
        menu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_DuplicateNote"), () =>
        {
            var clone = _note.Clone();
            _storageService.AddNote(clone);
            _onSpawnNewNote(clone);
        }));

        menu.Items.Add(new Separator());

        // Clear completed checklist items if in checklist mode
        if (_note.ViewMode == NoteViewMode.Checklist)
        {
            menu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_ClearCompletedTasks"), ClearCompletedTasks));
            menu.Items.Add(new Separator());
        }

        // Settings & Preferences
        menu.Items.Add(CreateMenuItem(LocalizationService.T("Tray_Settings"), OpenSettings));

        // Check for Updates
        menu.Items.Add(CreateMenuItem(LocalizationService.T("Tray_CheckUpdates", UpdateService.CurrentVersion), () =>
        {
            _ = UpdateService.CheckForUpdatesAsync(isManualCheck: true);
        }));

        menu.Items.Add(new Separator());

        // Delete Note
        menu.Items.Add(CreateMenuItem(LocalizationService.T("Menu_DeleteNote"), DeleteNote));

        menu.PlacementTarget = BtnMore;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OpenSettings()
    {
        var dlg = new SettingsDialog(_settingsService, _storageService, () =>
        {
            ApplyTheme(_note.ColorKey);
            ApplyFocusOpacity(IsActive);
            ApplyProofingSettings();
            App.Instance?.NotifyProofingSettingsChanged();
            App.Instance?.NotifyTransparencySettingsChanged();
            MemoryOptimizer.TrimMemory();
        });
        dlg.Owner = this;
        dlg.ShowDialog();
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

    private void SliderActiveOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressTransparencyEvents || !_isLoaded) return;

        double val = Math.Clamp(SliderActiveOpacity.Value / 100.0, 0.3, 1.0);
        _userConfiguredOpacity = val;
        _note.Opacity = val;

        if (TxtActiveOpacityPercent != null)
        {
            TxtActiveOpacityPercent.Text = $"{(int)SliderActiveOpacity.Value}%";
        }

        ApplyFocusOpacity(IsActive);
        RequestSave(isTyping: false);
    }

    private void ChkUnfocusedDim_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressTransparencyEvents || !_isLoaded) return;

        bool isEnabled = ChkUnfocusedDim.IsChecked == true;
        _settingsService.Settings.EnableUnfocusedTransparency = isEnabled;
        _settingsService.Save();

        if (UnfocusedOpacityControlsPanel != null)
        {
            UnfocusedOpacityControlsPanel.Opacity = isEnabled ? 1.0 : 0.4;
        }
        if (SliderUnfocusedOpacity != null)
        {
            SliderUnfocusedOpacity.IsEnabled = isEnabled;
        }

        App.Instance?.NotifyTransparencySettingsChanged();
    }

    private void SliderUnfocusedOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressTransparencyEvents || !_isLoaded) return;

        double factor = Math.Clamp(SliderUnfocusedOpacity.Value / 100.0, 0.15, 0.90);
        _settingsService.Settings.UnfocusedOpacity = factor;
        _settingsService.Save();

        if (TxtUnfocusedOpacityPercent != null)
        {
            TxtUnfocusedOpacityPercent.Text = $"{(int)SliderUnfocusedOpacity.Value}%";
        }

        App.Instance?.NotifyTransparencySettingsChanged();
    }

    private void SetNoteOpacity(double opacity)
    {
        _userConfiguredOpacity = Math.Clamp(opacity, 0.3, 1.0);
        _note.Opacity = _userConfiguredOpacity;

        if (SliderActiveOpacity != null && !_suppressTransparencyEvents)
        {
            _suppressTransparencyEvents = true;
            SliderActiveOpacity.Value = Math.Round(_userConfiguredOpacity * 100);
            if (TxtActiveOpacityPercent != null)
            {
                TxtActiveOpacityPercent.Text = $"{(int)SliderActiveOpacity.Value}%";
            }
            _suppressTransparencyEvents = false;
        }

        ApplyFocusOpacity(IsActive);
        RequestSave();
    }

    public void ApplyFocusOpacity(bool isFocused)
    {
        if (!_isLoaded) return;

        double targetOpacity;
        if (isFocused || !_settingsService.Settings.EnableUnfocusedTransparency)
        {
            targetOpacity = Math.Clamp(_userConfiguredOpacity, 0.3, 1.0);
        }
        else
        {
            double unfocusedFactor = _settingsService.Settings.UnfocusedOpacity;
            targetOpacity = Math.Clamp(_userConfiguredOpacity * unfocusedFactor, 0.25, 0.9);
        }

        var anim = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!IsActive && _settingsService.Settings.EnableUnfocusedTransparency)
        {
            double hoverOpacity = Math.Clamp(_userConfiguredOpacity * 0.85, 0.4, 1.0);
            var anim = new DoubleAnimation
            {
                To = hoverOpacity,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!IsActive && _settingsService.Settings.EnableUnfocusedTransparency)
        {
            ApplyFocusOpacity(false);
        }
    }

    private void ResizeEdge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !_note.IsLocked)
        {
            if (sender is FrameworkElement fe && Enum.TryParse<ResizeDirection>(fe.Tag?.ToString(), out var dir))
            {
                e.Handled = true;
                StartWindowResize(dir);
            }
        }
    }

    private void StartWindowResize(ResizeDirection direction)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        _desktopWindowManager.BringToFront();
        Win32Api.ReleaseCapture();
        Win32Api.SendMessage(hwnd, Win32Api.WM_SYSCOMMAND, (IntPtr)(Win32Api.SC_SIZE + (int)direction), IntPtr.Zero);
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
                Filter = LocalizationService.T("Export_Filter"),
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
            ModernMessageBox.Show(
                LocalizationService.T("Export_Failed", ex.Message),
                LocalizationService.T("Export_DialogTitle"),
                ModernMessageButtons.OK,
                ModernMessageIcon.Warning,
                LocalizationService.T("Export_Heading")
            );
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
        MemoryOptimizer.TrimMemory();
    }

    // Checklist Item Events
    private void NewTaskContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        TxtNewTask.Focus();
        e.Handled = true;
    }

    private void TxtNewTask_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        TxtNewTaskPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtNewTask_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtNewTask.Text))
        {
            TxtNewTaskPlaceholder.Visibility = Visibility.Visible;
        }
    }

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
        if (!string.IsNullOrEmpty(TxtNewTask.Text))
        {
            ShowTypingIndicator();
        }
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
            RequestSave(isTyping: false);
        }
    }

    private void CheckItem_Click(object sender, RoutedEventArgs e)
    {
        RequestSave(isTyping: false);
    }

    private void TaskText_Changed(object sender, TextChangedEventArgs e)
    {
        RequestSave(isTyping: true);
    }

    private void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TodoCheckItem item)
        {
            _checklistItems.Remove(item);
            RequestSave(isTyping: false);
        }
    }

    // Copy Compartment Item Events
    private void NewCopyItemContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        TxtNewCopyItem.Focus();
        e.Handled = true;
    }

    private void TxtNewCopyItem_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        TxtNewCopyItemPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtNewCopyItem_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtNewCopyItem.Text))
        {
            TxtNewCopyItemPlaceholder.Visibility = Visibility.Visible;
        }
    }

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
                    icon.Foreground = EmeraldBrush;
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
        RequestSave(isTyping: true);
    }

    private void BtnDeleteCopyItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CopySnippetItem item)
        {
            _copyItems.Remove(item);
            RequestSave(isTyping: false);
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
        if (!string.IsNullOrEmpty(TxtNewCopyItem.Text))
        {
            ShowTypingIndicator();
        }
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
            RequestSave(isTyping: false);
        }
    }

    // --- VERSION 2.0: TEXT PROOFING & AUTOCORRECT ENGINE ---

    private AutocorrectUndoItem? _lastAutocorrect;

    public void ApplyProofingSettings()
    {
        var proofing = TextProofingService.Instance;
        var settings = _settingsService.Settings;

        string langCode = settings.ProofingLanguage;
        if (string.IsNullOrWhiteSpace(langCode) || langCode.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            langCode = System.Globalization.CultureInfo.CurrentUICulture.IetfLanguageTag;
        }

        try
        {
            this.Language = XmlLanguage.GetLanguage(langCode);
        }
        catch
        {
            this.Language = XmlLanguage.GetLanguage("en-US");
        }

        proofing.ApplyProofingToTextBox(TxtContent, settings);
        proofing.ApplyProofingToTextBox(TxtTitle, settings);
        proofing.ApplyProofingToTextBox(TxtNewTask, settings);
        proofing.ApplyProofingToTextBox(TxtNewCopyItem, settings);
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;

        if (sender is TextBox tb)
        {
            char c = e.Text.Length > 0 ? e.Text[0] : ' ';
            if (c == ' ' || c == '.' || c == ',' || c == '!' || c == '?' || c == ';' || c == ':')
            {
                if (TextProofingService.Instance.TryAutocorrect(tb, c, _settingsService.Settings, out var undo))
                {
                    _lastAutocorrect = undo;
                    RequestSave(isTyping: true);
                }
            }
        }
    }

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;

        if (sender is TextBox tb)
        {
            if (e.Key == Key.Back)
            {
                if (TextProofingService.TryUndoAutocorrect(tb, _lastAutocorrect))
                {
                    _lastAutocorrect = null;
                    e.Handled = true;
                    RequestSave(isTyping: true);
                    return;
                }
                _lastAutocorrect = null;
            }
            else if (e.Key == Key.Space || e.Key == Key.Return || e.Key == Key.Tab)
            {
                char delim = (e.Key == Key.Return) ? '\n' : ' ';
                if (TextProofingService.Instance.TryAutocorrect(tb, delim, _settingsService.Settings, out var undo))
                {
                    _lastAutocorrect = undo;
                    RequestSave(isTyping: true);
                }
            }
        }
    }

    private void TextBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            Point mousePos = e.GetPosition(tb);
            int charIdx = tb.GetCharacterIndexFromPoint(mousePos, snapToText: true);
            if (charIdx >= 0)
            {
                tb.CaretIndex = charIdx;
            }
        }
    }

    private void TextBox_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb) return;
        e.Handled = true; // Intercepts tunneling right-click to completely suppress WPF's default white menu!

        ShowObsidianContextMenu(tb, e.GetPosition(tb));
    }

    private void TextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TextBox tb) return;
        e.Handled = true; // Suppress any fallback system menu
    }

    private void ShowObsidianContextMenu(TextBox tb, Point mousePos)
    {
        var menu = new ContextMenu();
        if (Application.Current.TryFindResource(typeof(ContextMenu)) is Style cmStyle)
        {
            menu.Style = cmStyle;
        }

        var settings = _settingsService.Settings;

        // 1. Spelling Suggestions if right-clicked on or adjacent to an error
        int charIndex = tb.GetCharacterIndexFromPoint(mousePos, snapToText: true);
        SpellingError? error = null;
        int targetIndex = -1;

        if (charIndex >= 0)
        {
            error = tb.GetSpellingError(charIndex);
            if (error != null)
            {
                targetIndex = charIndex;
            }
            else if (charIndex > 0)
            {
                error = tb.GetSpellingError(charIndex - 1);
                if (error != null)
                {
                    targetIndex = charIndex - 1;
                }
            }
        }

        if (error == null && tb.CaretIndex >= 0)
        {
            error = tb.GetSpellingError(tb.CaretIndex);
            if (error != null)
            {
                targetIndex = tb.CaretIndex;
            }
            else if (tb.CaretIndex > 0)
            {
                error = tb.GetSpellingError(tb.CaretIndex - 1);
                if (error != null)
                {
                    targetIndex = tb.CaretIndex - 1;
                }
            }
        }

        if (error != null && targetIndex >= 0)
        {
            var headerItem = new MenuItem
            {
                Header = LocalizationService.T("Menu_SpellingSuggestions"),
                IsEnabled = false,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Foreground = AmberBrush,
                Opacity = 1.0,
                Padding = new Thickness(9, 4, 9, 2)
            };
            menu.Items.Add(headerItem);
            
            var suggestions = error.Suggestions.Take(5).ToList();
            int errStart = tb.GetSpellingErrorStart(targetIndex);
            int errLen = tb.GetSpellingErrorLength(targetIndex);
            string misspelled = (errStart >= 0 && errStart + errLen <= tb.Text.Length) ? tb.Text.Substring(errStart, errLen) : "";

            if (suggestions.Count > 0)
            {
                foreach (var suggestion in suggestions)
                {
                    var suggItem = new MenuItem
                    {
                        Header = suggestion,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13.5,
                        Foreground = new SolidColorBrush(Colors.White),
                        Icon = new LucideIcon { IconKey = "check", Size = 13, StrokeThickness = 2.0, Foreground = EmeraldBrush }
                    };
                    suggItem.Click += (s, ev) =>
                    {
                        error.Correct(suggestion);
                        RequestSave(isTyping: true);
                    };
                    menu.Items.Add(suggItem);
                }
            }
            else
            {
                var noSugg = new MenuItem
                {
                    Header = LocalizationService.T("Menu_NoSuggestions"),
                    IsEnabled = false,
                    Icon = new LucideIcon { IconKey = "spell-check", Size = 13, StrokeThickness = 1.8, Foreground = MutedBrush }
                };
                menu.Items.Add(noSugg);
            }

            menu.Items.Add(new Separator());

            if (!string.IsNullOrEmpty(misspelled))
            {
                var addDictItem = new MenuItem
                {
                    Header = LocalizationService.T("Menu_AddToDict", misspelled),
                    Icon = new LucideIcon { IconKey = "book-plus", Size = 14, StrokeThickness = 1.8, Foreground = AmberBrush }
                };
                addDictItem.Click += (s, ev) =>
                {
                    TextProofingService.Instance.AddWordToCustomDictionary(misspelled);
                    ApplyProofingSettings();
                };
                menu.Items.Add(addDictItem);
            }

            var ignoreItem = new MenuItem
            {
                Header = LocalizationService.T("Menu_IgnoreAll"),
                Icon = new LucideIcon { IconKey = "eye-off", Size = 14, StrokeThickness = 1.8, Foreground = MutedBrush }
            };
            ignoreItem.Click += (s, ev) =>
            {
                error.IgnoreAll();
            };
            menu.Items.Add(ignoreItem);

            menu.Items.Add(new Separator());
        }

        // 2. Standard Clipboard Actions
        var cutItem = new MenuItem
        {
            Header = LocalizationService.T("Menu_Cut"),
            InputGestureText = "Ctrl+X",
            IsEnabled = !tb.IsReadOnly && tb.SelectionLength > 0,
            Icon = new LucideIcon { IconKey = "scissors", Size = 13, StrokeThickness = 1.8, Foreground = MutedBrush }
        };
        cutItem.Click += (s, ev) => tb.Cut();
        menu.Items.Add(cutItem);

        var copyItem = new MenuItem
        {
            Header = LocalizationService.T("Menu_Copy"),
            InputGestureText = "Ctrl+C",
            IsEnabled = tb.SelectionLength > 0,
            Icon = new LucideIcon { IconKey = "copy", Size = 13, StrokeThickness = 1.8, Foreground = MutedBrush }
        };
        copyItem.Click += (s, ev) => tb.Copy();
        menu.Items.Add(copyItem);

        var pasteItem = new MenuItem
        {
            Header = LocalizationService.T("Menu_Paste"),
            InputGestureText = "Ctrl+V",
            IsEnabled = !tb.IsReadOnly && Clipboard.ContainsText(),
            Icon = new LucideIcon { IconKey = "clipboard", Size = 13, StrokeThickness = 1.8, Foreground = MutedBrush }
        };
        pasteItem.Click += (s, ev) => tb.Paste();
        menu.Items.Add(pasteItem);

        var selectAllItem = new MenuItem
        {
            Header = LocalizationService.T("Menu_SelectAll"),
            InputGestureText = "Ctrl+A",
            IsEnabled = tb.Text.Length > 0,
            Icon = new LucideIcon { IconKey = "square", Size = 13, StrokeThickness = 1.8, Foreground = MutedBrush }
        };
        selectAllItem.Click += (s, ev) => tb.SelectAll();
        menu.Items.Add(selectAllItem);

        menu.Items.Add(new Separator());

        // 3. Text Proofing & Language Submenu
        var proofingMenu = new MenuItem
        {
            Header = LocalizationService.T("Menu_Proofing"),
            Icon = new LucideIcon { IconKey = "globe", Size = 14, StrokeThickness = 1.9, Foreground = AmberBrush }
        };

        // Spell Check Toggle
        var spellCheckToggle = new MenuItem
        {
            Header = settings.EnableSpellCheck ? $"✓ {LocalizationService.T("Menu_SpellCheck")}" : LocalizationService.T("Menu_SpellCheck")
        };
        spellCheckToggle.Click += (s, ev) =>
        {
            settings.EnableSpellCheck = !settings.EnableSpellCheck;
            _settingsService.Save();
            App.Instance?.NotifyProofingSettingsChanged();
        };
        proofingMenu.Items.Add(spellCheckToggle);

        // Autocorrect Toggle
        var autocorrectToggle = new MenuItem
        {
            Header = settings.EnableAutocorrect ? $"✓ {LocalizationService.T("Menu_Autocorrect")}" : LocalizationService.T("Menu_Autocorrect")
        };
        autocorrectToggle.Click += (s, ev) =>
        {
            settings.EnableAutocorrect = !settings.EnableAutocorrect;
            _settingsService.Save();
        };
        proofingMenu.Items.Add(autocorrectToggle);

        // Sentence Capitalization Toggle
        var autoCapToggle = new MenuItem
        {
            Header = settings.AutoCapitalizeSentences ? $"✓ {LocalizationService.T("Menu_AutoCapitalize")}" : LocalizationService.T("Menu_AutoCapitalize")
        };
        autoCapToggle.Click += (s, ev) =>
        {
            settings.AutoCapitalizeSentences = !settings.AutoCapitalizeSentences;
            _settingsService.Save();
        };
        proofingMenu.Items.Add(autoCapToggle);

        // Smart Symbols Toggle
        var smartSymToggle = new MenuItem
        {
            Header = settings.SmartSymbolReplacements ? $"✓ {LocalizationService.T("Menu_SmartSymbols")}" : LocalizationService.T("Menu_SmartSymbols")
        };
        smartSymToggle.Click += (s, ev) =>
        {
            settings.SmartSymbolReplacements = !settings.SmartSymbolReplacements;
            _settingsService.Save();
        };
        proofingMenu.Items.Add(smartSymToggle);

        proofingMenu.Items.Add(new Separator());

        // Language Selection Submenu
        var activeLangOption = TextProofingService.SupportedLanguages.FirstOrDefault(l => l.Code.Equals(settings.ProofingLanguage, StringComparison.OrdinalIgnoreCase))
            ?? TextProofingService.SupportedLanguages[0];

        var langSubmenu = new MenuItem
        {
            Header = $"{LocalizationService.T("Menu_Language")} ({activeLangOption.DisplayName})",
            Icon = new LucideIcon { IconKey = "languages", Size = 13, StrokeThickness = 1.8, Foreground = MutedBrush }
        };

        foreach (var lang in TextProofingService.SupportedLanguages)
        {
            bool isSelected = string.Equals(lang.Code, settings.ProofingLanguage, StringComparison.OrdinalIgnoreCase);
            var langItem = new MenuItem
            {
                Header = isSelected ? $"✓ {lang.DisplayName}" : lang.DisplayName,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
            };
            langItem.Click += (s, ev) =>
            {
                settings.ProofingLanguage = lang.Code;
                _settingsService.Save();
                App.Instance?.NotifyProofingSettingsChanged();
            };
            langSubmenu.Items.Add(langItem);
        }
        proofingMenu.Items.Add(langSubmenu);

        menu.Items.Add(proofingMenu);

        tb.ContextMenu = menu;
        menu.PlacementTarget = tb;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}
