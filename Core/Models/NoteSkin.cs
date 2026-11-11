using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace SmartNotes.Core.Models;

/// <summary>
/// A note "skin" restyles a sticky note: its silhouette, surface, typography and chrome colours.
/// All skins share the single StickyNoteWindow layout (no template swapping), so every feature
/// keeps working. Skins with <see cref="CustomArt"/> paint their own non-rectangular body
/// (see UI/Skins/*Art.cs) and the regular card border is switched off.
/// The note's own colour (<see cref="NoteColorTheme"/>) is re-tinted by the skin, never replaced.
/// </summary>
public class NoteSkin
{
    public const string ClassicKey = "Classic";
    public const string CyberpunkKey = "Cyberpunk";
    public const string EldenRingKey = "EldenRing";

    public const string DefaultFontStack = "Segoe UI, Segoe UI Variable, sans-serif";
    private static readonly Uri FontBaseUri = new("pack://application:,,,/");

    public string Key { get; init; } = ClassicKey;
    public string LocalizationKey { get; init; } = "Skin_Classic";

    /// <summary>True when the skin paints its own body shape instead of the rounded card.</summary>
    public bool CustomArt { get; init; } = false;

    /// <summary>Margin between the window edge and the note content (custom-art skins only).</summary>
    public Thickness ContentInset { get; init; } = new Thickness(0);

    /// <summary>Extra empty space above the header buttons (custom-art skins only).</summary>
    public double HeaderTopPad { get; init; } = 0;

    // Classic card shape (ignored by custom-art skins)
    public double CardRadius { get; init; } = 16;
    public double CardBorder { get; init; } = 5;
    public double HeaderRadius { get; init; } = 12;
    public double HeaderDivider { get; init; } = 3;

    // Inner panels
    /// <summary>Cut-corner size of header cells / rows (0 = regular rounded border). See CutBorder.</summary>
    public double CompartmentCut { get; init; } = 0;
    public double ItemCut { get; init; } = 0;
    public double CompartmentRadius { get; init; } = 8;
    public double CompartmentBorder { get; init; } = 2;
    public double ItemRadius { get; init; } = 8;
    public double ItemBorder { get; init; } = 2;
    public double InputRadius { get; init; } = 9;
    public double InputBorder { get; init; } = 2.5;

    // Typography ("./Assets/Fonts/#Name" = font bundled with the app)
    public string FontStack { get; init; } = DefaultFontStack;
    public string TitleFontStack { get; init; } = DefaultFontStack;
    public string MetaFontStack { get; init; } = DefaultFontStack;
    public FontWeight BodyWeight { get; init; } = FontWeights.Normal;
    public FontWeight TitleWeight { get; init; } = FontWeights.Bold;
    public bool AccentTitle { get; init; } = false;

    /// <summary>Title size relative to the note font size (display faces often run small).</summary>
    public double TitleScale { get; init; } = 1.0;

    /// <summary>Icon pack used on the note surface ("" = Lucide). See UI/Icons/SkinIcons.xaml.</summary>
    public string IconPack { get; init; } = "";

    /// <summary>When false the inner panels keep the classic slate-blue look.</summary>
    public bool TintPanels { get; init; } = false;

    // Chrome ink: icon / helper-text colours that sit directly on the note surface.
    public string MutedHex { get; init; } = "#A8B5CD";
    public string FaintHex { get; init; } = "#64748B";
    public string PlaceholderHex { get; init; } = "#7888A4";
    public string DangerHex { get; init; } = "#EF4444";
    public string AmberHex { get; init; } = "#F59E0B";
    public string EmeraldHex { get; init; } = "#10B981";
    public string CyanHex { get; init; } = "#06B6D4";

    /// <summary>Panel fill / outline as ARGB hex; null = derive from the tinted note colour.</summary>
    public string? PanelBgHex { get; init; }
    public string? PanelBorderHex { get; init; }

    private WpfFontFamily? _font, _titleFont, _metaFont;
    public WpfFontFamily Font => _font ??= new WpfFontFamily(FontBaseUri, FontStack);
    public WpfFontFamily TitleFont => _titleFont ??= new WpfFontFamily(FontBaseUri, TitleFontStack);
    public WpfFontFamily MetaFont => _metaFont ??= new WpfFontFamily(FontBaseUri, MetaFontStack);

    private Func<NoteColorTheme, NoteColorTheme>? Recolor { get; init; }
    private readonly ConcurrentDictionary<string, NoteColorTheme> _recolorCache = new();

    /// <summary>Returns the note colour as this skin renders it.</summary>
    public NoteColorTheme Tint(NoteColorTheme baseTheme)
    {
        if (Recolor == null) return baseTheme;
        return _recolorCache.GetOrAdd(baseTheme.Key, _ => Recolor(baseTheme));
    }

