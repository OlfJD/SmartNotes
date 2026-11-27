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
    public string SkinKey { get; set; } = "Classic";
    /// <summary>Great Rune shown by the Elden Ring skin (replaces the colour there). See GreatRune.</summary>
    public string RuneKey { get; set; } = GreatRune.DefaultKey;
    /// <summary>
    /// The emblem chosen per skin in the colour popup: skin key → emblem key (Astral: a constellation, Deep Sea: a
    /// creature). Missing = picked automatically from the note's id, so every note keeps its own.
    /// </summary>
    public Dictionary<string, string> Emblems { get; set; } = new();

    public string GetEmblem(string skinKey) => Emblems != null && Emblems.TryGetValue(skinKey, out var key) ? key ?? "" : "";

    public void SetEmblem(string skinKey, string? key)
    {
        Emblems ??= new();
        if (string.IsNullOrEmpty(key)) Emblems.Remove(skinKey);
        else Emblems[skinKey] = key;
    }

    /// <summary>Until 2.6 the Astral constellation had a field of its own: still read (into Emblems), never written.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("ConstellationKey")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyConstellationKey
    {
        get => null;
        set { if (!string.IsNullOrEmpty(value)) SetEmblem(NoteSkin.AstralKey, value); }
    }
    public NotePinMode PinMode { get; set; } = NotePinMode.DesktopStuck;
    /// <summary>Exe file name of the program this note is linked to ("Code.exe"); empty = not linked. See AppLink.</summary>
    public string LinkedApp { get; set; } = "";
    /// <summary>The linked program's name as shown in the menu and footer ("Visual Studio Code").</summary>
    public string LinkedAppName { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAppLinked => !string.IsNullOrWhiteSpace(LinkedApp);
    public bool IsLocked { get; set; } = false;

    /// <summary>"Remind me": the note comes forward and glows at this time (see NoteSchedule). Null = no reminder.</summary>
    public NoteReminder? Reminder { get; set; }

    /// <summary>"Repeat daily" checklist: every task is unticked when a new day starts (see NoteSchedule.ResetDaily).</summary>
    public bool RepeatDaily { get; set; } = false;
    /// <summary>The day the current ticks belong to.</summary>
    public DateTime? DailyDay { get; set; }
    /// <summary>Days in a row, before <see cref="DailyDay"/>, on which every task was done.</summary>
    public int Streak { get; set; } = 0;
    public double Opacity { get; set; } = 1.0;
    public double FontSize { get; set; } = 16.0;

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

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
            SkinKey = SkinKey,
            RuneKey = RuneKey,
            PinMode = PinMode,
            LinkedApp = LinkedApp,
            LinkedAppName = LinkedAppName,
            // a copy keeps "repeat daily" but not the reminder (it would ring twice)
            RepeatDaily = RepeatDaily,
            DailyDay = DailyDay,
            IsLocked = false,
            Opacity = Opacity,
            FontSize = FontSize,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now,
            IsDeleted = false
        };
    }
}
