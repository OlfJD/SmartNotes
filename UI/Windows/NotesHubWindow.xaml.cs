using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;

namespace SmartNotes.UI.Windows;

public partial class NotesHubWindow : Window
{
    private readonly NoteStorageService _storageService;
    private readonly SettingsService _settingsService;
    private readonly Action<NoteItem> _onSpawnNewNote;
    private readonly Action<Guid> _onLocateNote;
    private readonly Action _onArrangeNotes;
    private readonly Action _onOpenSettings;

    private string _currentFilter = "All";

    public NotesHubWindow(
        NoteStorageService storageService,
        SettingsService settingsService,
        Action<NoteItem> onSpawnNewNote,
        Action<Guid> onLocateNote,
        Action onArrangeNotes,
        Action onOpenSettings)
    {
        _storageService = storageService;
        _settingsService = settingsService;
        _onSpawnNewNote = onSpawnNewNote;
        _onLocateNote = onLocateNote;
        _onArrangeNotes = onArrangeNotes;
        _onOpenSettings = onOpenSettings;

        InitializeComponent();

        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        WindowBlurHelper.ApplyModernWindowStyles(this);
        RefreshNotesList();
    }

    public void RefreshNotesList()
    {
        if (_storageService?.Notes == null || NotesItemsControl == null || EmptyStatePanel == null || NotesScrollViewer == null || TxtStatusActiveCount == null)
            return;

        var allNotes = _storageService.Notes;
        var query = TxtSearch?.Text?.Trim().ToLowerInvariant() ?? "";

        IEnumerable<NoteItem> filtered = allNotes;

        // Apply Tab Filter
        if (_currentFilter == "Trash")
        {
            filtered = filtered.Where(n => n.IsDeleted);
        }
        else
        {
            filtered = filtered.Where(n => !n.IsDeleted);

            if (_currentFilter == "Checklists")
            {
                filtered = filtered.Where(n => n.IsChecklistMode);
            }
        }

        // Apply Search Filter
        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(n =>
                (n.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                (n.Content?.ToLowerInvariant().Contains(query) ?? false) ||
                (n.Checklist != null && n.Checklist.Any(c => c.Text?.ToLowerInvariant().Contains(query) ?? false)) ||
                (n.CopyList != null && n.CopyList.Any(c => c.Text?.ToLowerInvariant().Contains(query) ?? false))
            );
        }

        var resultList = filtered.OrderByDescending(n => n.ModifiedAt).ToList();
        NotesItemsControl.ItemsSource = resultList;

        bool hasItems = resultList.Count > 0;
        EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        NotesScrollViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;

        int activeCount = allNotes.Count(n => !n.IsDeleted);
        TxtStatusActiveCount.Text = $"Active Sticky Notes: {activeCount}";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtSearchPlaceholder != null)
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        RefreshNotesList();
    }

    private void FilterTab_Changed(object sender, RoutedEventArgs e)
    {
        if (TabAll?.IsChecked == true) _currentFilter = "All";
        else if (TabChecklists?.IsChecked == true) _currentFilter = "Checklists";
        else if (TabTrash?.IsChecked == true) _currentFilter = "Trash";

        RefreshNotesList();
    }

    private void BtnNewNote_Click(object sender, RoutedEventArgs e)
    {
        var newNote = new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = "",
            Content = "",
            X = 120 + new Random().Next(0, 100),
            Y = 120 + new Random().Next(0, 100),
            ColorKey = _settingsService.Settings.DefaultColorKey,
            PinMode = _settingsService.Settings.DefaultPinMode,
            FontSize = _settingsService.Settings.DefaultFontSize,
            Opacity = _settingsService.Settings.DefaultOpacity,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };

        _storageService.AddNote(newNote);
        _onSpawnNewNote(newNote);
        RefreshNotesList();
    }

    private void BtnArrange_Click(object sender, RoutedEventArgs e)
    {
        _onArrangeNotes();
    }

    private void BtnLocateNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is NoteItem note)
        {
            if (note.IsDeleted)
            {
                _storageService.RestoreNote(note.Id);
                _onSpawnNewNote(note);
                RefreshNotesList();
            }
            else
            {
                _onLocateNote(note.Id);
            }
        }
    }

    private void BtnDuplicateNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is NoteItem note)
        {
            var clone = note.Clone();
            _storageService.AddNote(clone);
            _onSpawnNewNote(clone);
            RefreshNotesList();
        }
    }

    private void BtnCardDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is NoteItem note)
        {
            if (note.IsDeleted)
            {
                _storageService.DeleteNote(note.Id, permanent: true);
            }
            else
            {
                _storageService.DeleteNote(note.Id, permanent: false);
            }
            RefreshNotesList();
        }
    }

    private void BtnExportAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "JSON Backup (*.json)|*.json",
                FileName = $"SmartNotes_Backup_{DateTime.Now:yyyyMMdd}.json"
            };
            if (sfd.ShowDialog() == true)
            {
                string json = _storageService.ExportAllToJson();
                File.WriteAllText(sfd.FileName, json);
                MessageBox.Show("All sticky notes successfully exported!", "SmartNotes Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export error: {ex.Message}", "SmartNotes Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON Backup (*.json)|*.json"
            };
            if (ofd.ShowDialog() == true)
            {
                string json = File.ReadAllText(ofd.FileName);
                if (_storageService.ImportFromJson(json))
                {
                    RefreshNotesList();
                    MessageBox.Show("Notes imported successfully!", "SmartNotes Import", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Invalid backup file format.", "SmartNotes Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import error: {ex.Message}", "SmartNotes Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        _onOpenSettings();
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
