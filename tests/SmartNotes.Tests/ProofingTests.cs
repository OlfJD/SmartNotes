using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using TextBox = System.Windows.Controls.TextBox;

namespace SmartNotes.Tests;

/// <summary>Autocorrect on a real WPF TextBox (runs on the STA main thread).</summary>
public static class ProofingTests
{
    private static AppSettings Settings(string language) => new()
    {
        ProofingLanguage = language,
        EnableAutocorrect = true,
        AutoCapitalizeSentences = true,
        SmartSymbolReplacements = true,
        CustomReplacementsEnabled = true,
    };

    /// <summary>Types <paramref name="text"/> (caret at the end) and lets autocorrect react to a space.</summary>
    private static (TextBox Box, bool Changed, AutocorrectUndoItem? Undo) TypeThenSpace(string text, AppSettings settings, bool readOnly = false)
    {
        var tb = new TextBox { Text = text, IsReadOnly = readOnly };
        tb.SelectionStart = text.Length;
        bool changed = TextProofingService.Instance.TryAutocorrect(tb, ' ', settings, out var undo);
        return (tb, changed, undo);
    }

    /// <summary>Text boxes only spell-check (and record undo steps) inside a window: host one off-screen.</summary>
    private static System.Windows.Window HostOffscreen(TextBox tb)
    {
        var host = new System.Windows.Window
        {
            Content = tb, Width = 300, Height = 80, Left = -30000, Top = -30000,
            WindowStyle = System.Windows.WindowStyle.None, ShowInTaskbar = false, ShowActivated = false
        };
        host.Show();
        return host;
    }

    [Test]
    public static void English_FixesCommonTypos()
    {
        var (tb, changed, _) = TypeThenSpace("I saw teh", Settings("en-US"));
        Assert.True(changed, "corrected");
        Assert.Equal("I saw the", tb.Text, "text");
    }

    [Test]
    public static void German_IsNotRewrittenWithEnglishCorrections()
    {
        // "im" (= "in the") is one of the most common German words; the English list turns it into "I'm"
        var (tb, _, _) = TypeThenSpace("Ich bin im", Settings("de-DE"));
        Assert.Equal("Ich bin im", tb.Text, "German text after autocorrect");

        var (tb2, _, _) = TypeThenSpace("Das ist ill", Settings("de-DE"));
        Assert.Equal("Das ist ill", tb2.Text, "German text after autocorrect");
    }

    [Test]
    public static void German_FixesCommonGermanTypos()
    {
        Assert.Equal("Das ist nicht", TypeThenSpace("Das ist nciht", Settings("de-DE")).Box.Text, "nciht");
        Assert.Equal("Ich bin der Standard", TypeThenSpace("Ich bin der standart", Settings("de-DE")).Box.Text, "nouns come back capitalised");
        Assert.Equal("Das geht gar nicht", TypeThenSpace("Das geht garnicht", Settings("de-DE")).Box.Text, "two words");
        Assert.Equal("Und", TypeThenSpace("Udn", Settings("de-DE")).Box.Text, "a capitalised typo keeps its capital");
    }

    [Test]
    public static void GermanList_IsNotUsedForEnglishText()
    {
        Assert.Equal("This is nciht", TypeThenSpace("This is nciht", Settings("en-US")).Box.Text, "German fixes stay out of English text");
    }

    [Test]
    public static void ReadOnlyText_IsNeverChanged()
    {
        // A locked note's text boxes are read-only; autocorrect used to rewrite them anyway
        var (tb, changed, _) = TypeThenSpace("I saw teh", Settings("en-US"), readOnly: true);
        Assert.False(changed, "no correction on a read-only box");
        Assert.Equal("I saw teh", tb.Text, "text");
    }

    [Test]
    public static void SmartSymbols_WorkInAnyLanguage()
    {
        var (tb, _, _) = TypeThenSpace("a ->", Settings("de-DE"));
        Assert.Equal("a →", tb.Text, "arrow");
    }

    [Test]
    public static void CustomRules_AreApplied_WithMatchingCase()
    {
        TextProofingService.Instance.SaveCustomRules(new List<AutocorrectRule> { new("brb", "be right back") });
        try
        {
            var (tb, _, _) = TypeThenSpace("Brb", Settings("de-DE"));
            Assert.Equal("Be right back", tb.Text, "custom rule with capitalised shortcut");
        }
        finally
        {
            TextProofingService.Instance.SaveCustomRules(new List<AutocorrectRule>());
        }
    }

    [Test]
    public static void Backspace_UndoesTheLastCorrection()
    {
        var (tb, changed, undo) = TypeThenSpace("teh", Settings("en-US"));
        Assert.True(changed, "corrected first");
        Assert.True(TextProofingService.TryUndoAutocorrect(tb, undo), "undo applied");
        Assert.Equal("teh", tb.Text, "original word back");
    }

    [Test]
    public static void SentenceStart_IsCapitalised()
    {
        var (tb, _, _) = TypeThenSpace("hello", Settings("en-US"));
        Assert.Equal("Hello", tb.Text, "first word of the note");
        var (tb2, _, _) = TypeThenSpace("Done. next", Settings("en-US"));
        Assert.Equal("Done. Next", tb2.Text, "word after a full stop");
    }

    [Test]
    public static void Correction_KeepsTheTextBoxUndoHistory()
    {
        // Replacing the whole Text wipes Ctrl+Z; only the corrected word should change.
        // A text box only records undo steps once it is part of a window, so host it off-screen.
        var tb = new TextBox { Text = "first" };
        var host = HostOffscreen(tb);
        try
        {
            tb.SelectionStart = 5;
            tb.SelectedText = " words"; // an edit the user could undo
            tb.SelectionStart = tb.Text.Length;
            tb.SelectedText = " teh";
            tb.SelectionStart = tb.Text.Length;
            Assert.True(tb.CanUndo, "precondition: typed edits are undoable");
            TextProofingService.Instance.TryAutocorrect(tb, ' ', Settings("en-US"), out _);
            Assert.Equal("first words the", tb.Text, "corrected");
            Assert.True(tb.CanUndo, "the text box can still undo after a correction");
            tb.Undo();
            Assert.Equal("first words teh", tb.Text, "Ctrl+Z restores the word before the correction");
        }
        finally
        {
            host.Close();
        }
    }
}
