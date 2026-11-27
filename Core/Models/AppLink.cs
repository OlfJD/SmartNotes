using System;
using System.IO;

namespace SmartNotes.Core.Models;

/// <summary>
/// App-linked notes: a note linked to a program (by its exe file name, e.g. "Code.exe") shows on top only while that
/// program is in front, and hides when another program comes to the front. Clicking into the note itself (a SmartNotes
/// window) keeps it as it is, so it can be edited.
/// </summary>
public static class AppLink
{
    /// <summary>"C:\Tools\Code.exe", "code.EXE " and "Code" all become "Code.exe"-comparable names.</summary>
    public static string Normalize(string? exe)
    {
        string name = Path.GetFileName((exe ?? "").Trim());
        if (name.Length > 0 && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name += ".exe";
        return name;
    }

    /// <summary>True when the program in front is the one the note is linked to (exe names, case-insensitive).</summary>
    public static bool Matches(string? linkedExe, string? foregroundExe)
    {
        string linked = Normalize(linkedExe);
        return linked.Length > 0 && string.Equals(linked, Normalize(foregroundExe), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a note may be on screen while <paramref name="foregroundExe"/> (another program, never SmartNotes) is in
    /// front. Notes that are not linked are always allowed.
    /// </summary>
    public static bool MayShow(NoteItem note, string? foregroundExe) =>
        !note.IsAppLinked || Matches(note.LinkedApp, foregroundExe);
}
