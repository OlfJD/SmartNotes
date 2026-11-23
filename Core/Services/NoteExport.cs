using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SmartNotes.Core.Services;

public static class NoteExport
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// A file name Windows accepts for a note title ("To-do: today" made the save dialog fail): characters that are
    /// not allowed in file names become '-', trailing dots and spaces go, device names like CON get a suffix and
    /// long titles are cut to 100 characters.
    /// </summary>
    public static string SuggestFileName(string? title, string extension = ".md")
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (char c in (title ?? "").Trim())
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '-' : c);
        }

        string name = sb.ToString().Trim().TrimEnd('.', ' ');
        if (name.Length > 100) name = name.Substring(0, 100).TrimEnd('.', ' ');
        if (name.Length == 0 || name.All(c => c == '-')) name = "SmartNote";
        if (ReservedNames.Contains(name.Split('.')[0])) name += "-note";
        return name + extension;
    }
}
