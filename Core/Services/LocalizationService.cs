using System;
using System.Collections.Generic;
using System.Globalization;

namespace SmartNotes.Core.Services;

public class LocalizationService
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    public string CurrentLanguage { get; private set; } = "en";

    public event Action? LanguageChanged;

    public static readonly IReadOnlyList<(string Code, string DisplayName)> SupportedLanguages = new List<(string, string)>
    {
        ("en", "English (Default)"),
        ("de", "Deutsch (German)")
    };

    private readonly Dictionary<string, Dictionary<string, string>> _strings = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService()
    {
        InitializeStrings();
    }

    public void SetLanguage(string langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode)) langCode = "en";
        langCode = langCode.Split('-')[0].ToLowerInvariant();
        if (langCode != "de" && langCode != "en")
        {
            langCode = "en";
        }

        if (CurrentLanguage != langCode)
        {
            CurrentLanguage = langCode;
            LanguageChanged?.Invoke();
        }
    }

    public string Get(string key, params object[] args)
    {
        if (_strings.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return args.Length > 0 ? string.Format(val, args) : val;
        }

        // Fallback to English
        if (_strings.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
        {
            return args.Length > 0 ? string.Format(enVal, args) : enVal;
        }

        return key;
    }

    public static string T(string key, params object[] args) => Instance.Get(key, args);

    private void InitializeStrings()
    {
        // ---------------- ENGLISH (en) ----------------
        var en = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // General App & Settings Header
            ["App_Title"] = "SmartNotes",
            ["App_VersionBadge"] = "SmartNotes V2.1",
            ["Settings_Title"] = "Settings & Preferences",
            ["Settings_Subtitle"] = "Customize desktop behavior, themes, typing proofing, and performance",
            ["Settings_PreferencesNav"] = "PREFERENCES",
            ["Settings_Engine"] = "SmartNotes Engine",
            ["Settings_EngineInfo"] = ".NET 10.0 • High-DPI Ready",

            // Navigation
            ["Nav_General"] = "Desktop & System",
            ["Nav_Appearance"] = "Note Appearance",
            ["Nav_Hotkeys"] = "Global Hotkeys",
            ["Nav_Proofing"] = "Text Proofing",
            ["Nav_Trash"] = "Trash & Recovery",
            ["Nav_Performance"] = "Memory & Perf",

            // Footer
            ["Btn_Save"] = "Save Settings",
            ["Btn_Cancel"] = "Cancel",
            ["Status_SaveInstant"] = "Changes take effect immediately upon saving",

            // General Panel (Desktop & System)
            ["General_SectionTitle"] = "Desktop & System",
            ["General_SectionDesc"] = "Control how sticky notes run in the background and interact with your wallpaper.",
            ["General_AppLangTitle"] = "Application UI Language",
            ["General_AppLangDesc"] = "Select your preferred display language for the SmartNotes interface.",
            ["General_StartupTitle"] = "Launch with Windows Startup",
            ["General_StartupDesc"] = "Automatically start SmartNotes and restore your pinned sticky notes on system boot.",
            ["General_DesktopStuckTitle"] = "Desktop Sticking (Fixed to Wallpaper)",
            ["General_DesktopStuckDesc"] = "Pin notes directly onto your desktop wallpaper underneath open applications and full-screen games.",
            ["General_TransparencyTitle"] = "Transparent When Unfocused",
            ["General_TransparencyDesc"] = "Automatically reduce note opacity when you are working inside other applications.",
            ["General_UnfocusedOpacityTitle"] = "Unfocused Inactivity Opacity",
            ["General_UnfocusedOpacityDesc"] = "Opacity level applied when cursor is not hovering over note",

            // Note Appearance Panel
            ["Appearance_SectionTitle"] = "Default Note Appearance",
            ["Appearance_SectionDesc"] = "Choose default theme palettes, font size, and visual styling for newly created notes.",
            ["Appearance_DefaultColorTitle"] = "Default Theme Color",
            ["Appearance_DefaultColorDesc"] = "Primary neon accent and card tint for new sticky notes.",
            ["Appearance_QuickSwatches"] = "Quick Preset Swatches (Click to Select)",
            ["Appearance_DefaultFontSizeTitle"] = "Default Font Size",
            ["Appearance_DefaultFontSizeDesc"] = "Default text scaling for note content, checklists, and copy rows.",
            ["Appearance_LivePreview"] = "LIVE PREVIEW",
            ["Appearance_PreviewTitle"] = "Quick Ideas & Tasks",
            ["Appearance_PreviewPinned"] = "📌 Pinned",
            ["Appearance_PreviewBody"] = "SmartNotes is modern, lightning-fast, and stays right where you need it.",

            // Hotkeys Panel
            ["Hotkeys_SectionTitle"] = "Global System Hotkeys",
            ["Hotkeys_SectionDesc"] = "Quickly create notes, search notes, or toggle desktop visibility anywhere in Windows.",
            ["Hotkeys_ToggleTitle"] = "Enable Global System Hotkeys",
            ["Hotkeys_ToggleDesc"] = "Listen for keyboard shortcuts across any active Windows application.",
            ["Hotkeys_ActiveShortcuts"] = "ACTIVE KEYBOARD SHORTCUTS",
            ["Hotkeys_NewNote"] = "Create New Sticky Note",
            ["Hotkeys_NewNoteDesc"] = "Instantly spawns a new note at your cursor location",
            ["Hotkeys_NotesHub"] = "Open Notes Hub / Manager",
            ["Hotkeys_NotesHubDesc"] = "Search, sort, filter, and organize all your desktop notes in one place",
            ["Hotkeys_ToggleNotes"] = "Show / Hide All Desktop Notes",
            ["Hotkeys_ToggleNotesDesc"] = "Quickly toggle visibility for clean desktop presentations",

            // Proofing Panel
            ["Proofing_SectionTitle"] = "Text Proofing & Autocorrect (V2.0)",
            ["Proofing_SectionDesc"] = "Smart typing intelligence, real-time spellcheck, autocorrect rules, and symbol shortcuts.",
            ["Proofing_SpellCheckTitle"] = "Real-Time Spell Checking",
            ["Proofing_SpellCheckDesc"] = "Underline misspelled words with suggestions on right-click.",
            ["Proofing_DictTitle"] = "Proofing Language Dictionary",
            ["Proofing_DictDesc"] = "Active dictionary used for spell checking verification.",
            ["Proofing_WindowsDictNotice"] = "Windows Spell Checking relies on Windows language dictionaries. Any language installed on your PC (e.g. English, German) works automatically. To add more languages to Windows, open Windows Settings → Time & Language → Language.",
            ["Proofing_AutocorrectTitle"] = "Autocorrect Typos As You Type",
            ["Proofing_AutocorrectDesc"] = "Automatically fix common typos (e.g., 'teh' → 'the', 'dont' → 'don't'). Press Backspace to revert.",
            ["Proofing_AutoCapTitle"] = "Auto-Capitalize Sentences",
            ["Proofing_AutoCapDesc"] = "Capitalize the first letter of sentences and standalone 'i'.",
            ["Proofing_SmartSymbolsTitle"] = "Smart Symbol Replacements",
            ["Proofing_SmartSymbolsDesc"] = "Convert typing shortcuts like '->' to '→', '--' to '—', '!=' to '≠', '(c)' to '©'.",
            ["Proofing_CustomDictBtn"] = "Custom Dictionary (.lex)",
            ["Proofing_CustomDictDesc"] = "Add custom learned words",
            ["Proofing_CustomRulesBtn"] = "Autocorrect Rules (.json)",
            ["Proofing_CustomRulesDesc"] = "Define custom text expansions",
            ["Proofing_StatusSupported"] = "✓ Windows spell check dictionary is ready for {0}",
            ["Proofing_StatusNotSupported"] = "ℹ️ Note: Windows dictionary for {0} is not installed on this PC. It will automatically work on any PC with this language pack installed (such as your private PC).",

            // Trash Panel
            ["Trash_SectionTitle"] = "Trash & 48-Hour Recovery",
            ["Trash_SectionDesc"] = "Safeguard against accidental deletions with automatic local retention.",
            ["Trash_RetentionTitle"] = "48-Hour Safety Recovery Period",
            ["Trash_RetentionDesc"] = "Deleted notes are automatically preserved locally for 48 hours before permanent purging.",
            ["Trash_CountFormat"] = "{0} Note{1} in Trash",
            ["Trash_ActionsTitle"] = "Actions & File Management",
            ["Trash_OpenExplorerBtn"] = "Open Trash Folder in Explorer",
            ["Trash_EmptyBtn"] = "Empty Trash Folder Now",
            ["Trash_EmptiedToast"] = "Trash bin emptied. All deleted notes have been purged.",

            // Performance Panel
            ["Perf_SectionTitle"] = "Memory & System Performance",
            ["Perf_SectionDesc"] = "Manage background RAM optimization, automatic working set trimming, and performance.",
            ["Perf_AutoTrimTitle"] = "Automatic Background RAM Trimming",
            ["Perf_AutoTrimDesc"] = "Continuously compacts memory heap and flushes inactive working set pages back to Windows every 3 minutes when SmartNotes is idle.",
            ["Perf_WorkingSetTitle"] = "Physical Working Set RAM Footprint",
            ["Perf_WorkingSetDesc"] = "SmartNotes aggressively releases unreferenced memory pages directly back to the Windows OS kernel.",
            ["Perf_TrimNowBtn"] = "Trim RAM Now",
            ["Perf_TrimmedStatus"] = "Working Set: {0:F1} MB (Freed {1:F1} MB!)",
            ["Perf_HealthyStatus"] = "Working Set RAM: {0:F1} MB (Healthy)",
            ["Perf_HardwareAccelTitle"] = "Hardware Acceleration",
            ["Perf_HardwareAccelDesc"] = "DWM DirectX / WPF Pipeline Enabled",
            ["Perf_GcTitle"] = "Garbage Collection",
            ["Perf_GcDesc"] = ".NET 10 Non-Intrusive LOH Compaction",
            ["Perf_ZeroRamTitle"] = "Zero-RAM Idle Architecture",
            ["Perf_ZeroRamDesc"] = "Installed Windows language packs, dictionaries, and unopened notes remain stored on disk and consume 0 MB of RAM while inactive. Only active note dictionaries are loaded into memory on demand.",

            // Sticky Note Pin Indicators & Modes
            ["Pin_StuckToDesktop"] = "📌 Stuck to Desktop",
            ["Pin_AlwaysOnTop"] = "📌 Always on Top",
            ["Pin_Normal"] = "📌 Normal Window",
            ["Tooltip_Pin_StuckToDesktop"] = "Mode: Stuck to Desktop (Behind all apps). Click to Float Always on Top",
            ["Tooltip_Pin_AlwaysOnTop"] = "Mode: Always on Top (Floating). Click to Stick to Desktop",
            ["Tooltip_Pin_Normal"] = "Mode: Normal Window. Click to Stick to Desktop",

            // Sticky Note Tooltips & Placeholders
            ["Tooltip_NewNote"] = "Create new sticky note",
            ["Tooltip_LockNote"] = "Lock note (prevent editing)",
            ["Tooltip_UnlockNote"] = "Unlock note (allow editing)",
            ["Tooltip_TextMode"] = "Switch to Plain Text Mode",
            ["Tooltip_ActiveTextMode"] = "Active: Plain Text Mode",
            ["Tooltip_ChecklistMode"] = "Switch to Checklist Mode",
            ["Tooltip_ActiveChecklistMode"] = "Active: Checklist Mode",
            ["Tooltip_CopyMode"] = "Switch to Copy Compartments Mode",
            ["Tooltip_ActiveCopyMode"] = "Active: Copy Compartments Mode",
            ["Tooltip_ColorPalette"] = "Colors, Themes & Transparency",
            ["Tooltip_DeleteNote"] = "Move note to trash (48h safety retention)",
            ["Tooltip_MoreOptions"] = "More options...",
            ["Tooltip_AddTask"] = "Add task",
            ["Tooltip_DeleteTask"] = "Delete task",
            ["Tooltip_AddSnippet"] = "Add text snippet",
            ["Tooltip_CopySnippet"] = "Copy this line to clipboard",
            ["Tooltip_Copied"] = "Copied! ✓",
            ["Tooltip_DeleteSnippet"] = "Delete item",
            ["Placeholder_Title"] = "Title (optional)...",
            ["Placeholder_Content"] = "Take a note...",
            ["Placeholder_NewChecklist"] = "Add new task (Press Enter)...",
            ["Placeholder_NewSnippet"] = "Add new copy item (Press Enter)...",

            // Context Menu & More Options Menu
            ["Menu_SpellingSuggestions"] = "SPELLING SUGGESTIONS",
            ["Menu_NoSuggestions"] = "(No spelling suggestions)",
            ["Menu_AddToDict"] = "Add \"{0}\" to Dictionary",
            ["Menu_IgnoreAll"] = "Ignore All Occurrences",
            ["Menu_Cut"] = "Cut",
            ["Menu_Copy"] = "Copy",
            ["Menu_Paste"] = "Paste",
            ["Menu_SelectAll"] = "Select All",
            ["Menu_Proofing"] = "Text Proofing & Language",
            ["Menu_SpellCheck"] = "Spell Check",
            ["Menu_SpellCheckEnabled"] = "Spell Check Underlines (Enabled)",
            ["Menu_SpellCheckDisabled"] = "Spell Check Underlines (Disabled)",
            ["Menu_Autocorrect"] = "Autocorrect Common Typos",
            ["Menu_AutocorrectEnabled"] = "Autocorrect Common Typos (Enabled)",
            ["Menu_AutocorrectDisabled"] = "Autocorrect Common Typos (Disabled)",
            ["Menu_AutoCapitalize"] = "Auto-Capitalize Sentences",
            ["Menu_AutoCapitalizeEnabled"] = "Auto-Capitalize Sentences (Enabled)",
            ["Menu_AutoCapitalizeDisabled"] = "Auto-Capitalize Sentences (Disabled)",
            ["Menu_SmartSymbols"] = "Smart Symbols: -> to →",
            ["Menu_SmartSymbolsEnabled"] = "Smart Symbols: -> to → (Enabled)",
            ["Menu_SmartSymbolsDisabled"] = "Smart Symbols (Disabled)",
            ["Menu_ProofingLang"] = "Proofing Language ({0})",
            ["Menu_Language"] = "Language",
            ["Menu_FontSize"] = "Font Size ({0}pt)",
            ["Menu_IncreaseFontSize"] = "Increase Font Size (+)",
            ["Menu_DecreaseFontSize"] = "Decrease Font Size (-)",
            ["Menu_ResetFontSize"] = "Reset Font Size (16pt)",
            ["Menu_OpacityHeader"] = "Opacity ({0}%)",
            ["Menu_Opacity100"] = "100% Solid",
            ["Menu_Opacity90"] = "90% Crisp",
            ["Menu_Opacity80"] = "80% Glass",
            ["Menu_Opacity65"] = "65% Translucent",
            ["Menu_Opacity50"] = "50% Stealth",
            ["Menu_DimUnfocusedEnabled"] = "Dim When Unfocused (Enabled)",
            ["Menu_DimUnfocusedDisabled"] = "Dim When Unfocused (Disabled)",
            ["Menu_CopyAllContent"] = "Copy All Content (Ctrl+Shift+C)",
            ["Menu_ExportMarkdown"] = "Export as Markdown (.md)",
            ["Menu_DuplicateNote"] = "Duplicate Note",
            ["Menu_ClearCompletedTasks"] = "Clear Completed Tasks",
            ["Menu_DeleteNote"] = "Delete Note",

            // Color Palette & Transparency Popup
            ["ColorPopup_QuickThemes"] = "Quick Neon Themes",
            ["ColorPopup_CustomCanvas"] = "Custom Palette Canvas",
            ["ColorPopup_Apply"] = "Apply",
            ["ColorPopup_TransparencyDimming"] = "Transparency & Dimming",
            ["ColorPopup_NoteOpacity"] = "Note Opacity",
            ["ColorPopup_DimUnfocused"] = "Dim When Unfocused",
            ["ColorPopup_UnfocusedOpacity"] = "Unfocused Opacity",

            // Theme Preset Swatch Names
            ["Theme_Amber"] = "Radiant Amber",
            ["Theme_Emerald"] = "Neon Emerald",
            ["Theme_Violet"] = "Cyber Violet",
            ["Theme_Cyan"] = "Electric Cyan",
            ["Theme_Rose"] = "Coral Rose",
            ["Theme_Obsidian"] = "Stealth Obsidian",
            ["Theme_Gold"] = "Classic Sticky Gold",
            ["Theme_Mint"] = "Fresh Mint",

            // Timestamps & Status
            ["Time_JustNow"] = "Just now",
            ["Time_MinutesAgo"] = "{0}m ago",
            ["Time_HoursAgo"] = "{0}h ago",
            ["Status_Typing"] = "Typing...",
            ["Status_Saved"] = "Saved",
            ["Status_Locked"] = "LOCKED",

            // Note Export
            ["Export_Filter"] = "Markdown File (*.md)|*.md|Text File (*.txt)|*.txt",
            ["Export_Failed"] = "Failed to export note: {0}",
            ["Export_DialogTitle"] = "SmartNotes Export",
            ["Export_Heading"] = "Export Notice",

            // Tray Menu
            ["Tray_NewNote"] = "New Sticky Note",
            ["Tray_BringAllToFront"] = "Bring All Notes to Front",
            ["Tray_ShowAll"] = "Show All Desktop Notes",
            ["Tray_HideAll"] = "Hide All Desktop Notes",
            ["Tray_Arrange"] = "Arrange Notes on Desktop",
            ["Tray_Trash"] = "Trash & 48h Recovery",
            ["Tray_RestoreNote"] = "Restore: \"{0}\" ({1})",
            ["Tray_RestoreAll"] = "Restore All Notes",
            ["Tray_EmptyTrash"] = "Empty Trash Now",
            ["Tray_OpenTrashFolder"] = "Open Trash Folder in Explorer...",
            ["Tray_NoTrashNotes"] = "(No recently deleted notes)",
            ["Tray_Startup"] = "Start with Windows",
            ["Tray_Settings"] = "Settings & Preferences...",
            ["Tray_OptimizeRam"] = "Optimize Memory (RAM)",
            ["Tray_CheckUpdates"] = "Check for Updates... (v{0})",
            ["Tray_Exit"] = "Exit SmartNotes",
            ["Tray_RamOptimizedBalloon"] = "RAM working set trimmed to {0:F1} MB (Freed {1:F1} MB)!",
            ["Tray_TimeLeftHours"] = "{0}h left",
            ["Tray_TimeLeftMinutes"] = "{0}m left",
            ["Tray_TooltipCount"] = "SmartNotes - {0} sticky note{1} on desktop",

            // Balloon Notifications
            ["Balloon_NotesArranged"] = "Sticky notes arranged neatly on your desktop!",
            ["Balloon_NoteRestored"] = "Restored note \"{0}\" to desktop!",
            ["Balloon_NotesRestoredCount"] = "Restored {0} note{1} to desktop!",
            ["Balloon_TrashEmptied"] = "Trash folder emptied.",
            ["Balloon_BroughtToFront"] = "Sticky notes active and brought to front!",

            // Updater Messages
            ["Update_DialogTitle"] = "SmartNotes Updater",
            ["Update_UpToDateTitle"] = "SmartNotes is Up to Date!",
            ["Update_UpToDateMsg"] = "You are running the newest release (v{0}). No new updates found at this time.",
            ["Update_AvailableTitle"] = "New Version Available!",
            ["Update_AvailableMsg"] = "• Installed Version: v{0}\n• Latest Version: v{1}\n\nWould you like to download and install this update now?",
            ["Update_ToastTitle"] = "SmartNotes Update Available",
            ["Update_ToastMsg"] = "v{0} is ready to install!",
            ["Update_ErrorNetwork"] = "Unable to contact GitHub Releases right now. Please check your internet connection.",
            ["Update_ErrorApply"] = "Failed to auto-apply update: {0}\nOpening release page in your browser...",
            ["Update_ManualRequired"] = "Manual Update Required",
            ["Update_ConnectionNotice"] = "Connection Notice",

            // ModernMessageBox Buttons
            ["MsgBox_GotIt"] = "Got It",
            ["MsgBox_OK"] = "OK",
            ["MsgBox_Cancel"] = "Cancel",
            ["MsgBox_Yes"] = "Yes",
            ["MsgBox_YesUpdate"] = "Yes, Update",
            ["MsgBox_No"] = "No"
        };

        // ---------------- GERMAN (de) ----------------
        var de = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // General App & Settings Header
            ["App_Title"] = "SmartNotes",
            ["App_VersionBadge"] = "SmartNotes V2.1",
            ["Settings_Title"] = "Einstellungen & Optionen",
            ["Settings_Subtitle"] = "Desktop-Verhalten, Notiz-Designs, Rechtschreibung und Leistung anpassen",
            ["Settings_PreferencesNav"] = "EINSTELLUNGEN",
            ["Settings_Engine"] = "SmartNotes Engine",
            ["Settings_EngineInfo"] = ".NET 10.0 • High-DPI Bereit",

            // Navigation
            ["Nav_General"] = "Desktop & System",
            ["Nav_Appearance"] = "Notiz-Design",
            ["Nav_Hotkeys"] = "Tastenkürzel",
            ["Nav_Proofing"] = "Rechtschreibung",
            ["Nav_Trash"] = "Papierkorb & Wiederherstellung",
            ["Nav_Performance"] = "Speicher & Leistung",

            // Footer
            ["Btn_Save"] = "Einstellungen speichern",
            ["Btn_Cancel"] = "Abbrechen",
            ["Status_SaveInstant"] = "Änderungen werden sofort wirksam",

            // General Panel (Desktop & System)
            ["General_SectionTitle"] = "Desktop & System",
            ["General_SectionDesc"] = "Steuern Sie, wie Notizen im Hintergrund laufen und mit Ihrem Desktop-Hintergrund interagieren.",
            ["General_AppLangTitle"] = "Sprache der Benutzeroberfläche",
            ["General_AppLangDesc"] = "Wählen Sie Ihre bevorzugte Anzeigesprache für die SmartNotes-Benutzeroberfläche.",
            ["General_StartupTitle"] = "Mit Windows starten",
            ["General_StartupDesc"] = "SmartNotes beim Hochfahren des Computers automatisch im Hintergrund starten und Notizen wiederherstellen.",
            ["General_DesktopStuckTitle"] = "Desktop-Pinnwand (Am Hintergrund fixiert)",
            ["General_DesktopStuckDesc"] = "Notizen fest an den Desktop heften, sodass geöffnete Fenster und Vollbild-Programme darüber liegen.",
            ["General_TransparencyTitle"] = "Transparenz im Hintergrund (Unfokussiert)",
            ["General_TransparencyDesc"] = "Inaktive Notizen automatisch dezent abdunkeln/durchscheinend machen, wenn in anderen Fenstern gearbeitet wird.",
            ["General_UnfocusedOpacityTitle"] = "Transparenz bei Inaktivität",
            ["General_UnfocusedOpacityDesc"] = "Deckkraft der Notiz, wenn sich die Maus außerhalb befindet",

            // Note Appearance Panel
            ["Appearance_SectionTitle"] = "Standard-Notizdesign",
            ["Appearance_SectionDesc"] = "Standard-Farbschemata, Schriftgrößen und Design für neue Notizen festlegen.",
            ["Appearance_DefaultColorTitle"] = "Standard-Farbschema",
            ["Appearance_DefaultColorDesc"] = "Neon-Akzentfarbe und Kartentönung für neu erstellte Notizen.",
            ["Appearance_QuickSwatches"] = "Farbauswahl (Klicken zum Auswählen)",
            ["Appearance_DefaultFontSizeTitle"] = "Standard-Schriftgröße",
            ["Appearance_DefaultFontSizeDesc"] = "Standardschriftgröße für Notiztext, Checklisten und Textbausteine.",
            ["Appearance_LivePreview"] = "LIVE-VORSCHAU",
            ["Appearance_PreviewTitle"] = "Notizen & Aufgaben",
            ["Appearance_PreviewPinned"] = "📌 Angeheftet",
            ["Appearance_PreviewBody"] = "SmartNotes ist modern, blitzschnell und bleibt genau dort, wo Sie es brauchen.",

            // Hotkeys Panel
            ["Hotkeys_SectionTitle"] = "Globale System-Tastenkürzel",
            ["Hotkeys_SectionDesc"] = "Notizen blitzschnell erstellen, verwalten oder desktopweit ein-/ausblenden.",
            ["Hotkeys_ToggleTitle"] = "Globale System-Tastenkürzel aktivieren",
            ["Hotkeys_ToggleDesc"] = "Tastenkombinationen systemweit in jeder Windows-Anwendung empfangen.",
            ["Hotkeys_ActiveShortcuts"] = "AKTIVE TASTENKÜRZEL",
            ["Hotkeys_NewNote"] = "Neue Notiz erstellen",
            ["Hotkeys_NewNoteDesc"] = "Erstellt sofort eine neue Notiz an der aktuellen Mausposition",
            ["Hotkeys_NotesHub"] = "Notizen-Übersicht / Manager öffnen",
            ["Hotkeys_NotesHubDesc"] = "Alle Notizen an einem zentralen Ort suchen, filtern und verwalten",
            ["Hotkeys_ToggleNotes"] = "Alle Desktop-Notizen ein-/ausblenden",
            ["Hotkeys_ToggleNotesDesc"] = "Schnelles Umschalten der Notizensichtbarkeit für Präsentationen",

            // Proofing Panel
            ["Proofing_SectionTitle"] = "Rechtschreibprüfung & Autokorrektur (V2.0)",
            ["Proofing_SectionDesc"] = "Intelligente Texteingabe, Live-Rechtschreibprüfung, Autokorrektur-Regeln und Sonderzeichen-Kürzel.",
            ["Proofing_SpellCheckTitle"] = "Echtzeit-Rechtschreibprüfung",
            ["Proofing_SpellCheckDesc"] = "Tippfehler rot unterstreichen mit Vorschlägen per Rechtsklick.",
            ["Proofing_DictTitle"] = "Wörterbuch-Sprache",
            ["Proofing_DictDesc"] = "Aktives Wörterbuch für die Rechtschreibprüfung.",
            ["Proofing_WindowsDictNotice"] = "Die Rechtschreibprüfung verwendet installierte Windows-Sprachpakete. Alle auf Ihrem PC installierten Sprachen funktionieren automatisch. Um weitere Sprachen hinzuzufügen: Windows-Einstellungen → Zeit und Sprache → Sprache.",
            ["Proofing_AutocorrectTitle"] = "Tippfehler automatisch korrigieren",
            ["Proofing_AutocorrectDesc"] = "Häufige Tippfehler beim Schreiben sofort beheben. Rücktaste stellt das Originalwort wieder her.",
            ["Proofing_AutoCapTitle"] = "Satzanfänge automatisch großschreiben",
            ["Proofing_AutoCapDesc"] = "Den ersten Buchstaben jedes neuen Satzes automatisch kapitalisieren.",
            ["Proofing_SmartSymbolsTitle"] = "Sonderzeichen-Kürzel (Smart Symbols)",
            ["Proofing_SmartSymbolsDesc"] = "Tastaturkürzel wie '->' zu '→', '--' zu '—', '!=' zu '≠', '(c)' zu '©' umwandeln.",
            ["Proofing_CustomDictBtn"] = "Eigenes Wörterbuch (.lex)",
            ["Proofing_CustomDictDesc"] = "Eigene Wörter hinzufügen",
            ["Proofing_CustomRulesBtn"] = "Autokorrektur-Regeln (.json)",
            ["Proofing_CustomRulesDesc"] = "Eigene Texterweiterungen definieren",
            ["Proofing_StatusSupported"] = "✓ Windows-Wörterbuch ist einsatzbereit für {0}",
            ["Proofing_StatusNotSupported"] = "ℹ️ Hinweis: Das Windows-Wörterbuch für {0} ist auf diesem PC nicht installiert. Auf jedem PC mit diesem Sprachpaket (z. B. Ihrem privaten PC) funktioniert es automatisch.",

            // Trash Panel
            ["Trash_SectionTitle"] = "Papierkorb & 48-Stunden-Wiederherstellung",
            ["Trash_SectionDesc"] = "Schutz vor versehentlichem Löschen durch automatische lokale Aufbewahrung.",
            ["Trash_RetentionTitle"] = "48-Stunden Sicherheitsaufbewahrung",
            ["Trash_RetentionDesc"] = "Gelöschte Notizen werden 48 Stunden lang lokal aufbewahrt, bevor sie endgültig entfernt werden.",
            ["Trash_CountFormat"] = "{0} Notiz{1} im Papierkorb",
            ["Trash_ActionsTitle"] = "Aktionen & Dateiverwaltung",
            ["Trash_OpenExplorerBtn"] = "Papierkorb im Explorer öffnen",
            ["Trash_EmptyBtn"] = "Papierkorb jetzt endgültig leeren",
            ["Trash_EmptiedToast"] = "Papierkorb geleert. Alle gelöschten Notizen wurden endgültig entfernt.",

            // Performance Panel
            ["Perf_SectionTitle"] = "Speicher & Systemleistung",
            ["Perf_SectionDesc"] = "Hintergrund-RAM-Optimierung, automatisches Working-Set-Trimming und Leistungseinstellungen.",
            ["Perf_AutoTrimTitle"] = "Automatisches RAM-Trimming im Hintergrund",
            ["Perf_AutoTrimDesc"] = "Bereinigt den Arbeitsspeicher und gibt inaktive Speicherseiten alle 3 Minuten im Leerlauf automatisch an Windows frei.",
            ["Perf_WorkingSetTitle"] = "Aktuelle RAM-Belegung (Working Set)",
            ["Perf_WorkingSetDesc"] = "SmartNotes gibt ungenutzte Speicherseiten direkt und aggressiv an das Windows-Betriebssystem zurück.",
            ["Perf_TrimNowBtn"] = "RAM jetzt optimieren",
            ["Perf_TrimmedStatus"] = "Working Set: {0:F1} MB ({1:F1} MB freigegeben!)",
            ["Perf_HealthyStatus"] = "Working Set RAM: {0:F1} MB (Optimal)",
            ["Perf_HardwareAccelTitle"] = "Hardware-Beschleunigung",
            ["Perf_HardwareAccelDesc"] = "DWM DirectX / WPF Pipeline Aktiv",
            ["Perf_GcTitle"] = "Speicherbereinigung (GC)",
            ["Perf_GcDesc"] = ".NET 10 Non-Intrusive LOH-Kompaktierung",
            ["Perf_ZeroRamTitle"] = "Zero-RAM Leerlauf-Architektur",
            ["Perf_ZeroRamDesc"] = "Installierte Windows-Sprachpakete, Wörterbücher und geschlossene Notizen verbleiben auf der Festplatte und belegen 0 MB RAM im Leerlauf. Nur aktiv geöffnete Notizen laden ihr jeweiliges Wörterbuch in den Speicher.",

            // Sticky Note Pin Indicators & Modes
            ["Pin_StuckToDesktop"] = "📌 Am Desktop fixiert",
            ["Pin_AlwaysOnTop"] = "📌 Immer im Vordergrund",
            ["Pin_Normal"] = "📌 Normales Fenster",
            ["Tooltip_Pin_StuckToDesktop"] = "Modus: Am Desktop fixiert (Hinter allen Fenstern). Klicken für 'Immer im Vordergrund'",
            ["Tooltip_Pin_AlwaysOnTop"] = "Modus: Immer im Vordergrund (Schwebend). Klicken für 'Am Desktop fixieren'",
            ["Tooltip_Pin_Normal"] = "Modus: Normales Fenster. Klicken für 'Am Desktop fixieren'",

            // Sticky Note Tooltips & Placeholders
            ["Tooltip_NewNote"] = "Neue Notiz erstellen",
            ["Tooltip_LockNote"] = "Notiz sperren (Bearbeitung verhindern)",
            ["Tooltip_UnlockNote"] = "Notiz entsperren",
            ["Tooltip_TextMode"] = "Zu Text-Modus wechseln",
            ["Tooltip_ActiveTextMode"] = "Aktiv: Text-Modus",
            ["Tooltip_ChecklistMode"] = "Zu Checklisten-Modus wechseln",
            ["Tooltip_ActiveChecklistMode"] = "Aktiv: Checklisten-Modus",
            ["Tooltip_CopyMode"] = "Zu Textbausteine-Modus wechseln",
            ["Tooltip_ActiveCopyMode"] = "Aktiv: Textbausteine-Modus",
            ["Tooltip_ColorPalette"] = "Farben, Design & Transparenz",
            ["Tooltip_DeleteNote"] = "In den Papierkorb verschieben (48h Aufbewahrung)",
            ["Tooltip_MoreOptions"] = "Weitere Optionen...",
            ["Tooltip_AddTask"] = "Aufgabe hinzufügen",
            ["Tooltip_DeleteTask"] = "Aufgabe löschen",
            ["Tooltip_AddSnippet"] = "Textbaustein hinzufügen",
            ["Tooltip_CopySnippet"] = "Zeile in Zwischenablage kopieren",
            ["Tooltip_Copied"] = "Kopiert! ✓",
            ["Tooltip_DeleteSnippet"] = "Eintrag löschen",
            ["Placeholder_Title"] = "Titel (optional)...",
            ["Placeholder_Content"] = "Notiz schreiben...",
            ["Placeholder_NewChecklist"] = "Neue Aufgabe (Eingabetaste)...",
            ["Placeholder_NewSnippet"] = "Neuer Textbaustein (Eingabetaste)...",

            // Context Menu & More Options Menu
            ["Menu_SpellingSuggestions"] = "VORSCHLÄGE ZUR RECHTSCHREIBUNG",
            ["Menu_NoSuggestions"] = "(Keine Korrekturvorschläge)",
            ["Menu_AddToDict"] = "\"{0}\" zum Wörterbuch hinzufügen",
            ["Menu_IgnoreAll"] = "Alle Vorkommnisse ignorieren",
            ["Menu_Cut"] = "Ausschneiden",
            ["Menu_Copy"] = "Kopieren",
            ["Menu_Paste"] = "Einfügen",
            ["Menu_SelectAll"] = "Alles auswählen",
            ["Menu_Proofing"] = "Rechtschreibung & Sprache",
            ["Menu_SpellCheck"] = "Rechtschreibprüfung",
            ["Menu_SpellCheckEnabled"] = "Rechtschreibprüfung (Aktiviert)",
            ["Menu_SpellCheckDisabled"] = "Rechtschreibprüfung (Deaktiviert)",
            ["Menu_Autocorrect"] = "Häufige Tippfehler korrigieren",
            ["Menu_AutocorrectEnabled"] = "Häufige Tippfehler korrigieren (Aktiviert)",
            ["Menu_AutocorrectDisabled"] = "Häufige Tippfehler korrigieren (Deaktiviert)",
            ["Menu_AutoCapitalize"] = "Satzanfänge großschreiben",
            ["Menu_AutoCapitalizeEnabled"] = "Satzanfänge großschreiben (Aktiviert)",
            ["Menu_AutoCapitalizeDisabled"] = "Satzanfänge großschreiben (Deaktiviert)",
            ["Menu_SmartSymbols"] = "Sonderzeichen: -> zu →",
            ["Menu_SmartSymbolsEnabled"] = "Sonderzeichen: -> zu → (Aktiviert)",
            ["Menu_SmartSymbolsDisabled"] = "Sonderzeichen (Deaktiviert)",
            ["Menu_ProofingLang"] = "Wörterbuch-Sprache ({0})",
            ["Menu_Language"] = "Sprache",
            ["Menu_FontSize"] = "Schriftgröße ({0}pt)",
            ["Menu_IncreaseFontSize"] = "Schriftgröße vergrößern (+)",
            ["Menu_DecreaseFontSize"] = "Schriftgröße verkleinern (-)",
            ["Menu_ResetFontSize"] = "Schriftgröße zurücksetzen (16pt)",
            ["Menu_OpacityHeader"] = "Deckkraft ({0}%)",
            ["Menu_Opacity100"] = "100% Vollständig sichtbar",
            ["Menu_Opacity90"] = "90% Klar",
            ["Menu_Opacity80"] = "80% Glas",
            ["Menu_Opacity65"] = "65% Durchscheinend",
            ["Menu_Opacity50"] = "50% Dezent",
            ["Menu_DimUnfocusedEnabled"] = "Bei Inaktivität abdunkeln (Aktiviert)",
            ["Menu_DimUnfocusedDisabled"] = "Bei Inaktivität abdunkeln (Deaktiviert)",
            ["Menu_CopyAllContent"] = "Gesamten Inhalt kopieren (Strg+Umschalt+C)",
            ["Menu_ExportMarkdown"] = "Als Markdown exportieren (.md)",
            ["Menu_DuplicateNote"] = "Notiz duplizieren",
            ["Menu_ClearCompletedTasks"] = "Erledigte Aufgaben entfernen",
            ["Menu_DeleteNote"] = "Notiz löschen",

            // Color Palette & Transparency Popup
            ["ColorPopup_QuickThemes"] = "Neon-Farbschemata",
            ["ColorPopup_CustomCanvas"] = "Eigene Farbauswahl",
            ["ColorPopup_Apply"] = "Anwenden",
            ["ColorPopup_TransparencyDimming"] = "Transparenz & Inaktivität",
            ["ColorPopup_NoteOpacity"] = "Notiz-Deckkraft",
            ["ColorPopup_DimUnfocused"] = "Bei Inaktivität abdunkeln",
            ["ColorPopup_UnfocusedOpacity"] = "Transparenz bei Inaktivität",

            // Theme Preset Swatch Names
            ["Theme_Amber"] = "Strahlendes Bernstein",
            ["Theme_Emerald"] = "Neon-Smaragd",
            ["Theme_Violet"] = "Cyber-Violett",
            ["Theme_Cyan"] = "Elektrisches Cyan",
            ["Theme_Rose"] = "Korallen-Rosa",
            ["Theme_Obsidian"] = "Obsidian-Blau",
            ["Theme_Gold"] = "Klassisches Notiz-Gold",
            ["Theme_Mint"] = "Frische Minze",

            // Timestamps & Status
            ["Time_JustNow"] = "Gerade eben",
            ["Time_MinutesAgo"] = "Vor {0} Min.",
            ["Time_HoursAgo"] = "Vor {0} Std.",
            ["Status_Typing"] = "Schreibt...",
            ["Status_Saved"] = "Gespeichert",
            ["Status_Locked"] = "GESPERRT",

            // Note Export
            ["Export_Filter"] = "Markdown-Datei (*.md)|*.md|Textdatei (*.txt)|*.txt",
            ["Export_Failed"] = "Notiz konnte nicht exportiert werden: {0}",
            ["Export_DialogTitle"] = "SmartNotes Export",
            ["Export_Heading"] = "Export-Hinweis",

            // Tray Menu
            ["Tray_NewNote"] = "Neue Notiz",
            ["Tray_BringAllToFront"] = "Alle Notizen in den Vordergrund",
            ["Tray_ShowAll"] = "Alle Notizen anzeigen",
            ["Tray_HideAll"] = "Alle Notizen ausblenden",
            ["Tray_Arrange"] = "Notizen auf Desktop anordnen",
            ["Tray_Trash"] = "Papierkorb (48h Wiederherstellung)",
            ["Tray_RestoreNote"] = "Wiederherstellen: \"{0}\" ({1})",
            ["Tray_RestoreAll"] = "Alle Notizen wiederherstellen",
            ["Tray_EmptyTrash"] = "Papierkorb jetzt leeren",
            ["Tray_OpenTrashFolder"] = "Papierkorb im Explorer öffnen...",
            ["Tray_NoTrashNotes"] = "(Keine kürzlich gelöschten Notizen)",
            ["Tray_Startup"] = "Mit Windows starten",
            ["Tray_Settings"] = "Einstellungen & Optionen...",
            ["Tray_OptimizeRam"] = "Arbeitsspeicher optimieren (RAM)",
            ["Tray_CheckUpdates"] = "Nach Updates suchen... (v{0})",
            ["Tray_Exit"] = "SmartNotes beenden",
            ["Tray_RamOptimizedBalloon"] = "RAM-Arbeitsspeicher auf {0:F1} MB reduziert ({1:F1} MB freigegeben)!",
            ["Tray_TimeLeftHours"] = "Noch {0} Std.",
            ["Tray_TimeLeftMinutes"] = "Noch {0} Min.",
            ["Tray_TooltipCount"] = "SmartNotes - {0} Notiz{1} auf dem Desktop",

            // Balloon Notifications
            ["Balloon_NotesArranged"] = "Notizen übersichtlich auf dem Desktop angeordnet!",
            ["Balloon_NoteRestored"] = "Notiz \"{0}\" auf dem Desktop wiederhergestellt!",
            ["Balloon_NotesRestoredCount"] = "{0} Notiz{1} auf dem Desktop wiederhergestellt!",
            ["Balloon_TrashEmptied"] = "Papierkorb wurde geleert.",
            ["Balloon_BroughtToFront"] = "Notizen aktiviert und in den Vordergrund geholt!",

            // Updater Messages
            ["Update_DialogTitle"] = "SmartNotes Updater",
            ["Update_UpToDateTitle"] = "SmartNotes ist auf dem neuesten Stand!",
            ["Update_UpToDateMsg"] = "Sie verwenden die neueste Version (v{0}). Keine neuen Updates verfügbar.",
            ["Update_AvailableTitle"] = "Neue Version verfügbar!",
            ["Update_AvailableMsg"] = "• Installierte Version: v{0}\n• Neueste Version: v{1}\n\nMöchten Sie dieses Update jetzt herunterladen und installieren?",
            ["Update_ToastTitle"] = "SmartNotes Update verfügbar",
            ["Update_ToastMsg"] = "Version v{0} steht zur Installation bereit!",
            ["Update_ErrorNetwork"] = "GitHub Releases kann derzeit nicht erreicht werden. Bitte überprüfen Sie Ihre Internetverbindung.",
            ["Update_ErrorApply"] = "Automatisches Update fehlgeschlagen: {0}\nDie Release-Seite wird im Browser geöffnet...",
            ["Update_ManualRequired"] = "Manuelles Update erforderlich",
            ["Update_ConnectionNotice"] = "Verbindungshinweis",

            // ModernMessageBox Buttons
            ["MsgBox_GotIt"] = "Verstanden",
            ["MsgBox_OK"] = "OK",
            ["MsgBox_Cancel"] = "Abbrechen",
            ["MsgBox_Yes"] = "Ja",
            ["MsgBox_YesUpdate"] = "Ja, Jetzt aktualisieren",
            ["MsgBox_No"] = "Nein"
        };

        _strings["en"] = en;
        _strings["de"] = de;
    }
}
