using System;
using System.IO;
using System.Text.Json;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

public class SettingsService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartNotes"
    );
    private static readonly string SettingsPath = Path.Combine(AppFolder, "settings.json");

    public AppSettings Settings { get; private set; } = new();

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
        }

        // Sync startup setting with actual registry status
        Settings.LaunchOnStartup = AutoStartManager.IsStartupEnabled();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            AutoStartManager.SetStartup(Settings.LaunchOnStartup);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
