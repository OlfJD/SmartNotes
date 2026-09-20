using System;
using System.Collections.Generic;
using System.Windows;
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
    }

    private void LoadValues()
    {
        var s = _settingsService.Settings;
        ChkStartup.IsChecked = s.LaunchOnStartup;
        ChkDesktopStuck.IsChecked = s.KeepBehindAllWindows;
        ChkHotkeys.IsChecked = s.EnableGlobalHotkeys;

        // Populate Colors
        CmbDefaultColor.ItemsSource = new List<string> { "Amber", "Emerald", "Violet", "Cyan", "Rose", "Obsidian", "Gold", "Mint" };
        CmbDefaultColor.SelectedItem = s.DefaultColorKey;

        // Populate Font Sizes
        CmbDefaultFontSize.ItemsSource = new List<string> { "12 pt", "14 pt (Default)", "16 pt", "18 pt", "20 pt" };
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
        TxtRamStatus.Text = $"Current Physical Working Set: {mb:F1} MB (Optimized for .NET 10)";
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
        double beforeMb = MemoryOptimizer.GetCurrentMemoryUsageMb();
        MemoryOptimizer.TrimMemory();
        double afterMb = MemoryOptimizer.GetCurrentMemoryUsageMb();
        RefreshRamStatus();
        BtnOptimizeRam.Content = "✓ Trimmed!";
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
        s.EnableGlobalHotkeys = ChkHotkeys.IsChecked == true;

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
