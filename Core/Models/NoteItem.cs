using System;
using System.Collections.Generic;

namespace SmartNotes.Core.Models;

public class NoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public List<TodoCheckItem> Checklist { get; set; } = new();
    public List<CopySnippetItem> CopyList { get; set; } = new();
    public NoteViewMode ViewMode { get; set; } = NoteViewMode.Text;

    public bool IsChecklistMode
    {
        get => ViewMode == NoteViewMode.Checklist;
        set
        {
            if (value && ViewMode != NoteViewMode.Checklist) ViewMode = NoteViewMode.Checklist;
            else if (!value && ViewMode == NoteViewMode.Checklist) ViewMode = NoteViewMode.Text;
        }
    }

    // Desktop Screen Position & Dimensions
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 360;

    // Appearance & Pinning
    public string ColorKey { get; set; } = "Amber";
    public NotePinMode PinMode { get; set; } = NotePinMode.DesktopStuck;
    public bool IsLocked { get; set; } = false;
    public bool IsCollapsed { get; set; } = false;
    public double Opacity { get; set; } = 1.0;
    public double FontSize { get; set; } = 16.0;

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public List<string> Tags { get; set; } = new();

    public string SnippetPreview
    {
        get
        {
            if (ViewMode == NoteViewMode.Checklist && Checklist.Count > 0)
            {
                int done = Checklist.FindAll(c => c.IsDone).Count;
                return $"Checklist ({done}/{Checklist.Count} done): {string.Join(", ", Checklist.ConvertAll(c => c.Text))}";
            }
            if (ViewMode == NoteViewMode.CopyCompartments && CopyList.Count > 0)
            {
                return $"Copy List ({CopyList.Count} items): {string.Join(" | ", CopyList.ConvertAll(c => c.Text))}";
            }
            return string.IsNullOrEmpty(Content) ? "(Empty Note)" : Content;
        }
    }

    public NoteItem Clone()
    {
        return new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrEmpty(Title) ? "" : $"{Title} (Copy)",
            Content = Content,
            Checklist = Checklist.ConvertAll(item => new TodoCheckItem { Id = Guid.NewGuid().ToString("N"), Text = item.Text, IsDone = item.IsDone }),
            CopyList = CopyList.ConvertAll(item => new CopySnippetItem { Id = Guid.NewGuid().ToString("N"), Text = item.Text }),
            ViewMode = ViewMode,
            X = X + 30,
            Y = Y + 30,
            Width = Width,
            Height = Height,
            ColorKey = ColorKey,
            PinMode = PinMode,
            IsLocked = false,
            IsCollapsed = IsCollapsed,
            Opacity = Opacity,
            FontSize = FontSize,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now,
            IsDeleted = false,
            Tags = new List<string>(Tags)
        };
    }
}
