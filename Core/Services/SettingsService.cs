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
                    Settings = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            _loadFailed = true; // the defaults are in use; copy the real file aside before the first save replaces it
        }

        // Self-heal and sync startup setting with current binary location
        SyncAutoStart();
    }

    /// <summary>
    /// Points the Windows autostart entry at this exe - but only for the real app: never from a copy running
    /// with its own data folder (tests, screenshots) and never from another host process that loads SmartNotes.dll.
    /// </summary>
    private void SyncAutoStart()
    {
        if (AppPaths.IsOverridden) return;
        string exe = Path.GetFileName(Environment.ProcessPath ?? "");
        if (!exe.Equals("SmartNotes.exe", StringComparison.OrdinalIgnoreCase)) return;
        AutoStartManager.SyncStartupRegistration(Settings.LaunchOnStartup);
    }

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
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
