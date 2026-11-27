using System.Text.Json;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using SmartNotes.UI.Skins;

namespace SmartNotes.Tests;

/// <summary>The skins: names in both languages, per-note emblems, and the rules their painters follow.</summary>
public static class ThemeTests
{
    [Test]
    public static void Skins_HaveUniqueKeys_AndNamesInBothLanguages()
    {
        var keys = NoteSkin.All.Select(s => s.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "unique skin keys");
        foreach (var skin in NoteSkin.All)
        {
            foreach (var lang in new[] { "en", "de" })
            {
                LocalizationService.Instance.SetLanguage(lang);
                Assert.True(LocalizationService.T(skin.LocalizationKey) != skin.LocalizationKey, $"{skin.Key} has a {lang} name");
            }
        }
        LocalizationService.Instance.SetLanguage("en");
    }

    [Test]
    public static void Skins_ShapedBodiesStayInsideTheirContentInset()
    {
        foreach (var skin in NoteSkin.All.Where(s => s.CustomArt))
        {
            var v = skin.VisualInset;
            var c = skin.ContentInset;
            Assert.True(v.Left <= c.Left && v.Top <= c.Top && v.Right <= c.Right && v.Bottom <= c.Bottom,
                $"{skin.Key}: the content starts inside the visible body");
        }
    }

    [Test]
    public static void Emblems_TheOldConstellationField_IsStillRead()
    {
        // a note saved by 2.6: the constellation had its own field
        var old = JsonSerializer.Deserialize<NoteItem>("{\"Title\":\"Sky\",\"SkinKey\":\"Astral\",\"ConstellationKey\":\"Orion\"}")!;
        Assert.Equal("Orion", old.GetEmblem(NoteSkin.AstralKey), "carried over into the emblems");

        string saved = JsonSerializer.Serialize(old);
        Assert.False(saved.Contains("ConstellationKey"), "the old field is not written again");
        var back = JsonSerializer.Deserialize<NoteItem>(saved)!;
        Assert.Equal("Orion", back.GetEmblem(NoteSkin.AstralKey), "and survives a save");
    }

    [Test]
    public static void Emblems_ArePerSkin_AndEmptyMeansAutomatic()
    {
        var note = new NoteItem();
        note.SetEmblem(NoteSkin.DeepSeaKey, "Nautilus");
        note.SetEmblem(NoteSkin.AstralKey, "Lyra");
        Assert.Equal("Nautilus", note.GetEmblem(NoteSkin.DeepSeaKey), "Deep Sea creature");
        Assert.Equal("Lyra", note.GetEmblem(NoteSkin.AstralKey), "Astral constellation, kept separately");
        note.SetEmblem(NoteSkin.DeepSeaKey, "");
        Assert.Equal("", note.GetEmblem(NoteSkin.DeepSeaKey), "back to automatic");
        Assert.True(DeepSeaArt.CreatureKeys.Contains(DeepSeaArt.Resolve("", "7F3A")), "automatic picks one of the creatures");
        Assert.Equal(DeepSeaArt.Resolve("", "7F3A"), DeepSeaArt.Resolve(null, "7F3A"), "the same one every time for the same note");
        Assert.Equal("Anglerfish", DeepSeaArt.Resolve("anglerfish", "7F3A"), "a chosen creature wins");
    }

    [Test]
    public static void InkWashi_SealCarriesTheFirstLetterOfTheTitle()
    {
        Assert.Equal("T", InkWashiArt.SealLetter("today's priorities"), "first letter, upper case");
        Assert.Equal("Ö", InkWashiArt.SealLetter("  ölwechsel"), "after spaces, umlauts too");
        Assert.Equal("3", InkWashiArt.SealLetter("3 things"), "a digit counts");
        Assert.Equal("印", InkWashiArt.SealLetter(""), "no title: 印 (seal)");
        Assert.Equal("印", InkWashiArt.SealLetter("— …"), "no letter at all");
    }
}
