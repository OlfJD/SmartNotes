using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;

namespace SmartNotes.UI.Windows;

/// <summary>
/// The note window's part of the sync folder (see SyncService): "look" changes get their own time stamp
/// (NoteItem.ChangedAt), and a newer version from another PC can be put into the open note.
/// </summary>
public partial class StickyNoteWindow
{
    /// <summary>The look and settings of the note as last saved (not its text, position or size).</summary>
    private string? _savedStyle;

    private string StyleFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(_note.ColorKey).Append('|').Append(_note.SkinKey).Append('|').Append(_note.RuneKey).Append('|')
          .Append(string.Join(",", (_note.Emblems ?? new()).OrderBy(e => e.Key).Select(e => e.Key + "=" + e.Value))).Append('|')
          .Append(_note.FontSize).Append('|').Append(Math.Round(_userConfiguredOpacity, 2)).Append('|')
          .Append(_note.PinMode).Append('|').Append(_note.IsLocked).Append('|').Append(_note.LinkedApp).Append('|')
          .Append(_note.Reminder == null ? "" : JsonSerializer.Serialize(_note.Reminder)).Append('|')
          .Append(_note.RepeatDaily);
        return sb.ToString();
    }

    /// <summary>Called by SaveNoteState: a change of look or settings stamps ChangedAt (so it syncs, too).</summary>
    private void StampStyleChange()
    {
        string style = StyleFingerprint();
        if (_savedStyle != null && style != _savedStyle) _note.ChangedAt = DateTime.Now;
        _savedStyle = style;
    }

    /// <summary>
    /// Puts a newer version of this note from another PC into the window (its position and size stay this PC's).
    /// Returns false while an edit here is still waiting to be saved: that edit is newer and wins.
    /// If the text here differs, it is kept as an earlier version first, so nothing typed here is lost.
    /// </summary>
    public bool ApplyRemote(NoteItem remote)
    {
        if (_saveDebounceTimer?.IsEnabled == true || _saveRetryTimer?.IsEnabled == true) return false;

        var before = TakeVersion();
        var incoming = new NoteVersion { Title = remote.Title, Content = remote.Content, ViewMode = remote.ViewMode, Checklist = remote.Checklist, CopyList = remote.CopyList, Sketch = remote.Sketch };
        if (!before.SameContentAs(incoming)) NoteHistoryService.AddVersion(_note.Id, before);

        _note.Title = remote.Title ?? "";
        _note.Content = remote.Content ?? "";
        _note.ColorKey = remote.ColorKey;
        _note.SkinKey = remote.SkinKey;
        _note.RuneKey = remote.RuneKey;
        _note.Emblems = new(remote.Emblems ?? new());
        _note.PinMode = remote.PinMode;
        _note.Opacity = remote.Opacity;
        _note.FontSize = remote.FontSize;
        _note.LinkedApp = remote.LinkedApp ?? "";
        _note.LinkedAppName = remote.LinkedAppName ?? "";
        _note.Reminder = remote.Reminder;
        _note.RepeatDaily = remote.RepeatDaily;
        _note.DailyDay = remote.DailyDay;
        _note.Streak = remote.Streak;
        _note.CreatedAt = remote.CreatedAt;

        TxtTitle.Text = _note.Title;
        TxtContent.Text = _note.Content;
        TxtTitlePlaceholder.Visibility = _note.Title.Length == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        TxtContentPlaceholder.Visibility = _note.Content.Length == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        _checklistItems.Clear();
        foreach (var item in remote.Checklist ?? new()) _checklistItems.Add(new TodoCheckItem { Id = item.Id, Text = item.Text, IsDone = item.IsDone });
        _copyItems.Clear();
        foreach (var item in remote.CopyList ?? new()) _copyItems.Add(new CopySnippetItem { Id = item.Id, Text = item.Text });
        SetSketchStrokes(SketchData.FromBase64(remote.Sketch));

        _userConfiguredOpacity = Math.Clamp(_note.Opacity, 0.3, 1.0);
        ApplyFocusOpacity(IsActive);
        ApplyFontSizes();
        ApplyTheme(_note.ColorKey);
        UpdateViewMode(remote.ViewMode);
        UpdatePinModeUI(_note.PinMode);
        UpdateLockUI(remote.IsLocked);
        UpdateReminderBadge();

        // the other PC's time stamps, not "now": this is not an edit made here
        _note.ModifiedAt = remote.ModifiedAt;
        _note.ChangedAt = remote.ChangedAt;
        _savedContent = ContentFingerprint();
        _savedStyle = StyleFingerprint();
        _lastSavedVersion = TakeVersion();
        _saveDebounceTimer?.Stop(); // setting the text asked for a save; write directly instead
        _note.Checklist = new(_checklistItems);
        _note.CopyList = new(_copyItems);
        _note.Sketch = SketchData.ToBase64(SketchCanvas.Strokes);
        _storageService.UpdateNote(_note);
        UpdateModifiedTime();
        return true;
    }

    /// <summary>Another PC deleted this note: it goes to this PC's trash too (so it can still be restored here).</summary>
    public void RemoveBySync()
    {
        SaveNoteState();
        if (!_storageService.DeleteNote(_note.Id, permanent: false)) return; // no trash copy: keep the note
        _onClosedCallback(this);
        Close();
    }
}
