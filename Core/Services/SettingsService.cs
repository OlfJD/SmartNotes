using System;
using System.IO;
using System.Text.Json;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

public class SettingsService
{
    private static readonly string AppFolder = AppPaths.DataFolder;
    private static readonly string SettingsPath = Path.Combine(AppFolder, "settings.json");

    public AppSettings Settings { get; private set; } = new();
    private bool _loadFailed;

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = Repair(loaded);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Loading settings", ex);
            _loadFailed = true; // the defaults are in use; copy the real file aside before the first save replaces it
        }

        // Self-heal and sync startup setting with current binary location
        SyncAutoStart();
    }

    /// <summary>
    /// A hand-edited or damaged settings.json can hold nulls where the app expects values;
    /// e.g. null font maps made startup fail half-way and left an invisible process behind.
    /// </summary>
    private static AppSettings Repair(AppSettings s)
    {
        var d = new AppSettings();
        s.SkinTitleFonts ??= new();
        s.SkinTextFonts ??= new();
        s.DefaultColorKey ??= d.DefaultColorKey;
        s.DefaultSkinKey ??= d.DefaultSkinKey;
        s.AppLanguage ??= d.AppLanguage;
        s.ProofingLanguage ??= d.ProofingLanguage;
        if (!Enum.IsDefined(s.DefaultPinMode)) s.DefaultPinMode = d.DefaultPinMode;
        if (double.IsNaN(s.UnfocusedOpacity) || s.UnfocusedOpacity <= 0) s.UnfocusedOpacity = d.UnfocusedOpacity;
        if (double.IsNaN(s.DefaultOpacity) || s.DefaultOpacity <= 0) s.DefaultOpacity = d.DefaultOpacity;
        if (double.IsNaN(s.DefaultFontSize) || s.DefaultFontSize < 8) s.DefaultFontSize = d.DefaultFontSize;
        return s;
    }

    /// <summary>
    /// Points the Windows autostart entry at this exe - but only for the real app: never from a copy running
    /// with its own data folder (tests, screenshots) and never from another host process that loads SmartNotes.dll.
    /// </summary>
    private void SyncAutoStart()
    {
        if (!MayChangeAutoStart(Environment.ProcessPath, AppPaths.IsOverridden)) return;
        AutoStartManager.SyncStartupRegistration(Settings.LaunchOnStartup);
    }

    /// <summary>
    /// Only SmartNotes.exe itself, running on the real data folder, may write the autostart entry. A test program
    /// built before this rule (2.3.1) once registered itself as "SmartNotes" on a development PC.
    /// </summary>
    public static bool MayChangeAutoStart(string? processPath, bool dataFolderOverridden) =>
        !dataFolderOverridden &&
        string.Equals(Path.GetFileName(processPath ?? ""), "SmartNotes.exe", StringComparison.OrdinalIgnoreCase);

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            if (_loadFailed)
            {
                DataBackupService.Snapshot("unreadable-settings");
                _loadFailed = false;
            }
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            AtomicFile.WriteAllText(SettingsPath, json);
            SyncAutoStart();
        }
        catch (Exception ex)
        {
            AppLog.Error("Saving settings", ex);
        }
    }
}
