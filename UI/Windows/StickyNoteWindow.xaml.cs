using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
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
            private ItemsControl? ChecklistItemsControl;
    private Grid? ChecklistModeContainer;
    private ItemsControl? CopyItemsControl;
    private Grid? CopyModeContainer;
    private Border? CustomColorPreview;
    private System.Windows.Shapes.Rectangle? HueBaseRect;
    private Border? HueThumb;
    private LucideIcon? IconChecklistMode;
    private LucideIcon? IconCopyMode;
    private LucideIcon? IconLock;
    private LucideIcon? IconPin;
    private LucideIcon? IconPlus;
    private LucideIcon? IconTextMode;
    private LucideIcon? IconTypingStatus;
    private Border? LeftCompartmentBorder;
    private StackPanel? LockBadge;
    private Border? ModeCompartmentBorder;
    private Border? NoteCardBorder;
    private Grid? ResizeOverlayGrid;
    private System.Windows.Shapes.Ellipse? SatValThumb;
    private Grid? TextModeContainer;
    private Border? ToolsCompartmentBorder;
    private TextBlock? TxtActiveOpacityPercent;
    private TextBlock? TxtContentPlaceholder;
    private TextBlock? TxtModifiedTime;
    private TextBlock? TxtNewCopyItemPlaceholder;
    private TextBlock? TxtNewTaskPlaceholder;
    private TextBlock? TxtPinIndicator;
    private TextBlock? TxtTitlePlaceholder;
    private TextBlock? TxtTypingStatus;
    private TextBlock? TxtUnfocusedOpacityPercent;
    private StackPanel? TypingIndicatorContainer;
    private StackPanel? UnfocusedOpacityControlsPanel;
