using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartNotes.Core.Models;

/// <summary>An earlier state of a note's content (see NoteHistoryService). Position, size and colours are not part of it.</summary>
public class NoteVersion
{
    /// <summary>When the note last looked like this.</summary>
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public NoteViewMode ViewMode { get; set; } = NoteViewMode.Text;
    public List<TodoCheckItem> Checklist { get; set; } = new();
    public List<CopySnippetItem> CopyList { get; set; } = new();

    /// <summary>Same content? (Ignores SavedAt and item ids.)</summary>
    public bool SameContentAs(NoteVersion other) =>
        Title == other.Title &&
        Content == other.Content &&
        ViewMode == other.ViewMode &&
        Checklist.Select(i => (i.Text, i.IsDone)).SequenceEqual(other.Checklist.Select(i => (i.Text, i.IsDone))) &&
        CopyList.Select(i => i.Text).SequenceEqual(other.CopyList.Select(i => i.Text));
}
