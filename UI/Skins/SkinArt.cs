using System.Windows.Media;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Picks the painter of a custom-art skin (<see cref="NoteSkin.ArtKey"/>). Shared by the note window and the
/// Settings preview, so both show the same shape. Returns null for skins without their own art.
/// </summary>
public static class SkinArt
{
    /// <param name="tag">Short id shown by some skins (Cyberpunk label tab) and used to seed star fields.</param>
    /// <param name="bottomReserve">Height at the bottom the emblem (rune, constellation) must stay clear of.</param>
    public static Drawing? Build(NoteSkin skin, double w, double h, NoteColorTheme theme, string tag, double pixelsPerDip,
        double bottomReserve, string? runeKey = null, string? constellationKey = null)
    {
        if (!skin.CustomArt || w < 120 || h < 120) return null;
        return skin.ArtKey switch
        {
            "Cyberpunk" => CyberpunkArt.Build(w, h, theme, skin, tag, pixelsPerDip),
            "Scroll" => EldenScrollArt.Build(w, h, theme, tag),
            "Paper" => PaperStickyArt.Build(w, h, theme),
            "Elden" => EldenRingArt.Build(w, h, theme, tag, runeKey ?? GreatRune.DefaultKey, pixelsPerDip, bottomReserve),
            "Astral" => AstralArt.Build(w, h, theme, tag, bottomReserve, constellationKey ?? ""),
            _ => null
        };
    }
}
