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
    }

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
        ChkDesktopStuck.IsChecked = s.KeepBehindAllWindows;
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

            // Populate Font Sizes
            CmbDefaultFontSize.ItemsSource = new List<string> { "12 pt (Compact)", "14 pt (Default)", "16 pt (Large)", "18 pt (Extra Large)", "20 pt (Headline)" };
            int idx = s.DefaultFontSize switch
            {
                12.0 => 0,
                16.0 => 2,
                18.0 => 3,
                20.0 => 4,
                _ => 1
            };
            CmbDefaultFontSize.SelectedIndex = idx;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void ApplyLocalization()
    {
        // Window Title & Headers
        Title = LocalizationService.T("Settings_Title");
        TxtSettingsHeaderTitle.Text = LocalizationService.T("Settings_Title");
        TxtSettingsHeaderBadge.Text = LocalizationService.T("App_VersionBadge");
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
        TxtTransparencyTitle.Text = LocalizationService.T("General_TransparencyTitle");
        TxtTransparencyDesc.Text = LocalizationService.T("General_TransparencyDesc");
        TxtUnfocusedOpacityTitle.Text = LocalizationService.T("General_UnfocusedOpacityTitle");
        TxtUnfocusedOpacityDesc.Text = LocalizationService.T("General_UnfocusedOpacityDesc");

        // Section 2: Note Appearance
        TxtSection2Title.Text = LocalizationService.T("Appearance_SectionTitle");
        TxtSection2Subtitle.Text = LocalizationService.T("Appearance_SectionDesc");
        TxtDefaultColorTitle.Text = LocalizationService.T("Appearance_DefaultColorTitle");
        TxtDefaultColorDesc.Text = LocalizationService.T("Appearance_DefaultColorDesc");
        TxtQuickSwatchesTitle.Text = LocalizationService.T("Appearance_QuickSwatches");
        TxtDefaultFontSizeTitle.Text = LocalizationService.T("Appearance_DefaultFontSizeTitle");
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
        TxtShortcut2Title.Text = LocalizationService.T("Hotkeys_NotesHub");
        TxtShortcut2Desc.Text = LocalizationService.T("Hotkeys_NotesHubDesc");
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
        TxtCustomDictBtnTitle.Text = LocalizationService.T("Proofing_CustomDictBtn");
        TxtCustomDictBtnDesc.Text = LocalizationService.T("Proofing_CustomDictDesc");
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
        var theme = NoteColorTheme.Get(colorKey);

        PreviewNoteCard.Background = theme.BgBrush;
        PreviewNoteCard.BorderBrush = theme.BorderBrush;
        PreviewNoteHeader.Background = theme.HeaderBgBrush;
        PreviewNoteHeader.BorderBrush = theme.BorderBrush;
        PreviewNoteTitle.Foreground = theme.TextPrimaryBrush;
        PreviewNoteBody.Foreground = theme.TextPrimaryBrush;
        if (PreviewNoteIcon != null)
        {
            PreviewNoteIcon.Foreground = theme.PrimaryBrush;
        }

        double fontSize = CmbDefaultFontSize.SelectedIndex switch
        {
            0 => 12.0,
            2 => 16.0,
            3 => 18.0,
            4 => 20.0,
            _ => 14.0
        };
        PreviewNoteBody.FontSize = fontSize;
    }

    private void RefreshTrashStatus()
    {
        if (_storageService != null)
        {
            int count = _storageService.GetDeletedNotes().Count;
            TxtTrashCount.Text = LocalizationService.T("Trash_CountFormat", count, count == 1 ? "" : "s");
        }
        else
        {
            TxtTrashCount.Text = LocalizationService.T("Trash_CountFormat", 0, "s");
        }
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
            tb.Text = "✓ Trimmed!";
        }
    }

    private void BtnOpenCustomDict_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!System.IO.File.Exists(TextProofingService.CustomDictPath))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TextProofingService.CustomDictPath)!);
                System.IO.File.WriteAllText(TextProofingService.CustomDictPath, "#LID 1033\r\n");
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", TextProofingService.CustomDictPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ModernMessageBox.Show($"Could not open custom dictionary: {ex.Message}", "SmartNotes", ModernMessageButtons.OK, ModernMessageIcon.Warning, "Dictionary Notice");
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
            ModernMessageBox.Show($"Could not open custom rules: {ex.Message}", "SmartNotes", ModernMessageButtons.OK, ModernMessageIcon.Warning, "Autocorrect Rules Notice");
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
        s.KeepBehindAllWindows = ChkDesktopStuck.IsChecked == true;
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

        s.DefaultFontSize = CmbDefaultFontSize.SelectedIndex switch
        {
            0 => 12.0,
            2 => 16.0,
            3 => 18.0,
            4 => 20.0,
            _ => 14.0
        };

        _settingsService.Save();
        _onSettingsSaved();
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
