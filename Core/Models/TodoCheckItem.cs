using System;

namespace SmartNotes.Core.Models;

public class TodoCheckItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public bool IsDone { get; set; } = false;
}
