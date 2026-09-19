using System;

namespace SmartNotes.Core.Models;

public class AppSettings
{
    public bool LaunchOnStartup { get; set; } = true;
    public string DefaultColorKey { get; set; } = "Amber";
    public NotePinMode DefaultPinMode { get; set; } = NotePinMode.DesktopStuck;
    public double DefaultFontSize { get; set; } = 16.0;
    public double DefaultOpacity { get; set; } = 1.0;
    public bool EnableSoundCues { get; set; } = true;
    public bool EnableGlobalHotkeys { get; set; } = true;
    public string NewNoteHotKey { get; set; } = "Win+Alt+N";
    public string HubHotKey { get; set; } = "Win+Alt+H";
    public string ToggleNotesHotKey { get; set; } = "Win+Alt+D";
    public bool HideAllNotes { get; set; } = false;
    public bool ConfirmDelete { get; set; } = false;
    public bool KeepBehindAllWindows { get; set; } = true;
}
