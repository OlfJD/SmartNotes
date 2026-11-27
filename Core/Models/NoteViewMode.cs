namespace SmartNotes.Core.Models;

public enum NoteViewMode
{
    Text = 0,
    Checklist = 1,
    CopyCompartments = 2,
    /// <summary>Pen / mouse drawing (InkCanvas); the strokes are in NoteItem.Sketch.</summary>
    Sketch = 3
}
