using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;

namespace SmartNotes.UI.Windows;

public partial class SettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private readonly NoteStorageService? _storageService;
    private readonly Action _onSettingsSaved;

    public SettingsDialog(SettingsService settingsService, NoteStorageService? storageService, Action onSettingsSaved)
    {
        _settingsService = settingsService;
        _storageService = storageService;
        _onSettingsSaved = onSettingsSaved;

        InitializeComponent();

        Loaded += OnWindowLoaded;
        // Picking a language previews it right away (every open note follows). Closing without saving must undo that.
        string languageBefore = LocalizationService.Instance.CurrentLanguage;
        Closed += (s, e) =>
        {
            if (!_saved) LocalizationService.Instance.SetLanguage(languageBefore);
        };
    }

    private bool _saved;

    public SettingsDialog(SettingsService settingsService, Action onSettingsSaved)
        : this(settingsService, null, onSettingsSaved)
    {
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        WindowBlurHelper.ApplyModernWindowStyles(this);
        LoadValues();
        ApplyLocalization();
        RefreshTrashStatus();
        RefreshRamStatus();
        UpdateLivePreview();
    }

    private bool _suppressEvents = false;

    private void LoadValues()
    {
        var s = _settingsService.Settings;
        ChkStartup.IsChecked = s.LaunchOnStartup;
        ChkDesktopStuck.IsChecked = s.DefaultPinMode == NotePinMode.DesktopStuck;
        ChkLivingThemes.IsChecked = s.LivingThemes;
        _pendingSyncFolder = s.SyncFolder ?? "";
        UpdateSyncCard();
        ChkUnfocusedTransparency.IsChecked = s.EnableUnfocusedTransparency;
        ChkHotkeys.IsChecked = s.EnableGlobalHotkeys;
        ChkAutoTrimMemory.IsChecked = s.AutoOptimizeMemory;

        _suppressEvents = true;
        try
        {
            SliderUnfocusedOpacity.Value = Math.Round(s.UnfocusedOpacity * 100);
            TxtUnfocusedOpacityPercent.Text = $"{(int)SliderUnfocusedOpacity.Value}%";
            PnlUnfocusedOpacitySlider.Opacity = s.EnableUnfocusedTransparency ? 1.0 : 0.4;
            SliderUnfocusedOpacity.IsEnabled = s.EnableUnfocusedTransparency;

            // Populate UI Languages
            CmbAppLanguage.ItemsSource = LocalizationService.SupportedLanguages.Select(l => l.DisplayName).ToList();
            var currentAppLang = LocalizationService.SupportedLanguages.FirstOrDefault(l => l.Code.Equals(s.AppLanguage, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(currentAppLang.DisplayName)) currentAppLang = LocalizationService.SupportedLanguages[0];
            CmbAppLanguage.SelectedItem = currentAppLang.DisplayName;

            // Populate Proofing Languages
            CmbProofingLanguage.ItemsSource = TextProofingService.SupportedLanguages.Select(l => l.DisplayName).ToList();
            var currentLang = TextProofingService.SupportedLanguages.FirstOrDefault(l => l.Code.Equals(s.ProofingLanguage, StringComparison.OrdinalIgnoreCase))
                ?? TextProofingService.SupportedLanguages[0];
            CmbProofingLanguage.SelectedItem = currentLang.DisplayName;
            UpdateProofingLanguageStatus();

            // Populate Proofing Toggles
            ChkSpellCheck.IsChecked = s.EnableSpellCheck;
            ChkAutocorrect.IsChecked = s.EnableAutocorrect;
            ChkAutoCapitalize.IsChecked = s.AutoCapitalizeSentences;
            ChkSmartSymbols.IsChecked = s.SmartSymbolReplacements;

            // Populate Colors
            CmbDefaultColor.ItemsSource = new List<string> { "Amber", "Emerald", "Violet", "Cyan", "Rose", "Obsidian", "Gold", "Mint" };
            CmbDefaultColor.SelectedItem = s.DefaultColorKey;
            PopulateSkinCombo(s.DefaultSkinKey);
            BuildSkinFontRows(s);

            // Populate Font Sizes ("Default" used to sit on 14 pt although new notes start at 16 pt)
            CmbDefaultFontSize.ItemsSource = FontSizeChoices.Select(size => LocalizationService.T($"FontSize_{size}")).ToList();
            CmbDefaultFontSize.SelectedIndex = NearestFontSizeIndex(s.DefaultFontSize);
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private static readonly int[] FontSizeChoices = { 12, 14, 16, 18, 20 };

    /// <summary>The closest offered size, so opening and saving Settings never changes an in-between value much.</summary>
    private static int NearestFontSizeIndex(double size)
    {
        int best = 2; // 16 pt, the default
        for (int i = 0; i < FontSizeChoices.Length; i++)
        {
            if (Math.Abs(FontSizeChoices[i] - size) < Math.Abs(FontSizeChoices[best] - size)) best = i;
        }
        return best;
    }

    private double SelectedFontSize => FontSizeChoices[Math.Clamp(CmbDefaultFontSize.SelectedIndex, 0, FontSizeChoices.Length - 1)];

    private void ApplyLocalization()
    {
        // Window Title & Headers
        Title = LocalizationService.T("Settings_Title");
        TxtSettingsHeaderTitle.Text = LocalizationService.T("Settings_Title");
        TxtSettingsHeaderBadge.Text = LocalizationService.T("App_VersionBadge", UpdateService.CurrentVersion.ToString(3));
        TxtSettingsHeaderSubtitle.Text = LocalizationService.T("Settings_Subtitle");
        TxtSidebarPreferences.Text = LocalizationService.T("Settings_PreferencesNav");
        TxtEngineTitle.Text = LocalizationService.T("Settings_Engine");
        TxtEngineInfo.Text = LocalizationService.T("Settings_EngineInfo");

        // Sidebar Navigation
        TxtNavGeneral.Text = LocalizationService.T("Nav_General");
        TxtNavAppearance.Text = LocalizationService.T("Nav_Appearance");
        TxtNavHotkeys.Text = LocalizationService.T("Nav_Hotkeys");
        TxtNavProofing.Text = LocalizationService.T("Nav_Proofing");
        TxtNavTrash.Text = LocalizationService.T("Nav_Trash");
        TxtNavPerformance.Text = LocalizationService.T("Nav_Performance");

        // Section 1: Desktop & System
        TxtSection1Title.Text = LocalizationService.T("General_SectionTitle");
        TxtSection1Subtitle.Text = LocalizationService.T("General_SectionDesc");
        TxtAppLangTitle.Text = LocalizationService.T("General_AppLangTitle");
        TxtAppLangDesc.Text = LocalizationService.T("General_AppLangDesc");
        TxtStartupTitle.Text = LocalizationService.T("General_StartupTitle");
        TxtStartupDesc.Text = LocalizationService.T("General_StartupDesc");
        TxtDesktopStuckTitle.Text = LocalizationService.T("General_DesktopStuckTitle");
        TxtDesktopStuckDesc.Text = LocalizationService.T("General_DesktopStuckDesc");
        TxtSyncTitle.Text = LocalizationService.T("Sync_Title");
        TxtSyncDesc.Text = LocalizationService.T("Sync_Desc");
        TxtChooseSyncFolder.Text = LocalizationService.T("Sync_Choose");
        TxtSyncOff.Text = LocalizationService.T("Sync_TurnOff");
        UpdateSyncCard();
        TxtLivingThemesTitle.Text = LocalizationService.T("Appearance_LivingTitle");
        TxtLivingThemesDesc.Text = LocalizationService.T("Appearance_LivingDesc");
        TxtTransparencyTitle.Text = LocalizationService.T("General_TransparencyTitle");
        TxtTransparencyDesc.Text = LocalizationService.T("General_TransparencyDesc");
        TxtUnfocusedOpacityTitle.Text = LocalizationService.T("General_UnfocusedOpacityTitle");
        TxtUnfocusedOpacityDesc.Text = LocalizationService.T("General_UnfocusedOpacityDesc");

        // Section 2: Note Appearance
        TxtSection2Title.Text = LocalizationService.T("Appearance_SectionTitle");
        TxtSection2Subtitle.Text = LocalizationService.T("Appearance_SectionDesc");
        TxtDefaultColorTitle.Text = LocalizationService.T("Appearance_DefaultColorTitle");
        TxtDefaultColorDesc.Text = LocalizationService.T("Appearance_DefaultColorDesc");
        TxtDefaultSkinTitle.Text = LocalizationService.T("Appearance_DefaultSkinTitle");
        TxtDefaultSkinDesc.Text = LocalizationService.T("Appearance_DefaultSkinDesc");
        TxtSkinFontsTitle.Text = LocalizationService.T("Appearance_SkinFontsTitle");
        TxtSkinFontsDesc.Text = LocalizationService.T("Appearance_SkinFontsDesc");
        if (CmbDefaultSkin.Items.Count > 0)
        {
            bool wasSuppressed = _suppressEvents;
            _suppressEvents = true;
            PopulateSkinCombo(CmbDefaultSkin.SelectedValue as string);
            _suppressEvents = wasSuppressed;
        }
        TxtQuickSwatchesTitle.Text = LocalizationService.T("Appearance_QuickSwatches");
        TxtDefaultFontSizeTitle.Text = LocalizationService.T("Appearance_DefaultFontSizeTitle");
        if (CmbDefaultFontSize.Items.Count > 0)
        {
            bool wasSuppressed = _suppressEvents;
            _suppressEvents = true;
            int selected = CmbDefaultFontSize.SelectedIndex;
            CmbDefaultFontSize.ItemsSource = FontSizeChoices.Select(size => LocalizationService.T($"FontSize_{size}")).ToList();
            CmbDefaultFontSize.SelectedIndex = selected;
            _suppressEvents = wasSuppressed;
        }
        TxtDefaultFontSizeDesc.Text = LocalizationService.T("Appearance_DefaultFontSizeDesc");
        TxtLivePreviewTitle.Text = LocalizationService.T("Appearance_LivePreview");
        PreviewNoteTitle.Text = LocalizationService.T("Appearance_PreviewTitle");
        TxtPreviewPinned.Text = LocalizationService.T("Appearance_PreviewPinned");
        PreviewNoteBody.Text = LocalizationService.T("Appearance_PreviewBody");

        // Section 3: Global Hotkeys
        TxtSection3Title.Text = LocalizationService.T("Hotkeys_SectionTitle");
        TxtSection3Subtitle.Text = LocalizationService.T("Hotkeys_SectionDesc");
        TxtHotkeysToggleTitle.Text = LocalizationService.T("Hotkeys_ToggleTitle");
        TxtHotkeysToggleDesc.Text = LocalizationService.T("Hotkeys_ToggleDesc");
        TxtActiveShortcutsTitle.Text = LocalizationService.T("Hotkeys_ActiveShortcuts");
        TxtShortcut1Title.Text = LocalizationService.T("Hotkeys_NewNote");
        TxtShortcut1Desc.Text = LocalizationService.T("Hotkeys_NewNoteDesc");
        TxtShortcutFrontTitle.Text = LocalizationService.T("Hotkeys_BringFront");
        TxtShortcutFrontDesc.Text = LocalizationService.T("Hotkeys_BringFrontDesc");
        TxtShortcut3Title.Text = LocalizationService.T("Hotkeys_ToggleNotes");
        TxtShortcut3Desc.Text = LocalizationService.T("Hotkeys_ToggleNotesDesc");

        // Section 4: Text Proofing
        TxtSection4Title.Text = LocalizationService.T("Proofing_SectionTitle");
        TxtSection4Subtitle.Text = LocalizationService.T("Proofing_SectionDesc");
        TxtSpellCheckTitle.Text = LocalizationService.T("Proofing_SpellCheckTitle");
        TxtSpellCheckDesc.Text = LocalizationService.T("Proofing_SpellCheckDesc");
        TxtProofingDictTitle.Text = LocalizationService.T("Proofing_DictTitle");
        TxtProofingDictDesc.Text = LocalizationService.T("Proofing_DictDesc");
        TxtWindowsDictNotice.Text = LocalizationService.T("Proofing_WindowsDictNotice");
        TxtAutocorrectTitle.Text = LocalizationService.T("Proofing_AutocorrectTitle");
        TxtAutocorrectDesc.Text = LocalizationService.T("Proofing_AutocorrectDesc");
        TxtAutoCapTitle.Text = LocalizationService.T("Proofing_AutoCapTitle");
        TxtAutoCapDesc.Text = LocalizationService.T("Proofing_AutoCapDesc");
        TxtSmartSymbolsTitle.Text = LocalizationService.T("Proofing_SmartSymbolsTitle");
        TxtSmartSymbolsDesc.Text = LocalizationService.T("Proofing_SmartSymbolsDesc");
        TxtCustomRulesBtnTitle.Text = LocalizationService.T("Proofing_CustomRulesBtn");
        TxtCustomRulesBtnDesc.Text = LocalizationService.T("Proofing_CustomRulesDesc");

        // Section 5: Trash & Recovery
        TxtSection5Title.Text = LocalizationService.T("Trash_SectionTitle");
        TxtSection5Subtitle.Text = LocalizationService.T("Trash_SectionDesc");
        TxtRetentionTitle.Text = LocalizationService.T("Trash_RetentionTitle");
        TxtRetentionDesc.Text = LocalizationService.T("Trash_RetentionDesc");
        TxtTrashActionsTitle.Text = LocalizationService.T("Trash_ActionsTitle");
        TxtOpenTrashBtn.Text = LocalizationService.T("Trash_OpenExplorerBtn");
        TxtEmptyTrashBtn.Text = LocalizationService.T("Trash_EmptyBtn");

        // Section 6: Memory & Performance
        TxtSection6Title.Text = LocalizationService.T("Perf_SectionTitle");
        TxtSection6Subtitle.Text = LocalizationService.T("Perf_SectionDesc");
        TxtAutoTrimTitle.Text = LocalizationService.T("Perf_AutoTrimTitle");
        TxtAutoTrimDesc.Text = LocalizationService.T("Perf_AutoTrimDesc");
        TxtWorkingSetTitle.Text = LocalizationService.T("Perf_WorkingSetTitle");
        TxtWorkingSetDesc.Text = LocalizationService.T("Perf_WorkingSetDesc");
        TxtTrimRamBtn.Text = LocalizationService.T("Perf_TrimNowBtn");
        TxtHardwareAccelTitle.Text = LocalizationService.T("Perf_HardwareAccelTitle");
        TxtHardwareAccelDesc.Text = LocalizationService.T("Perf_HardwareAccelDesc");
        TxtGcTitle.Text = LocalizationService.T("Perf_GcTitle");
        TxtGcDesc.Text = LocalizationService.T("Perf_GcDesc");
        TxtZeroRamTitle.Text = LocalizationService.T("Perf_ZeroRamTitle");
        TxtZeroRamDesc.Text = LocalizationService.T("Perf_ZeroRamDesc");

        // Footer
        TxtFooterStatus.Text = LocalizationService.T("Status_SaveInstant");
        TxtBtnCancel.Text = LocalizationService.T("Btn_Cancel");
        TxtBtnSave.Text = LocalizationService.T("Btn_Save");

        UpdateProofingLanguageStatus();
        RefreshTrashStatus();
        RefreshRamStatus();
    }

    private void CmbAppLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        if (CmbAppLanguage.SelectedItem is string selectedAppLangName)
        {
            var matched = LocalizationService.SupportedLanguages.FirstOrDefault(l => l.DisplayName == selectedAppLangName);
            if (!string.IsNullOrEmpty(matched.Code))
            {
                LocalizationService.Instance.SetLanguage(matched.Code);
                ApplyLocalization();
            }
        }
    }

    private void CmbProofingLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateProofingLanguageStatus();
    }

    private void UpdateProofingLanguageStatus()
    {
        if (TxtLanguageStatus == null || CmbProofingLanguage == null) return;

        string? selectedName = CmbProofingLanguage.SelectedItem as string;
        var matched = TextProofingService.SupportedLanguages.FirstOrDefault(l => l.DisplayName == selectedName)
            ?? TextProofingService.SupportedLanguages[0];

        bool isSupported = TextProofingService.IsLanguageSupported(matched.Code);
        if (isSupported)
        {
            TxtLanguageStatus.Text = LocalizationService.T("Proofing_StatusSupported", matched.DisplayName);
            TxtLanguageStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        }
        else
        {
            TxtLanguageStatus.Text = LocalizationService.T("Proofing_StatusNotSupported", matched.DisplayName);
            TxtLanguageStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
        }
    }

    private void NavRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        if (PanelGeneral == null || PanelAppearance == null || PanelHotkeys == null ||
            PanelProofing == null || PanelTrash == null || PanelPerformance == null) return;

        PanelGeneral.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
        PanelAppearance.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        PanelHotkeys.Visibility = tag == "Hotkeys" ? Visibility.Visible : Visibility.Collapsed;
        PanelProofing.Visibility = tag == "Proofing" ? Visibility.Visible : Visibility.Collapsed;
        PanelTrash.Visibility = tag == "Trash" ? Visibility.Visible : Visibility.Collapsed;
        PanelPerformance.Visibility = tag == "Performance" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "Appearance")
        {
            UpdateLivePreview();
        }
    }

    private void ChkUnfocusedTransparency_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = ChkUnfocusedTransparency.IsChecked == true;
        PnlUnfocusedOpacitySlider.Opacity = isChecked ? 1.0 : 0.4;
        SliderUnfocusedOpacity.IsEnabled = isChecked;
    }

    private void SliderUnfocusedOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents) return;
        if (TxtUnfocusedOpacityPercent != null)
        {
            TxtUnfocusedOpacityPercent.Text = $"{(int)SliderUnfocusedOpacity.Value}%";
        }
    }

    private readonly Dictionary<string, (ComboBox Title, ComboBox Text)> _skinFontCombos = new();

    /// <summary>One row per skin that offers a choice of fonts: skin name, title font, text font.</summary>
    private void BuildSkinFontRows(AppSettings s)
    {
        SkinFontRowsPanel.Children.Clear();
        _skinFontCombos.Clear();

        var header = NewFontRowGrid();
        header.Children.Add(NewFontRowLabel(LocalizationService.T("Appearance_FontTitleColumn"), 1, true));
        header.Children.Add(NewFontRowLabel(LocalizationService.T("Appearance_FontTextColumn"), 2, true));
        SkinFontRowsPanel.Children.Add(header);

        foreach (var skin in NoteSkin.All)
        {
            if (skin.TitleFonts.Count == 0 && skin.TextFonts.Count == 0) continue;

            var row = NewFontRowGrid();
            row.Children.Add(NewFontRowLabel(LocalizationService.T(skin.LocalizationKey), 0, false));
            var title = NewFontCombo(skin.TitleFonts, skin.ResolveTitleFont(s), 1);
            var text = NewFontCombo(skin.TextFonts, skin.ResolveTextFont(s), 2);
            row.Children.Add(title);
            row.Children.Add(text);
            SkinFontRowsPanel.Children.Add(row);
            _skinFontCombos[skin.Key] = (title, text);
        }
    }

    private static Grid NewFontRowGrid()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        return grid;
    }

    private static TextBlock NewFontRowLabel(string text, int column, bool isHeader)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = isHeader ? 11.5 : 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = NoteColorTheme.CreateFrozenBrush(isHeader ? "#7888A4" : "#E2E8F0"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0)
        };
        Grid.SetColumn(label, column);
        return label;
    }

    private ComboBox NewFontCombo(IReadOnlyList<SkinFontOption> options, SkinFontOption? selected, int column)
    {
        var combo = new ComboBox { Height = 36, Margin = new Thickness(8, 0, 0, 0) };
        foreach (var option in options)
        {
            var item = new ComboBoxItem { Content = option.DisplayName, Tag = option.Key, FontFamily = option.Family, FontSize = 15 };
            combo.Items.Add(item);
            if (selected != null && option.Key == selected.Key) combo.SelectedItem = item;
        }
        combo.SelectionChanged += (sender, e) =>
        {
            if (!_suppressEvents) UpdateLivePreview();
        };
        Grid.SetColumn(combo, column);
        return combo;
    }

    /// <summary>The option currently selected in a font combo (falls back to the skin default).</summary>
    private static SkinFontOption? SelectedFont(ComboBox combo, IReadOnlyList<SkinFontOption> options)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is string key)
        {
            foreach (var option in options)
            {
                if (option.Key == key) return option;
            }
        }
        return options.Count > 0 ? options[0] : null;
    }

    private void PopulateSkinCombo(string? selectedKey)
    {
        var items = new List<KeyValuePair<string, string>>();
        foreach (var skin in NoteSkin.All)
        {
            items.Add(new KeyValuePair<string, string>(skin.Key, LocalizationService.T(skin.LocalizationKey)));
        }
        CmbDefaultSkin.ItemsSource = items;
        CmbDefaultSkin.SelectedValue = NoteSkin.Get(selectedKey).Key;
    }

    private void ThemeSwatchBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string colorKey)
        {
            CmbDefaultColor.SelectedItem = colorKey;
            UpdateLivePreview();
        }
    }

    private void CmbDefaultColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        UpdateLivePreview();
    }

    private void CmbDefaultFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        UpdateLivePreview();
    }

    private void UpdateLivePreview()
    {
        if (PreviewNoteCard == null || PreviewNoteHeader == null || PreviewNoteTitle == null || PreviewNoteBody == null)
            return;

        string? colorKey = CmbDefaultColor.SelectedItem as string;
        var skin = NoteSkin.Get(CmbDefaultSkin.SelectedValue as string);
        var theme = skin.Tint(NoteColorTheme.Get(colorKey));

        PreviewNoteCard.CornerRadius = new CornerRadius(Math.Min(12, skin.CardRadius));
        PreviewNoteCard.BorderThickness = new Thickness(Math.Min(3.5, skin.CardBorder));
        PreviewNoteHeader.CornerRadius = new CornerRadius(Math.Min(9, skin.HeaderRadius), Math.Min(9, skin.HeaderRadius), 0, 0);
        SkinFontOption? previewTitleFont = null, previewTextFont = null;
        if (_skinFontCombos.TryGetValue(skin.Key, out var fontCombos))
        {
            previewTitleFont = SelectedFont(fontCombos.Title, skin.TitleFonts);
            previewTextFont = SelectedFont(fontCombos.Text, skin.TextFonts);
        }
        PreviewNoteTitle.FontFamily = previewTitleFont?.Family ?? skin.TitleFont;
        PreviewNoteTitle.FontWeight = previewTitleFont?.Weight ?? skin.TitleWeight;
        PreviewNoteTitle.FontSize = 13.5 * (previewTitleFont?.Scale ?? skin.TitleScale);
        PreviewNoteBody.FontWeight = previewTextFont?.Weight ?? skin.BodyWeight;
        PreviewNoteBody.FontFamily = previewTextFont?.Family ?? skin.Font;

        PreviewNoteCard.Background = theme.BgBrush;
        PreviewNoteCard.BorderBrush = theme.BorderBrush;
        PreviewNoteHeader.Background = theme.HeaderBgBrush;
        PreviewNoteHeader.BorderBrush = theme.BorderBrush;

        // Shaped skins: no card; their own painter draws the body behind the mini note, and the header row sits in
        // the painted header strip (same insets as on a real note)
        _previewSkin = skin;
        _previewTheme = theme;
        if (skin.CustomArt)
        {
            PreviewNoteCard.Background = Brushes.Transparent;
            PreviewNoteCard.BorderThickness = new Thickness(0);
            PreviewNoteHeader.Background = Brushes.Transparent;
            PreviewNoteHeader.BorderThickness = new Thickness(0);
            PreviewNoteHeader.Height = 44;
            var inset = skin.ContentInset;
            PreviewNoteStack.Margin = new Thickness(inset.Left, inset.Top + skin.HeaderTopPad, inset.Right, inset.Bottom + 8);
            PreviewNoteHost.MinHeight = 170;
        }
        else
        {
            PreviewNoteHeader.ClearValue(HeightProperty);
            PreviewNoteHeader.BorderThickness = new Thickness(0, 0, 0, 2);
            PreviewNoteStack.Margin = new Thickness(0);
            PreviewNoteHost.MinHeight = 0;
        }
        DrawPreviewArt();
        PreviewNoteTitle.Foreground = skin.AccentTitle ? theme.GlowBrush : theme.TextPrimaryBrush;
        PreviewNoteBody.Foreground = theme.TextPrimaryBrush;
        if (TxtPreviewPinned != null) TxtPreviewPinned.Foreground = theme.TextMutedBrush; // readable on parchment and paper too
        if (PreviewNoteIcon != null)
        {
            PreviewNoteIcon.Foreground = theme.PrimaryBrush;
        }

        PreviewNoteBody.FontSize = SelectedFontSize * (previewTextFont?.Scale ?? 1.0);
    }

    // ---------------------------------------------------------------- sync folder

    /// <summary>The folder chosen in this dialog; applied with Save, like every other setting here.</summary>
    private string _pendingSyncFolder = "";

    private void BtnChooseSyncFolder_Click(object sender, RoutedEventArgs e)
    {
        using var picker = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = LocalizationService.T("Sync_PickerTitle"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = _pendingSyncFolder.Length > 0 ? _pendingSyncFolder : DefaultSyncStart(),
        };
        if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedPath)) return;
        string folder = picker.SelectedPath;
        // the app's own data folder would sync with itself
        if (folder.StartsWith(Core.AppPaths.DataFolder, StringComparison.OrdinalIgnoreCase))
        {
            ModernMessageBox.Show(LocalizationService.T("Sync_NotDataFolder"), "SmartNotes", ModernMessageButtons.OK, ModernMessageIcon.Warning, owner: this);
            return;
        }
        _pendingSyncFolder = folder;
        UpdateSyncCard();
    }

    private void BtnSyncOff_Click(object sender, RoutedEventArgs e)
    {
        _pendingSyncFolder = "";
        UpdateSyncCard();
    }

    /// <summary>OneDrive if it is set up, else Documents.</summary>
    private static string DefaultSyncStart()
    {
        string? oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        return !string.IsNullOrWhiteSpace(oneDrive) && System.IO.Directory.Exists(oneDrive)
            ? oneDrive
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void UpdateSyncCard()
    {
        if (TxtSyncPath == null) return;
        bool on = _pendingSyncFolder.Length > 0;
        TxtSyncPath.Text = on ? System.IO.Path.Combine(_pendingSyncFolder, Core.Services.SyncService.SubfolderName) : LocalizationService.T("Sync_Off");
        TxtSyncPath.ToolTip = on ? TxtSyncPath.Text : null;
        BtnSyncOff.IsEnabled = on;

        var sync = App.Instance?.Sync;
        string status;
        if (!on) status = LocalizationService.T("Sync_OffDesc");
        else if (!string.Equals(_pendingSyncFolder, _settingsService.Settings.SyncFolder, StringComparison.OrdinalIgnoreCase)) status = LocalizationService.T("Sync_AfterSave");
        else if (sync?.LastError is { } error) status = LocalizationService.T("Sync_Problem", error);
        else if (sync?.LastSync is { } last) status = LocalizationService.T("Sync_Status", last.ToString("t"), sync.OtherDevices);
        else status = "";
        TxtSyncStatus.Text = status;
    }

    private NoteSkin? _previewSkin;
    private NoteColorTheme? _previewTheme;

    private void PreviewNoteHost_SizeChanged(object sender, SizeChangedEventArgs e) => DrawPreviewArt();

    private void DrawPreviewArt()
    {
        if (PreviewArtHost == null || PreviewNoteHost == null) return;
        if (_previewSkin == null || _previewTheme == null || !_previewSkin.CustomArt)
        {
            PreviewArtHost.Drawing = null;
            return;
        }
        PreviewArtHost.Drawing = SmartNotes.UI.Skins.SkinArt.Build(_previewSkin, PreviewNoteHost.ActualWidth, PreviewNoteHost.ActualHeight,
            _previewTheme, "PREV", System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip, bottomReserve: 34);
    }

    private void RefreshTrashStatus()
    {
        int count = _storageService?.GetDeletedNotes().Count ?? 0;
        TxtTrashCount.Text = LocalizationService.T("Trash_CountFormat", count, LocalizationService.PluralSuffix(count));
    }

    private void RefreshRamStatus()
    {
        double mb = MemoryOptimizer.GetCurrentMemoryUsageMb();
        TxtRamStatus.Text = LocalizationService.T("Perf_HealthyStatus", mb);
        TxtRamStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
    }

    private void BtnOpenTrash_Click(object sender, RoutedEventArgs e)
    {
        _storageService?.OpenTrashFolderInExplorer();
    }

    private void BtnEmptyTrash_Click(object sender, RoutedEventArgs e)
    {
        if (_storageService != null)
        {
            _storageService.ClearTrash();
            RefreshTrashStatus();
            ModernMessageBox.Show(LocalizationService.T("Trash_EmptiedToast"), LocalizationService.T("Trash_SectionTitle"), ModernMessageButtons.OK, ModernMessageIcon.Success, LocalizationService.T("Trash_ActionsTitle"));
        }
    }

    private void BtnOptimizeRam_Click(object sender, RoutedEventArgs e)
    {
        var (before, after, freed) = MemoryOptimizer.TrimMemory();
        TxtRamStatus.Text = LocalizationService.T("Perf_TrimmedStatus", after, freed);
        TxtRamStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        if (BtnOptimizeRam.Content is StackPanel sp && sp.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock tb)
        {
            tb.Text = LocalizationService.T("Perf_TrimmedBtn");
        }
    }

    private void BtnOpenCustomRules_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!System.IO.File.Exists(TextProofingService.CustomRulesPath))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TextProofingService.CustomRulesPath)!);
                var starterRules = new List<AutocorrectRule>
                {
                    new("brb", "be right back"),
                    new("omw", "on my way"),
                    new("addr", "123 Main St")
                };
                string json = System.Text.Json.JsonSerializer.Serialize(starterRules, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(TextProofingService.CustomRulesPath, json);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", TextProofingService.CustomRulesPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ModernMessageBox.Show(LocalizationService.T("Proofing_OpenFileFailed", System.IO.Path.GetFileName(TextProofingService.CustomRulesPath), ex.Message),
                "SmartNotes", ModernMessageButtons.OK, ModernMessageIcon.Warning, LocalizationService.T("Proofing_CustomRulesBtn"), this);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Settings;
        s.LaunchOnStartup = ChkStartup.IsChecked == true;
        // On: new notes start stuck to the desktop (behind your apps). Off: they start on top of other windows.
        s.DefaultPinMode = ChkDesktopStuck.IsChecked == true ? NotePinMode.DesktopStuck : NotePinMode.AlwaysOnTop;
        s.LivingThemes = ChkLivingThemes.IsChecked == true;
        s.SyncFolder = _pendingSyncFolder;
        s.EnableUnfocusedTransparency = ChkUnfocusedTransparency.IsChecked == true;
        s.UnfocusedOpacity = Math.Clamp(SliderUnfocusedOpacity.Value / 100.0, 0.15, 0.90);
        s.EnableGlobalHotkeys = ChkHotkeys.IsChecked == true;
        s.AutoOptimizeMemory = ChkAutoTrimMemory.IsChecked == true;
        MemoryOptimizer.AutoTrimEnabled = s.AutoOptimizeMemory;

        if (CmbAppLanguage.SelectedItem is string selectedAppLangName)
        {
            var matchedAppLang = LocalizationService.SupportedLanguages.FirstOrDefault(l => l.DisplayName == selectedAppLangName);
            if (!string.IsNullOrEmpty(matchedAppLang.Code))
            {
                s.AppLanguage = matchedAppLang.Code;
                LocalizationService.Instance.SetLanguage(s.AppLanguage);
            }
        }

        // Text Proofing Settings
        s.EnableSpellCheck = ChkSpellCheck.IsChecked == true;
        s.EnableAutocorrect = ChkAutocorrect.IsChecked == true;
        s.AutoCapitalizeSentences = ChkAutoCapitalize.IsChecked == true;
        s.SmartSymbolReplacements = ChkSmartSymbols.IsChecked == true;

        if (CmbProofingLanguage.SelectedItem is string selectedDisplayName)
        {
            var matchedLang = TextProofingService.SupportedLanguages.FirstOrDefault(l => l.DisplayName == selectedDisplayName);
            if (matchedLang != null)
            {
                s.ProofingLanguage = matchedLang.Code;
            }
        }

        if (CmbDefaultColor.SelectedItem is string color)
        {
            s.DefaultColorKey = color;
        }

        if (CmbDefaultSkin.SelectedValue is string skinKey)
        {
            s.DefaultSkinKey = skinKey;
        }

        foreach (var pair in _skinFontCombos)
        {
            if (pair.Value.Title.SelectedItem is ComboBoxItem titleItem && titleItem.Tag is string titleKey) s.SkinTitleFonts[pair.Key] = titleKey;
            if (pair.Value.Text.SelectedItem is ComboBoxItem textItem && textItem.Tag is string textKey) s.SkinTextFonts[pair.Key] = textKey;
        }

        s.DefaultFontSize = SelectedFontSize;

        _settingsService.Save();
        _saved = true;
        _onSettingsSaved();
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
