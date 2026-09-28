using System;

namespace SmartNotes.Core.Models;

public class AutocorrectRule
{
    public string Shortcut { get; set; } = "";
    public string Replacement { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    public AutocorrectRule() { }

    public AutocorrectRule(string shortcut, string replacement, bool isEnabled = true)
    {
        Shortcut = shortcut;
        Replacement = replacement;
        IsEnabled = isEnabled;
    }
}

public class AutocorrectUndoItem
{
    public int StartIndex { get; set; }
    public string OriginalWord { get; set; } = "";
    public string Replacement { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public AutocorrectUndoItem(int startIndex, string originalWord, string replacement)
    {
        StartIndex = startIndex;
        OriginalWord = originalWord;
        Replacement = replacement;
        Timestamp = DateTime.UtcNow;
    }
}
