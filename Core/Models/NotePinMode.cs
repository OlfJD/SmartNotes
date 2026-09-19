namespace SmartNotes.Core.Models;

public enum NotePinMode
{
    DesktopStuck = 0, // Behind all apps, stuck to desktop surface (Default)
    AlwaysOnTop = 1,  // Floating above all other windows
    Normal = 2        // Standard desktop window
}
