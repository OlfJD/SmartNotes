using System;
using System.Collections.Generic;
using System.Windows.Media;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Skins;

/// <summary>
/// A skin's per-note emblem as offered in the colour popup: Automatic (picked from the note's id) plus one tile per
/// emblem. The choice is stored in <see cref="NoteItem.Emblems"/> under the skin's key.
/// The Great Runes of the Elden Ring skin are separate: a rune also sets the note's colours (see GreatRune).
/// </summary>
public sealed class EmblemSet
{
    public required string SkinKey { get; init; }
    /// <summary>Localization key of the popup heading ("Constellation", "Creature").</summary>
    public required string HeaderKey { get; init; }
    public required IReadOnlyList<string> Keys { get; init; }
    /// <summary>(key, language) → name for the tooltip.</summary>
    public required Func<string, string, string> DisplayName { get; init; }
    /// <summary>(key or "" for Automatic, pixel size, note glow colour) → tile picture.</summary>
    public required Func<string?, int, Color, ImageSource> RenderTile { get; init; }
}

public static class Emblems
{
    private static readonly EmblemSet Constellations = new()
    {
        SkinKey = NoteSkin.AstralKey,
        HeaderKey = "ColorPopup_Constellation",
        Keys = AstralArt.ConstellationKeys,
        DisplayName = AstralArt.DisplayName,
        RenderTile = AstralArt.RenderTile,
    };

    private static readonly EmblemSet Creatures = new()
    {
        SkinKey = NoteSkin.DeepSeaKey,
        HeaderKey = "ColorPopup_Creature",
        Keys = DeepSeaArt.CreatureKeys,
        DisplayName = DeepSeaArt.DisplayName,
        RenderTile = DeepSeaArt.RenderTile,
    };

    /// <summary>The skin's emblems, or null when it has none to choose.</summary>
    public static EmblemSet? For(NoteSkin skin) => skin.Key switch
    {
        NoteSkin.AstralKey => Constellations,
        NoteSkin.DeepSeaKey => Creatures,
        _ => null
    };
}
