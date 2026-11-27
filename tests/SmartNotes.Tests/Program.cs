namespace SmartNotes.Tests;

public static class Program
{
    [STAThread] // WPF TextBox (autocorrect tests) and UI Automation need an STA thread
    public static int Main(string[] args)
    {
        // Must happen before any SmartNotes type is touched: AppPaths resolves the data folder once per process.
        // It also keeps SettingsService away from the Windows autostart entry.
        Directory.CreateDirectory(TestData.UnitDataDir);
        Environment.SetEnvironmentVariable("SMARTNOTES_DATA_DIR", TestData.UnitDataDir);
        // "--render <folder> [skins|features|menu]": draw real note windows to PNG files (see NoteRenderer)
        int render = Array.IndexOf(args, "--render");
        if (render >= 0 && render + 1 < args.Length)
        {
            return NoteRenderer.Run(args[render + 1], render + 2 < args.Length ? args[render + 2] : "skins");
        }
        return Runner.Run(args);
    }
}
