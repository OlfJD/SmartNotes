using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SmartNotes.Core.Models;

/// <summary>
/// The Great Runes offered by the Elden Ring skin in place of note colours. Each rune sets the
/// rune drawn on the note (UI/Skins/RuneArt.cs) and the accent colours of the note.
/// The runes are our own drawings of the in-game shapes; no game images are used.
/// Reference: docs/references/great-runes-board.html.
/// </summary>
public sealed class GreatRune
{
    public const string DefaultKey = "Morgott";

    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string NameDe { get; init; } = "";

    /// <summary>Main ember colour; also the note's title / accent colour.</summary>
    public string ColHex { get; init; } = "#F4A23A";

    public static readonly IReadOnlyList<GreatRune> All = new List<GreatRune>
    {
        new() { Key = "Godrick", Name = "Godrick's Great Rune", NameDe = "Godricks Große Rune", ColHex = "#E9D356" },
        new() { Key = "Radahn", Name = "Radahn's Great Rune", NameDe = "Radahns Große Rune", ColHex = "#F2683A" },
        new() { Key = "Rykard", Name = "Rykard's Great Rune", NameDe = "Rykards Große Rune", ColHex = "#F0923A" },
        new() { Key = "Morgott", Name = "Morgott's Great Rune", NameDe = "Morgotts Große Rune", ColHex = "#F4A23A" },
        new() { Key = "Mohg", Name = "Mohg's Great Rune", NameDe = "Mohgs Große Rune", ColHex = "#E83246" },
        new() { Key = "Malenia", Name = "Malenia's Great Rune", NameDe = "Malenias Große Rune", ColHex = "#F39C86" },
        new() { Key = "Unborn", Name = "Great Rune of the Unborn", NameDe = "Große Rune der Ungeborenen", ColHex = "#EFB53E" },
        new() { Key = "Miquella", Name = "Miquella's Great Rune", NameDe = "Miquellas Große Rune", ColHex = "#ECC8C0" },
        new() { Key = "Elden", Name = "Elden Ring", NameDe = "Elden Ring", ColHex = "#E8902C" }
    };

    public static GreatRune Get(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            foreach (var rune in All)
            {
                if (string.Equals(rune.Key, key, StringComparison.OrdinalIgnoreCase)) return rune;
            }
        }
        return Get(DefaultKey);
    }

    public string DisplayName(string language) => language.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? NameDe : Name;

    private static readonly ConcurrentDictionary<string, NoteColorTheme> ThemeCache = new();

    /// <summary>Note colours for the Elden Ring skin with this rune: dark olive panel, accents in the rune's colour.</summary>
    public NoteColorTheme Theme => ThemeCache.GetOrAdd(Key, _ => new NoteColorTheme
    {
        Key = "Rune:" + Key,
        Name = Name,
        PrimaryHex = ColHex,
        GlowHex = ColHex,
        AccentHex = ColHex,
        BgHex = "#1B1C15",
        HeaderBgHex = "#2A2B21",
        BorderHex = NoteSkin.Mix("#8E8665", ColHex, 0.22),   // frame
        TextPrimaryHex = "#DDD6C0",
        TextMutedHex = "#9A927A"
    });
}