private Border? HeaderBorder;
    private Button? Unnamed_Button_1;
    private Button? BtnPinMode;
    private Button? BtnTextMode;
    private Button? BtnChecklistMode;
    private Button? BtnCopyMode;
    private Button? BtnColorPicker;
    private Button? BtnLock;
    private Button? BtnMore;
    private Button? Unnamed_Button_9;
    private TextBox? TxtTitle;
    internal TextBox? TxtContent;
    private Border? NewTaskContainer;
    private TextBox? TxtNewTask;
    private Button? Unnamed_Button_14;
    private Border? NewCopyItemContainer;
    private TextBox? TxtNewCopyItem;
    private Button? Unnamed_Button_17;
    private Popup? ColorPopup;
    private Button? Unnamed_Button_19;
    private Button? Unnamed_Button_20;
    private Button? Unnamed_Button_21;
    private Button? Unnamed_Button_22;
    private Button? Unnamed_Button_23;
    private Button? Unnamed_Button_24;
    private Button? Unnamed_Button_25;
    private Button? Unnamed_Button_26;
    private Grid? SatValPickerGrid;
    private Grid? HueBarGrid;
    private TextBox? TxtCustomHex;
    private Button? Unnamed_Button_30;
    private Slider? SliderActiveOpacity;
    private CheckBox? ChkUnfocusedDim;
    private Slider? SliderUnfocusedOpacity;
    private System.Windows.Shapes.Rectangle? ResizeEdgeTop;
    private System.Windows.Shapes.Rectangle? ResizeEdgeBottom;
    private System.Windows.Shapes.Rectangle? ResizeEdgeLeft;
    private System.Windows.Shapes.Rectangle? ResizeEdgeRight;
    private System.Windows.Shapes.Rectangle? ResizeCornerTopLeft;
    private System.Windows.Shapes.Rectangle? ResizeCornerTopRight;
    private System.Windows.Shapes.Rectangle? ResizeCornerBottomLeft;
    private System.Windows.Shapes.Rectangle? ResizeCornerBottomRight;


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
        ThemeManager.ApplyTheme(this, _note.ThemeKey);

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
    }


    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Unbind previous
        if (HeaderBorder != null)
        {
            HeaderBorder.MouseLeftButtonDown -= Header_MouseLeftButtonDown;
        }
        if (Unnamed_Button_1 != null)
        {
            Unnamed_Button_1.Click -= BtnNewNote_Click;
        }
        if (BtnPinMode != null)
        {
            if (BtnPinMode != null) BtnPinMode.Click -= BtnPinMode_Click;
        }
        if (BtnTextMode != null)
        {
            if (BtnTextMode != null) BtnTextMode.Click -= BtnTextMode_Click;
        }
        if (BtnChecklistMode != null)
        {
            if (BtnChecklistMode != null) BtnChecklistMode.Click -= BtnChecklistMode_Click;
        }
        if (BtnCopyMode != null)
        {
            if (BtnCopyMode != null) BtnCopyMode.Click -= BtnCopyMode_Click;
        }
        if (BtnColorPicker != null)
        {
            BtnColorPicker.Click -= BtnColorPicker_Click;
        }
        if (BtnLock != null)
        {
            if (BtnLock != null) BtnLock.Click -= BtnLock_Click;
        }
        if (BtnMore != null)
        {
            BtnMore.Click -= BtnMore_Click;
        }
        if (Unnamed_Button_9 != null)
        {
            Unnamed_Button_9.Click -= BtnClose_Click;
        }
        if (TxtTitle != null)
        {
            if (TxtTitle != null) TxtTitle.TextChanged -= TxtTitle_TextChanged;
            if (TxtTitle != null) TxtTitle.GotFocus -= TxtTitle_GotFocus;
            if (TxtTitle != null) TxtTitle.LostFocus -= TxtTitle_LostFocus;
        }
        if (TxtContent != null)
        {
            if (TxtContent != null) TxtContent.TextChanged -= TxtContent_TextChanged;
            if (TxtContent != null) TxtContent.GotFocus -= TxtContent_GotFocus;
            if (TxtContent != null) TxtContent.LostFocus -= TxtContent_LostFocus;
        }
        if (NewTaskContainer != null)
        {
            NewTaskContainer.MouseLeftButtonDown -= NewTaskContainer_MouseLeftButtonDown;
        }
        if (TxtNewTask != null)
        {
            if (TxtNewTask != null) TxtNewTask.KeyDown -= TxtNewTask_KeyDown;
            if (TxtNewTask != null) TxtNewTask.TextChanged -= TxtNewTask_TextChanged;
            if (TxtNewTask != null) TxtNewTask.GotFocus -= TxtNewTask_GotFocus;
            if (TxtNewTask != null) TxtNewTask.LostFocus -= TxtNewTask_LostFocus;
        }
        if (Unnamed_Button_14 != null)
        {
            Unnamed_Button_14.Click -= BtnAddTask_Click;
        }
        if (NewCopyItemContainer != null)
        {
            NewCopyItemContainer.MouseLeftButtonDown -= NewCopyItemContainer_MouseLeftButtonDown;
        }
        if (TxtNewCopyItem != null)
        {
            if (TxtNewCopyItem != null) TxtNewCopyItem.KeyDown -= TxtNewCopyItem_KeyDown;
            if (TxtNewCopyItem != null) TxtNewCopyItem.TextChanged -= TxtNewCopyItem_TextChanged;
            if (TxtNewCopyItem != null) TxtNewCopyItem.GotFocus -= TxtNewCopyItem_GotFocus;
            if (TxtNewCopyItem != null) TxtNewCopyItem.LostFocus -= TxtNewCopyItem_LostFocus;
        }
        if (Unnamed_Button_17 != null)
        {
            Unnamed_Button_17.Click -= BtnAddCopyItem_Click;
        }
        if (ColorPopup != null)
        {
            if (ColorPopup != null) ColorPopup.Opened -= ColorPopup_Opened;
        }
        if (Unnamed_Button_19 != null)
        {
            Unnamed_Button_19.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_20 != null)
        {
            Unnamed_Button_20.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_21 != null)
        {
            Unnamed_Button_21.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_22 != null)
        {
            Unnamed_Button_22.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_23 != null)
        {
            Unnamed_Button_23.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_24 != null)
        {
            Unnamed_Button_24.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_25 != null)
        {
            Unnamed_Button_25.Click -= ColorSwatch_Click;
        }
        if (Unnamed_Button_26 != null)
        {
            Unnamed_Button_26.Click -= ColorSwatch_Click;
        }
        if (SatValPickerGrid != null)
        {
            SatValPickerGrid.MouseLeftButtonDown -= SatValPicker_MouseDown;
            SatValPickerGrid.MouseMove -= SatValPicker_MouseMove;
            SatValPickerGrid.MouseLeftButtonUp -= SatValPicker_MouseUp;
        }
        if (HueBarGrid != null)
        {
            HueBarGrid.MouseLeftButtonDown -= HueBar_MouseDown;
            HueBarGrid.MouseMove -= HueBar_MouseMove;
            HueBarGrid.MouseLeftButtonUp -= HueBar_MouseUp;
        }
        if (TxtCustomHex != null)
        {
            if (TxtCustomHex != null) TxtCustomHex.TextChanged -= TxtCustomHex_TextChanged;
        }
        if (Unnamed_Button_30 != null)
        {
            Unnamed_Button_30.Click -= BtnApplyCustomColor_Click;
        }
        if (SliderActiveOpacity != null)
        {
            if (SliderActiveOpacity != null) SliderActiveOpacity.ValueChanged -= SliderActiveOpacity_ValueChanged;
        }
        if (ChkUnfocusedDim != null)
        {
            if (ChkUnfocusedDim != null) ChkUnfocusedDim.Click -= ChkUnfocusedDim_Click;
        }
        if (SliderUnfocusedOpacity != null)
        {
            if (SliderUnfocusedOpacity != null) SliderUnfocusedOpacity.ValueChanged -= SliderUnfocusedOpacity_ValueChanged;
        }
        if (ResizeEdgeTop != null)
        {
            ResizeEdgeTop.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeEdgeBottom != null)
        {
            ResizeEdgeBottom.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeEdgeLeft != null)
        {
            ResizeEdgeLeft.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeEdgeRight != null)
        {
            ResizeEdgeRight.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerTopLeft != null)
        {
            ResizeCornerTopLeft.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerTopRight != null)
        {
            ResizeCornerTopRight.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerBottomLeft != null)
        {
            ResizeCornerBottomLeft.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerBottomRight != null)
        {
            ResizeCornerBottomRight.MouseLeftButtonDown -= ResizeEdge_MouseLeftButtonDown;
        }



        // Bind new
        HeaderBorder = GetTemplateChild("PART_HeaderBorder") as Border;
        Unnamed_Button_1 = GetTemplateChild("PART_Unnamed_Button_1") as Button;
        BtnPinMode = GetTemplateChild("PART_BtnPinMode") as Button;
        BtnTextMode = GetTemplateChild("PART_BtnTextMode") as Button;
        BtnChecklistMode = GetTemplateChild("PART_BtnChecklistMode") as Button;
        BtnCopyMode = GetTemplateChild("PART_BtnCopyMode") as Button;
        BtnColorPicker = GetTemplateChild("PART_BtnColorPicker") as Button;
        BtnLock = GetTemplateChild("PART_BtnLock") as Button;
        BtnMore = GetTemplateChild("PART_BtnMore") as Button;
        Unnamed_Button_9 = GetTemplateChild("PART_Unnamed_Button_9") as Button;
        TxtTitle = GetTemplateChild("PART_TxtTitle") as TextBox;
        TxtContent = GetTemplateChild("PART_TxtContent") as TextBox;
        NewTaskContainer = GetTemplateChild("PART_NewTaskContainer") as Border;
        TxtNewTask = GetTemplateChild("PART_TxtNewTask") as TextBox;
        Unnamed_Button_14 = GetTemplateChild("PART_Unnamed_Button_14") as Button;
        NewCopyItemContainer = GetTemplateChild("PART_NewCopyItemContainer") as Border;
        TxtNewCopyItem = GetTemplateChild("PART_TxtNewCopyItem") as TextBox;
        Unnamed_Button_17 = GetTemplateChild("PART_Unnamed_Button_17") as Button;
        ColorPopup = GetTemplateChild("PART_ColorPopup") as Popup;
        Unnamed_Button_19 = GetTemplateChild("PART_Unnamed_Button_19") as Button;
        Unnamed_Button_20 = GetTemplateChild("PART_Unnamed_Button_20") as Button;
        Unnamed_Button_21 = GetTemplateChild("PART_Unnamed_Button_21") as Button;
        Unnamed_Button_22 = GetTemplateChild("PART_Unnamed_Button_22") as Button;
        Unnamed_Button_23 = GetTemplateChild("PART_Unnamed_Button_23") as Button;
        Unnamed_Button_24 = GetTemplateChild("PART_Unnamed_Button_24") as Button;
        Unnamed_Button_25 = GetTemplateChild("PART_Unnamed_Button_25") as Button;
        Unnamed_Button_26 = GetTemplateChild("PART_Unnamed_Button_26") as Button;
        SatValPickerGrid = GetTemplateChild("PART_SatValPickerGrid") as Grid;
        HueBarGrid = GetTemplateChild("PART_HueBarGrid") as Grid;
        TxtCustomHex = GetTemplateChild("PART_TxtCustomHex") as TextBox;
        Unnamed_Button_30 = GetTemplateChild("PART_Unnamed_Button_30") as Button;
        SliderActiveOpacity = GetTemplateChild("PART_SliderActiveOpacity") as Slider;
        ChkUnfocusedDim = GetTemplateChild("PART_ChkUnfocusedDim") as CheckBox;
        SliderUnfocusedOpacity = GetTemplateChild("PART_SliderUnfocusedOpacity") as Slider;
        ResizeEdgeTop = GetTemplateChild("PART_ResizeEdgeTop") as System.Windows.Shapes.Rectangle;
        ResizeEdgeBottom = GetTemplateChild("PART_ResizeEdgeBottom") as System.Windows.Shapes.Rectangle;
        ResizeEdgeLeft = GetTemplateChild("PART_ResizeEdgeLeft") as System.Windows.Shapes.Rectangle;
        ResizeEdgeRight = GetTemplateChild("PART_ResizeEdgeRight") as System.Windows.Shapes.Rectangle;
        ResizeCornerTopLeft = GetTemplateChild("PART_ResizeCornerTopLeft") as System.Windows.Shapes.Rectangle;
        ResizeCornerTopRight = GetTemplateChild("PART_ResizeCornerTopRight") as System.Windows.Shapes.Rectangle;
        ResizeCornerBottomLeft = GetTemplateChild("PART_ResizeCornerBottomLeft") as System.Windows.Shapes.Rectangle;
        ResizeCornerBottomRight = GetTemplateChild("PART_ResizeCornerBottomRight") as System.Windows.Shapes.Rectangle;
        if (HeaderBorder != null)
        {
            HeaderBorder.MouseLeftButtonDown += Header_MouseLeftButtonDown;
            ChecklistItemsControl = GetTemplateChild("PART_ChecklistItemsControl") as ItemsControl;
        ChecklistModeContainer = GetTemplateChild("PART_ChecklistModeContainer") as Grid;
        CopyItemsControl = GetTemplateChild("PART_CopyItemsControl") as ItemsControl;
        CopyModeContainer = GetTemplateChild("PART_CopyModeContainer") as Grid;
        CustomColorPreview = GetTemplateChild("PART_CustomColorPreview") as Border;
        HueBaseRect = GetTemplateChild("PART_HueBaseRect") as System.Windows.Shapes.Rectangle;
        HueThumb = GetTemplateChild("PART_HueThumb") as Border;
        IconChecklistMode = GetTemplateChild("PART_IconChecklistMode") as LucideIcon;
        IconCopyMode = GetTemplateChild("PART_IconCopyMode") as LucideIcon;
        IconLock = GetTemplateChild("PART_IconLock") as LucideIcon;
        IconPin = GetTemplateChild("PART_IconPin") as LucideIcon;
        IconPlus = GetTemplateChild("PART_IconPlus") as LucideIcon;
        IconTextMode = GetTemplateChild("PART_IconTextMode") as LucideIcon;
        IconTypingStatus = GetTemplateChild("PART_IconTypingStatus") as LucideIcon;
        LeftCompartmentBorder = GetTemplateChild("PART_LeftCompartmentBorder") as Border;
        LockBadge = GetTemplateChild("PART_LockBadge") as StackPanel;
        ModeCompartmentBorder = GetTemplateChild("PART_ModeCompartmentBorder") as Border;
        NoteCardBorder = GetTemplateChild("PART_NoteCardBorder") as Border;
        ResizeOverlayGrid = GetTemplateChild("PART_ResizeOverlayGrid") as Grid;
        SatValThumb = GetTemplateChild("PART_SatValThumb") as System.Windows.Shapes.Ellipse;
        TextModeContainer = GetTemplateChild("PART_TextModeContainer") as Grid;
        ToolsCompartmentBorder = GetTemplateChild("PART_ToolsCompartmentBorder") as Border;
        TxtActiveOpacityPercent = GetTemplateChild("PART_TxtActiveOpacityPercent") as TextBlock;
        TxtContentPlaceholder = GetTemplateChild("PART_TxtContentPlaceholder") as TextBlock;
        TxtModifiedTime = GetTemplateChild("PART_TxtModifiedTime") as TextBlock;
        TxtNewCopyItemPlaceholder = GetTemplateChild("PART_TxtNewCopyItemPlaceholder") as TextBlock;
        TxtNewTaskPlaceholder = GetTemplateChild("PART_TxtNewTaskPlaceholder") as TextBlock;
        TxtPinIndicator = GetTemplateChild("PART_TxtPinIndicator") as TextBlock;
        TxtTitlePlaceholder = GetTemplateChild("PART_TxtTitlePlaceholder") as TextBlock;
        TxtTypingStatus = GetTemplateChild("PART_TxtTypingStatus") as TextBlock;
        TxtUnfocusedOpacityPercent = GetTemplateChild("PART_TxtUnfocusedOpacityPercent") as TextBlock;
        TypingIndicatorContainer = GetTemplateChild("PART_TypingIndicatorContainer") as StackPanel;
        UnfocusedOpacityControlsPanel = GetTemplateChild("PART_UnfocusedOpacityControlsPanel") as StackPanel;
    }
        if (Unnamed_Button_1 != null)
        {
            Unnamed_Button_1.Click += BtnNewNote_Click;
        }
        if (BtnPinMode != null)
        {
            if (BtnPinMode != null) BtnPinMode.Click += BtnPinMode_Click;
        }
        if (BtnTextMode != null)
        {
            if (BtnTextMode != null) BtnTextMode.Click += BtnTextMode_Click;
        }
        if (BtnChecklistMode != null)
        {
            if (BtnChecklistMode != null) BtnChecklistMode.Click += BtnChecklistMode_Click;
        }
        if (BtnCopyMode != null)
        {
            if (BtnCopyMode != null) BtnCopyMode.Click += BtnCopyMode_Click;
        }
        if (BtnColorPicker != null)
        {
            BtnColorPicker.Click += BtnColorPicker_Click;
        }
        if (BtnLock != null)
        {
            if (BtnLock != null) BtnLock.Click += BtnLock_Click;
        }
        if (BtnMore != null)
        {
            BtnMore.Click += BtnMore_Click;
        }
        if (Unnamed_Button_9 != null)
        {
            Unnamed_Button_9.Click += BtnClose_Click;
        }
        if (TxtTitle != null)
        {
            if (TxtTitle != null) TxtTitle.TextChanged += TxtTitle_TextChanged;
            if (TxtTitle != null) TxtTitle.GotFocus += TxtTitle_GotFocus;
            if (TxtTitle != null) TxtTitle.LostFocus += TxtTitle_LostFocus;
        }
        if (TxtContent != null)
        {
            if (TxtContent != null) TxtContent.TextChanged += TxtContent_TextChanged;
            if (TxtContent != null) TxtContent.GotFocus += TxtContent_GotFocus;
            if (TxtContent != null) TxtContent.LostFocus += TxtContent_LostFocus;
        }
        if (NewTaskContainer != null)
        {
            NewTaskContainer.MouseLeftButtonDown += NewTaskContainer_MouseLeftButtonDown;
        }
        if (TxtNewTask != null)
        {
            if (TxtNewTask != null) TxtNewTask.KeyDown += TxtNewTask_KeyDown;
            if (TxtNewTask != null) TxtNewTask.TextChanged += TxtNewTask_TextChanged;
            if (TxtNewTask != null) TxtNewTask.GotFocus += TxtNewTask_GotFocus;
            if (TxtNewTask != null) TxtNewTask.LostFocus += TxtNewTask_LostFocus;
        }
        if (Unnamed_Button_14 != null)
        {
            Unnamed_Button_14.Click += BtnAddTask_Click;
        }
        if (NewCopyItemContainer != null)
        {
            NewCopyItemContainer.MouseLeftButtonDown += NewCopyItemContainer_MouseLeftButtonDown;
        }
        if (TxtNewCopyItem != null)
        {
            if (TxtNewCopyItem != null) TxtNewCopyItem.KeyDown += TxtNewCopyItem_KeyDown;
            if (TxtNewCopyItem != null) TxtNewCopyItem.TextChanged += TxtNewCopyItem_TextChanged;
            if (TxtNewCopyItem != null) TxtNewCopyItem.GotFocus += TxtNewCopyItem_GotFocus;
            if (TxtNewCopyItem != null) TxtNewCopyItem.LostFocus += TxtNewCopyItem_LostFocus;
        }
        if (Unnamed_Button_17 != null)
        {
            Unnamed_Button_17.Click += BtnAddCopyItem_Click;
        }
        if (ColorPopup != null)
        {
            if (ColorPopup != null) ColorPopup.Opened += ColorPopup_Opened;
        }
        if (Unnamed_Button_19 != null)
        {
            Unnamed_Button_19.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_20 != null)
        {
            Unnamed_Button_20.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_21 != null)
        {
            Unnamed_Button_21.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_22 != null)
        {
            Unnamed_Button_22.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_23 != null)
        {
            Unnamed_Button_23.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_24 != null)
        {
            Unnamed_Button_24.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_25 != null)
        {
            Unnamed_Button_25.Click += ColorSwatch_Click;
        }
        if (Unnamed_Button_26 != null)
        {
            Unnamed_Button_26.Click += ColorSwatch_Click;
        }
        if (SatValPickerGrid != null)
        {
            SatValPickerGrid.MouseLeftButtonDown += SatValPicker_MouseDown;
            SatValPickerGrid.MouseMove += SatValPicker_MouseMove;
            SatValPickerGrid.MouseLeftButtonUp += SatValPicker_MouseUp;
        }
        if (HueBarGrid != null)
        {
            HueBarGrid.MouseLeftButtonDown += HueBar_MouseDown;
            HueBarGrid.MouseMove += HueBar_MouseMove;
            HueBarGrid.MouseLeftButtonUp += HueBar_MouseUp;
        }
        if (TxtCustomHex != null)
        {
            if (TxtCustomHex != null) TxtCustomHex.TextChanged += TxtCustomHex_TextChanged;
        }
        if (Unnamed_Button_30 != null)
        {
            Unnamed_Button_30.Click += BtnApplyCustomColor_Click;
        }
        if (SliderActiveOpacity != null)
        {
            if (SliderActiveOpacity != null) SliderActiveOpacity.ValueChanged += SliderActiveOpacity_ValueChanged;
        }
        if (ChkUnfocusedDim != null)
        {
            if (ChkUnfocusedDim != null) ChkUnfocusedDim.Click += ChkUnfocusedDim_Click;
        }
        if (SliderUnfocusedOpacity != null)
        {
            if (SliderUnfocusedOpacity != null) SliderUnfocusedOpacity.ValueChanged += SliderUnfocusedOpacity_ValueChanged;
        }
        if (ResizeEdgeTop != null)
        {
            ResizeEdgeTop.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeEdgeBottom != null)
        {
            ResizeEdgeBottom.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeEdgeLeft != null)
        {
            ResizeEdgeLeft.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeEdgeRight != null)
        {
            ResizeEdgeRight.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerTopLeft != null)
        {
            ResizeCornerTopLeft.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerTopRight != null)
        {
            ResizeCornerTopRight.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerBottomLeft != null)
        {
            ResizeCornerBottomLeft.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }
        if (ResizeCornerBottomRight != null)
        {
            ResizeCornerBottomRight.MouseLeftButtonDown += ResizeEdge_MouseLeftButtonDown;
        }

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
        if (_startInForeground || _note.PinMode == NotePinMode.AlwaysOnTop)
        {
            _desktopWindowManager.BringToFront();
            if (TxtContent != null) TxtContent.Focus();
        }
        _isLoaded = true;
        ApplyFocusOpacity(IsActive || _startInForeground);
    }

    private void ApplyNoteData()
    {
        if (TxtTitle != null) TxtTitle.Text = _note.Title;
        if (TxtContent != null) TxtContent.Text = _note.Content;
        if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Visibility = string.IsNullOrEmpty(_note.Title) ? Visibility.Visible : Visibility.Collapsed;
        if (TxtContentPlaceholder != null) TxtContentPlaceholder.Visibility = string.IsNullOrEmpty(_note.Content) ? Visibility.Visible : Visibility.Collapsed;

        if (TxtContent != null) TxtContent.FontSize = Math.Max(11, _note.FontSize);
        if (TxtTitle != null) TxtTitle.FontSize = Math.Max(12, _note.FontSize + 1);

        // Checklist setup
        _checklistItems = new ObservableCollection<TodoCheckItem>(_note.Checklist);
        if (ChecklistItemsControl != null) ChecklistItemsControl.ItemsSource = _checklistItems;

        // Copy list setup
        _copyItems = new ObservableCollection<CopySnippetItem>(_note.CopyList);
        if (CopyItemsControl != null) CopyItemsControl.ItemsSource = _copyItems;

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
            
            if (_note.ThemeKey != "Cyberpunk")
            {
                if (NoteCardBorder != null)
                {
                    NoteCardBorder.Background = theme.BgBrush;
                    NoteCardBorder.BorderBrush = theme.BorderBrush;
                }
                if (HeaderBorder != null)
                {
                    HeaderBorder.Background = theme.HeaderBgBrush;
                    HeaderBorder.BorderBrush = theme.BorderBrush;
                }
                if (LeftCompartmentBorder != null) LeftCompartmentBorder.BorderBrush = theme.BorderBrush;
                if (ModeCompartmentBorder != null) ModeCompartmentBorder.BorderBrush = theme.BorderBrush;
                if (ToolsCompartmentBorder != null) ToolsCompartmentBorder.BorderBrush = theme.BorderBrush;

                if (TxtTitle != null) TxtTitle.Foreground = theme.TextPrimaryBrush;
                if (TxtContent != null) TxtContent.Foreground = theme.TextPrimaryBrush;
                if (TxtTitle != null) TxtTitle.CaretBrush = theme.GlowBrush;
                if (TxtContent != null) TxtContent.CaretBrush = theme.GlowBrush;
                if (TxtNewTask != null) TxtNewTask.CaretBrush = theme.GlowBrush;
                if (TxtNewCopyItem != null) TxtNewCopyItem.CaretBrush = theme.GlowBrush;
                if (IconPlus != null) IconPlus.Foreground = theme.GlowBrush;
            }
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

        if (BtnTextMode != null) BtnTextMode.ToolTip = (mode == NoteViewMode.Text) ? "Active: Plain Text Mode" : "Switch to Plain Text Mode";
        if (BtnChecklistMode != null) BtnChecklistMode.ToolTip = (mode == NoteViewMode.Checklist) ? "Active: Checklist Mode" : "Switch to Checklist Mode";
        if (BtnCopyMode != null) BtnCopyMode.ToolTip = (mode == NoteViewMode.CopyCompartments) ? "Active: Copy Compartments Mode" : "Switch to Copy Compartments Mode";
    }

    private void UpdatePinModeUI(NotePinMode mode)
    {
        _note.PinMode = mode;
        _desktopWindowManager.ApplyPinMode(mode);

        switch (mode)
        {
            case NotePinMode.DesktopStuck:
                if (TxtPinIndicator != null) TxtPinIndicator.Text = "📌 Stuck to Desktop";
                if (IconPin != null) IconPin.IconKey = "pin";
                if (IconPin != null) IconPin.Foreground = EmeraldBrush;
                if (BtnPinMode != null) BtnPinMode.ToolTip = "Mode: Stuck to Desktop (Behind all apps). Click to Float Always on Top";
                break;

            case NotePinMode.AlwaysOnTop:
                if (TxtPinIndicator != null) TxtPinIndicator.Text = "📌 Always on Top";
                if (IconPin != null) IconPin.IconKey = "pin";
                if (IconPin != null) IconPin.Foreground = AmberBrush;
                if (BtnPinMode != null) BtnPinMode.ToolTip = "Mode: Always on Top (Floating). Click to Stick to Desktop";
                break;

            case NotePinMode.Normal:
                if (TxtPinIndicator != null) TxtPinIndicator.Text = "📌 Normal Window";
                if (IconPin != null) IconPin.IconKey = "pin-off";
                if (IconPin != null) IconPin.Foreground = MutedBrush;
                if (BtnPinMode != null) BtnPinMode.ToolTip = "Mode: Normal Window. Click to Stick to Desktop";
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

        if (isLocked)
        {
            if (IconLock != null) IconLock.IconKey = "lock";
            if (IconLock != null) IconLock.Foreground = AmberBrush;
            if (LockBadge != null) LockBadge.Visibility = Visibility.Visible;
            if (BtnLock != null) BtnLock.ToolTip = "Note is Locked (Click to Unlock)";
        }
        else
        {
            if (IconLock != null) IconLock.IconKey = "unlock";
            if (IconLock != null) IconLock.Foreground = MutedBrush;
            if (LockBadge != null) LockBadge.Visibility = Visibility.Collapsed;
            if (BtnLock != null) BtnLock.ToolTip = "Lock note position & text";
        }
    }

    private void UpdateModifiedTime()
    {
        var elapsed = DateTime.Now - _note.ModifiedAt;
        if (elapsed.TotalMinutes < 1)
        {
            if (TxtModifiedTime != null) TxtModifiedTime.Text = "Just now";
        }
        else if (elapsed.TotalMinutes < 60)
        {
            if (TxtModifiedTime != null) TxtModifiedTime.Text = $"{(int)elapsed.TotalMinutes}m ago";
        }
        else if (elapsed.TotalHours < 24)
        {
            if (TxtModifiedTime != null) TxtModifiedTime.Text = $"{(int)elapsed.TotalHours}h ago";
        }
        else
        {
            if (TxtModifiedTime != null) TxtModifiedTime.Text = _note.ModifiedAt.ToString("MMM d");
        }
    }

    private void ShowTypingIndicator()
    {
        if (!_isLoaded) return;
        _savedStatusResetTimer?.Stop();

        if (TxtModifiedTime != null) TxtModifiedTime.Visibility = Visibility.Collapsed;
        if (TypingIndicatorContainer != null) TypingIndicatorContainer.Visibility = Visibility.Visible;
        if (IconTypingStatus != null) IconTypingStatus.IconKey = "pen-line";
        if (IconTypingStatus != null) IconTypingStatus.Foreground = _currentGlowBrush;
        if (TxtTypingStatus != null) TxtTypingStatus.Text = "Typing...";
        if (TxtTypingStatus != null) TxtTypingStatus.Foreground = _currentGlowBrush;
    }

    private void ShowSavedIndicator()
    {
        if (!_isLoaded) return;
        _savedStatusResetTimer?.Stop();

        if (TxtModifiedTime != null) TxtModifiedTime.Visibility = Visibility.Collapsed;
        if (TypingIndicatorContainer != null) TypingIndicatorContainer.Visibility = Visibility.Visible;
        if (IconTypingStatus != null) IconTypingStatus.IconKey = "check";
        if (IconTypingStatus != null) IconTypingStatus.Foreground = EmeraldBrush;
        if (TxtTypingStatus != null) TxtTypingStatus.Text = "Saved";
        if (TxtTypingStatus != null) TxtTypingStatus.Foreground = EmeraldBrush;

        _savedStatusResetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _savedStatusResetTimer.Tick += (s, e) =>
        {
            _savedStatusResetTimer.Stop();
            if (TypingIndicatorContainer != null) TypingIndicatorContainer.Visibility = Visibility.Collapsed;
            if (TxtModifiedTime != null) TxtModifiedTime.Visibility = Visibility.Visible;
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
        _note.Title = (TxtTitle != null ? TxtTitle.Text : _note.Title);
        _note.Content = (TxtContent != null ? TxtContent.Text : _note.Content);
        _note.Checklist = new List<TodoCheckItem>(_checklistItems);
        _note.CopyList = new List<CopySnippetItem>(_copyItems);
        _note.IsChecklistMode = (_note.ViewMode == NoteViewMode.Checklist);
        _note.X = Left;
        _note.Y = Top;
        _note.Width = Width;
        _note.Height = Height;
        _note.Opacity = _userConfiguredOpacity;
        _note.FontSize = (TxtContent != null ? TxtContent.FontSize : _note.FontSize);
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
        if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Visibility = string.IsNullOrEmpty(TxtTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
        RequestSave(isTyping: true);
    }

    private void TxtContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtContentPlaceholder != null) TxtContentPlaceholder.Visibility = string.IsNullOrEmpty(TxtContent.Text) ? Visibility.Visible : Visibility.Collapsed;
        RequestSave(isTyping: true);
    }

    private void TxtTitle_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtTitle.Text))
        {
            if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Visibility = Visibility.Visible;
        }
        SaveNoteState();
    }

    private void TxtContent_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        if (TxtContentPlaceholder != null) TxtContentPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtContent_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtContent.Text))
        {
            if (TxtContentPlaceholder != null) TxtContentPlaceholder.Visibility = Visibility.Visible;
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
        if (ColorPopup != null) ColorPopup.IsOpen = !ColorPopup.IsOpen;
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
                if (SliderActiveOpacity != null) SliderActiveOpacity.Value = Math.Round(_userConfiguredOpacity * 100);
            }
            if (TxtActiveOpacityPercent != null)
            {
                if (TxtActiveOpacityPercent != null) TxtActiveOpacityPercent.Text = $"{(int)Math.Round(_userConfiguredOpacity * 100)}%";
            }
            if (ChkUnfocusedDim != null)
            {
                if (ChkUnfocusedDim != null) ChkUnfocusedDim.IsChecked = _settingsService.Settings.EnableUnfocusedTransparency;
            }
            if (SliderUnfocusedOpacity != null)
            {
                if (SliderUnfocusedOpacity != null) SliderUnfocusedOpacity.Value = Math.Round(_settingsService.Settings.UnfocusedOpacity * 100);
                if (SliderUnfocusedOpacity != null) SliderUnfocusedOpacity.IsEnabled = _settingsService.Settings.EnableUnfocusedTransparency;
            }
            if (TxtUnfocusedOpacityPercent != null)
            {
                if (TxtUnfocusedOpacityPercent != null) TxtUnfocusedOpacityPercent.Text = $"{(int)Math.Round(_settingsService.Settings.UnfocusedOpacity * 100)}%";
            }
            if (UnfocusedOpacityControlsPanel != null)
            {
                if (UnfocusedOpacityControlsPanel != null) UnfocusedOpacityControlsPanel.Opacity = _settingsService.Settings.EnableUnfocusedTransparency ? 1.0 : 0.4;
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
            if (ColorPopup != null) ColorPopup.IsOpen = false;
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
            if (CustomColorPreview != null) CustomColorPreview.Background = new SolidColorBrush(color);
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
                if (CustomColorPreview != null) CustomColorPreview.Background = new SolidColorBrush(color);
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
        if (ColorPopup != null) ColorPopup.IsOpen = false;
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
        var fontMenu = new MenuItem { Header = $"Font Size ({TxtContent.FontSize:F0}pt)" };
        fontMenu.Items.Add(CreateMenuItem("Increase Font Size (+)", () => AdjustFontSize(2)));
        fontMenu.Items.Add(CreateMenuItem("Decrease Font Size (-)", () => AdjustFontSize(-2)));
        fontMenu.Items.Add(CreateMenuItem("Reset Font Size (16pt)", () => SetFontSize(16)));
        menu.Items.Add(fontMenu);

        // Opacity Submenu
        var opacityMenu = new MenuItem { Header = $"Opacity ({(int)(_userConfiguredOpacity * 100)}%)" };
        opacityMenu.Items.Add(CreateMenuItem("100% Solid", () => SetNoteOpacity(1.0)));
        opacityMenu.Items.Add(CreateMenuItem("90% Crisp", () => SetNoteOpacity(0.9)));
        opacityMenu.Items.Add(CreateMenuItem("80% Glass", () => SetNoteOpacity(0.8)));
        opacityMenu.Items.Add(CreateMenuItem("65% Translucent", () => SetNoteOpacity(0.65)));
        opacityMenu.Items.Add(CreateMenuItem("50% Stealth", () => SetNoteOpacity(0.5)));
        opacityMenu.Items.Add(new Separator());

        bool isDimEnabled = _settingsService.Settings.EnableUnfocusedTransparency;
        var toggleDimItem = new MenuItem
        {
            Header = isDimEnabled ? "✓ Dim When Unfocused (Enabled)" : "Dim When Unfocused (Disabled)"
        };
        toggleDimItem.Click += (s, e) =>
        {
            _settingsService.Settings.EnableUnfocusedTransparency = !isDimEnabled;
            _settingsService.Save();
            App.Instance?.NotifyTransparencySettingsChanged();
        };
        opacityMenu.Items.Add(toggleDimItem);
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

        // Settings & Preferences
        menu.Items.Add(CreateMenuItem("Settings & Preferences...", OpenSettings));

        // Check for Updates
        menu.Items.Add(CreateMenuItem("Check for Updates...", () =>
        {
            _ = UpdateService.CheckForUpdatesAsync(isManualCheck: true);
        }));

        menu.Items.Add(new Separator());

        // Delete Note
        menu.Items.Add(CreateMenuItem("Delete Note", DeleteNote));

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
        if (TxtContent != null) TxtContent.FontSize = size;
        if (TxtTitle != null) TxtTitle.FontSize = size + 1;
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
            if (TxtActiveOpacityPercent != null) TxtActiveOpacityPercent.Text = $"{(int)SliderActiveOpacity.Value}%";
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
            if (UnfocusedOpacityControlsPanel != null) UnfocusedOpacityControlsPanel.Opacity = isEnabled ? 1.0 : 0.4;
        }
        if (SliderUnfocusedOpacity != null)
        {
            if (SliderUnfocusedOpacity != null) SliderUnfocusedOpacity.IsEnabled = isEnabled;
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
            if (TxtUnfocusedOpacityPercent != null) TxtUnfocusedOpacityPercent.Text = $"{(int)SliderUnfocusedOpacity.Value}%";
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
            if (SliderActiveOpacity != null) SliderActiveOpacity.Value = Math.Round(_userConfiguredOpacity * 100);
            if (TxtActiveOpacityPercent != null)
            {
                if (TxtActiveOpacityPercent != null) TxtActiveOpacityPercent.Text = $"{(int)SliderActiveOpacity.Value}%";
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
        MemoryOptimizer.TrimMemory();
    }

    // Checklist Item Events
    private void NewTaskContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        if (TxtNewTask != null) TxtNewTask.Focus();
        e.Handled = true;
    }

    private void TxtNewTask_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        if (TxtNewTaskPlaceholder != null) TxtNewTaskPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtNewTask_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtNewTask.Text))
        {
            if (TxtNewTaskPlaceholder != null) TxtNewTaskPlaceholder.Visibility = Visibility.Visible;
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
        if (TxtNewTaskPlaceholder != null) TxtNewTaskPlaceholder.Visibility = string.IsNullOrEmpty(TxtNewTask.Text) ? Visibility.Visible : Visibility.Collapsed;
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
            if (TxtNewTask != null) TxtNewTask.Text = "";
            if (TxtNewTaskPlaceholder != null) TxtNewTaskPlaceholder.Visibility = Visibility.Visible;
            if (TxtNewTask != null) TxtNewTask.Focus();
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
        if (TxtNewCopyItem != null) TxtNewCopyItem.Focus();
        e.Handled = true;
    }

    private void TxtNewCopyItem_GotFocus(object sender, RoutedEventArgs e)
    {
        _desktopWindowManager.BringToFront();
        if (TxtNewCopyItemPlaceholder != null) TxtNewCopyItemPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtNewCopyItem_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtNewCopyItem.Text))
        {
            if (TxtNewCopyItemPlaceholder != null) TxtNewCopyItemPlaceholder.Visibility = Visibility.Visible;
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
        if (TxtNewCopyItemPlaceholder != null) TxtNewCopyItemPlaceholder.Visibility = string.IsNullOrEmpty(TxtNewCopyItem.Text) ? Visibility.Visible : Visibility.Collapsed;
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
            if (TxtNewCopyItem != null) TxtNewCopyItem.Text = "";
            if (TxtNewCopyItemPlaceholder != null) TxtNewCopyItemPlaceholder.Visibility = Visibility.Visible;
            if (TxtNewCopyItem != null) TxtNewCopyItem.Focus();
            RequestSave(isTyping: false);
        }
    }
}
