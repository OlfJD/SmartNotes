using System.Windows.Ink;
using System.Windows.Input;
using SmartNotes.Core.Models;
using SmartNotes.UI.Controls;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>Sketch mode: strokes saved with the note, kept in Earlier Versions, cleared undoably.</summary>
public static class SketchTests
{
    /// <summary>A few strokes like a quick doodle: a wave and a tick.</summary>
    public static StrokeCollection Doodle()
    {
        var wave = new StylusPointCollection();
        for (int i = 0; i <= 40; i++) wave.Add(new StylusPoint(20 + i * 5, 60 + Math.Sin(i / 4.0) * 18));
        var tick = new StylusPointCollection(new[] { new StylusPoint(60, 120), new StylusPoint(80, 140), new StylusPoint(130, 95) });
        return new StrokeCollection { new Stroke(wave), new Stroke(tick) };
    }

    [Test]
    public static void Sketch_StrokesSurviveTheRoundTrip()
    {
        string data = SketchData.ToBase64(Doodle());
        Assert.True(data.Length > 20, "something is stored");
        var back = SketchData.FromBase64(data);
        Assert.Equal(2, back.Count, "both strokes");
        Assert.Equal(41, back[0].StylusPoints.Count, "every point of the wave");
        Assert.Equal("", SketchData.ToBase64(new StrokeCollection()), "nothing drawn: nothing stored");
    }

    [Test]
    public static void Sketch_UnreadableData_GivesAnEmptyDrawing()
    {
        Assert.Equal(0, SketchData.FromBase64("this is not ink").Count, "no exception, just empty");
        Assert.Equal(0, SketchData.FromBase64(null).Count, "null");
    }

    [Test]
    public static void Sketch_CountsForEarlierVersions()
    {
        var a = new NoteVersion { Title = "Plan", ViewMode = NoteViewMode.Sketch, Sketch = SketchData.ToBase64(Doodle()) };
        var b = new NoteVersion { Title = "Plan", ViewMode = NoteViewMode.Sketch, Sketch = "" };
        Assert.False(a.SameContentAs(b), "a different drawing is a different version");
        Assert.True(a.SameContentAs(new NoteVersion { Title = "Plan", ViewMode = NoteViewMode.Sketch, Sketch = a.Sketch }), "the same drawing is not");
    }

    // ---------------------------------------------------------------- end-to-end

    [Test(E2E = true)]
    public static void Sketch_IsShownAfterRestart_AndClearKeepsAnEarlierVersion()
    {
        var note = new NoteItem { Title = "Doodle", ViewMode = NoteViewMode.Sketch, Sketch = SketchData.ToBase64(Doodle()), X = 140, Y = 160, Width = 340, Height = 320 };
        using var app = Start(nameof(Sketch_IsShownAfterRestart_AndClearKeepsAnEarlierVersion), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];
        Assert.NotNull(TryFind(w, "SketchCanvas"), "the drawing area is showing");

        Invoke(Find(w, "BtnSketchClear"));
        Assert.Eventually(() => app.ReadNotes().Single().Sketch == "", "the drawing is cleared and saved");
        Assert.Eventually(() => app.ReadHistory(note.Id).FirstOrDefault()?.Sketch == note.Sketch, "the drawing is kept as an earlier version");

        // switching modes is saved too
        Invoke(Find(w, "BtnTextMode"));
        Assert.Eventually(() => app.ReadNotes().Single().ViewMode == NoteViewMode.Text, "text mode");
        Invoke(Find(w, "BtnSketchMode"));
        Assert.Eventually(() => app.ReadNotes().Single().ViewMode == NoteViewMode.Sketch, "back to sketch mode");
    }
}
