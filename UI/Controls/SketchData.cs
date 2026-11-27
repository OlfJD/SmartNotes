using System;
using System.IO;
using System.Windows.Ink;

namespace SmartNotes.UI.Controls;

/// <summary>Sketch strokes ↔ the text stored in NoteItem.Sketch (Ink Serialized Format, compressed, base64).</summary>
public static class SketchData
{
    public static string ToBase64(StrokeCollection strokes)
    {
        if (strokes.Count == 0) return "";
        using var stream = new MemoryStream();
        strokes.Save(stream, compress: true);
        return Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>The strokes, or an empty collection for "" or data that cannot be read (logged, never thrown).</summary>
    public static StrokeCollection FromBase64(string? data)
    {
        if (string.IsNullOrEmpty(data)) return new StrokeCollection();
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(data));
            return new StrokeCollection(stream);
        }
        catch (Exception ex)
        {
            Core.Services.AppLog.Error("Reading a sketch", ex);
            return new StrokeCollection();
        }
    }
}
