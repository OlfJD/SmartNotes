using System;

namespace SmartNotes.Core.Models;

public class AppSettings
{
    public bool LaunchOnStartup { get; set; } = true;
    public string DefaultColorKey { get; set; } = "Amber";
    public string DefaultSkinKey { get; set; } = "Classic";

    /// <summary>Version of the stored skin keys; raised by App.MigrateSkinKeys().</summary>
    public int SkinDataVersion { get; set; } = 0;

    /// <summary>Chosen title / text font per skin: skin key -> SkinFontOption.Key (missing = skin default).</summary>
    public Dictionary<string, string> SkinTitleFonts { get; set; } = new();
    public Dictionary<string, string> SkinTextFonts { get; set; } = new();
    public NotePinMode DefaultPinMode { get; set; } = NotePinMode.DesktopStuck;
    public double DefaultFontSize { get; set; } = 16.0;
    public double DefaultOpacity { get; set; } = 1.0;
    public bool EnableGlobalHotkeys { get; set; } = true;
    /// <summary>Hotkeys last reported to the user as taken by another program ("Win+Alt+N, ..."), so it is said once.</summary>
    public string ReportedHotkeyConflicts { get; set; } = "";
    public bool HideAllNotes { get; set; } = false;
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