    public static readonly IReadOnlyList<NoteSkin> All = new List<NoteSkin>
    {
        new NoteSkin { Key = ClassicKey, LocalizationKey = "Skin_Classic" },

        // Neon HUD panel: stepped top edge with a solid label tab, cut corners, detached hazard
        // pieces, grid + scanlines. Painted by CyberpunkArt.
        new NoteSkin
        {
            Key = CyberpunkKey,
            LocalizationKey = "Skin_Cyberpunk",
            CustomArt = true,
            ContentInset = new Thickness(7, 5, 9, 6),
            HeaderTopPad = 14,
            CompartmentRadius = 0,
            CompartmentBorder = 1,
            ItemRadius = 0,
            ItemBorder = 1,
            InputRadius = 0,
            InputBorder = 1,
            FontStack = "./Assets/Fonts/#Chakra Petch, Bahnschrift, Segoe UI",
            TitleFontStack = "./Assets/Fonts/#Russo One, ./Assets/Fonts/#Chakra Petch, Bahnschrift, Segoe UI",
            IconPack = "Cyber",
            CompartmentCut = 7,
            ItemCut = 8,
            MetaFontStack = "./Assets/Fonts/#Share Tech Mono, Consolas, Courier New",
            BodyWeight = FontWeights.Medium,
            TitleWeight = FontWeights.Normal,
            TitleScale = 1.08,
            AccentTitle = true,
            TintPanels = true,
            MutedHex = "#8E99AD",
            FaintHex = "#5F6B80",
            PlaceholderHex = "#566176",
            DangerHex = "#FF3B5C",
            Recolor = t => new NoteColorTheme
            {
                Key = t.Key,
                Name = t.Name,
                PrimaryHex = t.PrimaryHex,
                GlowHex = t.GlowHex,
                AccentHex = t.AccentHex,
                BgHex = Mix("#06070A", t.PrimaryHex, 0.05),
                HeaderBgHex = Mix("#0A0B0F", t.PrimaryHex, 0.14),
                BorderHex = t.GlowHex,
                TextPrimaryHex = Mix("#FFFFFF", t.GlowHex, 0.22),
                TextMutedHex = Mix("#8A93A6", t.GlowHex, 0.35)
            }
        },

        // Aged parchment scroll with rolled rods, torn edges, dark ink and a wax seal in the
        // note's colour. Painted by EldenScrollArt.
        new NoteSkin
        {
            Key = EldenRingKey,
            LocalizationKey = "Skin_EldenRing",
            CustomArt = true,
            ContentInset = new Thickness(26, 25, 26, 27),
            HeaderTopPad = 0,
            CompartmentRadius = 3,
            CompartmentBorder = 0,
            ItemRadius = 3,
            ItemBorder = 1,
            InputRadius = 3,
            InputBorder = 1,
            FontStack = "./Assets/Fonts/#IM FELL English, Palatino Linotype, Georgia, serif",
            TitleFontStack = "./Assets/Fonts/#Pirata One, Palatino Linotype, Georgia, serif",
            MetaFontStack = "./Assets/Fonts/#IM FELL English, Palatino Linotype, Georgia, serif",
            IconPack = "Elden",
            BodyWeight = FontWeights.Normal,
            TitleWeight = FontWeights.Normal,
            TitleScale = 1.45,
            AccentTitle = true,
            TintPanels = true,
            MutedHex = "#5B4528",
            FaintHex = "#7A6442",
            PlaceholderHex = "#8A7350",
            DangerHex = "#9B1C1C",
            AmberHex = "#9A5A00",
            EmeraldHex = "#1F6B45",
            CyanHex = "#1E5F78",
            PanelBgHex = "#38FFF6DC",
            PanelBorderHex = "#663A2A14",
            Recolor = t =>
            {
                string deep = Mix(t.PrimaryHex, "#2A1608", 0.58);
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = Mix(t.PrimaryHex, "#5A1A10", 0.35),   // wax seal
                    GlowHex = deep,                                     // title / caret / ornaments
                    AccentHex = deep,
                    BgHex = Mix("#E9D7A8", t.PrimaryHex, 0.05),         // parchment
                    HeaderBgHex = Mix("#DCC690", t.PrimaryHex, 0.05),
                    BorderHex = "#4A3620",                              // ink
                    TextPrimaryHex = "#2A1B0C",
                    TextMutedHex = "#6B5638"
                };
            }
        }
    };

    public static NoteSkin Get(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            foreach (var skin in All)
            {
                if (string.Equals(skin.Key, key, StringComparison.OrdinalIgnoreCase)) return skin;
            }
        }
        return All[0];
    }

    /// <summary>Linear blend of two hex colours; amount 0 = a, 1 = b.</summary>
    public static string Mix(string a, string b, double amount)
    {
        try
        {
            var ca = (Color)ColorConverter.ConvertFromString(a);
            var cb = (Color)ColorConverter.ConvertFromString(b);
            byte r = (byte)Math.Round(ca.R + (cb.R - ca.R) * amount);
            byte g = (byte)Math.Round(ca.G + (cb.G - ca.G) * amount);
            byte bl = (byte)Math.Round(ca.B + (cb.B - ca.B) * amount);
            return $"#{r:X2}{g:X2}{bl:X2}";
        }
        catch
        {
            return a;
        }
    }

    public static Color WithAlpha(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Round(255 * Math.Clamp(alpha, 0, 1)), c.R, c.G, c.B);
}
