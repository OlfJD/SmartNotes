using System.Windows.Media;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Skins;

/// <summary>
/// Picks the painter of a custom-art skin (<see cref="NoteSkin.ArtKey"/>). Shared by the note window and the
/// Settings preview, so both show the same shape. Returns null for skins without their own art.
/// </summary>
public static class SkinArt
{
    /// <param name="tag">Short id shown by some skins (Cyberpunk label tab, Blueprint drawing number) and used as the seed for stars, fibres and bubbles.</param>
    /// <param name="bottomReserve">Height at the bottom the emblem (rune, constellation, creature, seal) must stay clear of.</param>
    /// <param name="note">The note, for its rune, emblem, title and date; null in the Settings preview.</param>
    public static Drawing? Build(NoteSkin skin, double w, double h, NoteColorTheme theme, string tag, double pixelsPerDip,
        double bottomReserve, NoteItem? note = null, string? title = null, System.Windows.Rect? titleBand = null)
    {
        if (!skin.CustomArt || w < 120 || h < 120) return null;
        title ??= note?.Title ?? "";
        return skin.ArtKey switch
        {
            "Cyberpunk" => CyberpunkArt.Build(w, h, theme, skin, tag, pixelsPerDip),
            "Scroll" => EldenScrollArt.Build(w, h, theme, tag),
            "Paper" => PaperStickyArt.Build(w, h, theme),
            "Elden" => EldenRingArt.Build(w, h, theme, tag, note?.RuneKey ?? GreatRune.DefaultKey, pixelsPerDip, bottomReserve),
            "Astral" => AstralArt.Build(w, h, theme, tag, bottomReserve, note?.GetEmblem(NoteSkin.AstralKey) ?? ""),
            "Terminal" => TerminalArt.Build(w, h, theme, skin, tag, pixelsPerDip),
            "Blueprint" => BlueprintArt.Build(w, h, theme, skin, tag, pixelsPerDip, titleBand, note?.CreatedAt),
            "InkWashi" => InkWashiArt.Build(w, h, theme, tag, pixelsPerDip, note == null ? "Quick Ideas" : title, titleBand),
            "DeepSea" => DeepSeaArt.Build(w, h, theme, tag, bottomReserve, note?.GetEmblem(NoteSkin.DeepSeaKey)),
            _ => null
        };
    }
}
