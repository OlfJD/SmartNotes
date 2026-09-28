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

        _suppressEvents = true;
        try
        {
            SliderUnfocusedOpacity.Value = Math.Round(s.UnfocusedOpacity * 100);
            TxtUnfocusedOpacityPercent.Text = $"{(int)SliderUnfocusedOpacity.Value}%";
            PnlUnfocusedOpacitySlider.Opacity = s.EnableUnfocusedTransparency ? 1.0 : 0.4;
            SliderUnfocusedOpacity.IsEnabled = s.EnableUnfocusedTransparency;

            // Populate Proofing Languages
            CmbProofingLanguage.ItemsSource = TextProofingService.SupportedLanguages.Select(l => l.DisplayName).ToList();
            var currentLang = TextProofingService.SupportedLanguages.FirstOrDefault(l => l.Code.Equals(s.ProofingLanguage, StringComparison.OrdinalIgnoreCase))
                ?? TextProofingService.SupportedLanguages[0];
            CmbProofingLanguage.SelectedItem = currentLang.DisplayName;

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
            TxtTrashCount.Text = $"{count} Note{(count == 1 ? "" : "s")} in Trash";
        }
        else
        {
            TxtTrashCount.Text = "Trash Ready (48h)";
        }
    }

    private void RefreshRamStatus()
    {
        double mb = MemoryOptimizer.GetCurrentMemoryUsageMb();
        TxtRamStatus.Text = $"Working Set RAM: {mb:F1} MB (Optimized for .NET 10)";
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
            MessageBox.Show("Trash folder emptied successfully.", "SmartNotes Trash", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnOptimizeRam_Click(object sender, RoutedEventArgs e)
    {
        MemoryOptimizer.TrimMemory();
        RefreshRamStatus();
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
            MessageBox.Show($"Could not open custom dictionary: {ex.Message}", "SmartNotes", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show($"Could not open custom rules: {ex.Message}", "SmartNotes", MessageBoxButton.OK, MessageBoxImage.Warning);
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
