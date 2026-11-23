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
        return Runner.Run(args);
    }
}
