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
    public bool EnableUnfocusedTransparency { get; set; } = false;
    public double UnfocusedOpacity { get; set; } = 0.55;

    // Version 2.1: Multi-Language Localization & Text Proofing
    public string AppLanguage { get; set; } = "en";
    public string? LastLanguagePromptVersion { get; set; } = null;
    public bool EnableSpellCheck { get; set; } = true;
    public string ProofingLanguage { get; set; } = "en-US";
    public bool EnableAutocorrect { get; set; } = true;
    public bool AutoCapitalizeSentences { get; set; } = true;
    public bool SmartSymbolReplacements { get; set; } = true;
    public bool CustomReplacementsEnabled { get; set; } = true;

    // Version 2.0.4: Performance & Memory Trimming
    public bool AutoOptimizeMemory { get; set; } = true;
}

