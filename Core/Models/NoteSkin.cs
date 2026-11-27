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
public sealed class SkinFontOption
{
    private static readonly Uri FontBaseUri = new("pack://application:,,,/");
    private WpfFontFamily? _family;

    /// <param name="key">Stable key stored in the settings.</param>
    /// <param name="family">Family name inside the bundled font file (Assets/Fonts).</param>
    /// <param name="scale">Size correction so faces with small or large letters look the same size.</param>
    public SkinFontOption(string key, string family, FontWeight weight, double scale, string? displayName = null)
    {
        Key = key;
        FamilyName = family;
        Weight = weight;
        Scale = scale;
        DisplayName = displayName ?? family;
    }

    public string Key { get; }
    public string FamilyName { get; }
    public string DisplayName { get; }
    public FontWeight Weight { get; }
    public double Scale { get; }
    public WpfFontFamily Family => _family ??= new WpfFontFamily(FontBaseUri, $"./Assets/Fonts/#{FamilyName}, Segoe UI");
}

public class NoteSkin
{
    public const string ClassicKey = "Classic";
    public const string CyberpunkKey = "Cyberpunk";
    public const string EldenRingKey = "EldenRing";
    public const string MedievalKey = "Medieval";
    public const string AstralKey = "Astral";
    public const string PaperKey = "Paper";
    public const string TerminalKey = "Terminal";
    public const string BlueprintKey = "Blueprint";
    public const string InkWashiKey = "InkWashi";
    public const string DeepSeaKey = "DeepSea";

    /// <summary>Checklist ticks drawn as "[ ]" / "[x]" text instead of boxes (Terminal).</summary>
    public bool TextCheckboxes { get; init; } = false;

    /// <summary>A block cursor in the footer that blinks while the note has focus (Terminal).</summary>
    public bool BlockCursor { get; init; } = false;

    /// <summary>Icon and helper-text ink follows the note colour instead of the fixed *Hex values (Terminal phosphor).</summary>
    public bool InkFromTheme { get; init; } = false;

    /// <summary>The painter shows the note's title (the Ink &amp; Washi seal carries its first letter): repaint when it changes.</summary>
    public bool ArtShowsTitle { get; init; } = false;

    /// <summary>Room kept free at the right end of the title row for something the painter puts there (Blueprint drawing number, Ink &amp; Washi seal).</summary>
    public double TitleReserveRight { get; init; } = 0;

    public const string DefaultFontStack = "Segoe UI, Segoe UI Variable, sans-serif";
    private static readonly Uri FontBaseUri = new("pack://application:,,,/");

    public string Key { get; init; } = ClassicKey;
    public string LocalizationKey { get; init; } = "Skin_Classic";

    /// <summary>True when the skin paints its own body shape instead of the rounded card.</summary>
    public bool CustomArt { get; init; } = false;

    /// <summary>Which painter draws the body of a custom-art skin (see StickyNoteWindow.UpdateSkinGeometry).</summary>
    public string ArtKey { get; init; } = "";

    /// <summary>The colour picker offers the Great Runes instead of colours; the rune sets the note's accents (see GreatRune).</summary>
    public bool UsesRunes { get; init; } = false;

    /// <summary>Shows the ornamental rule under the note title.</summary>
    public bool TitleRule { get; init; } = false;

    /// <summary>Margin between the window edge and the note content (custom-art skins only).</summary>
    public Thickness ContentInset { get; init; } = new Thickness(0);

    /// <summary>Extra empty space above the header buttons (custom-art skins only).</summary>
    public double HeaderTopPad { get; init; } = 0;

    /// <summary>
    /// Distance from the window edge to the visible body on each side (custom-art skins sit inside the window to leave
    /// room for glow or shadow). Snapping and docking line up these visible edges, and the corner resize zones sit on them.
    /// </summary>
    public Thickness VisualInset { get; init; } = new Thickness(0);

    /// <summary>Where the left/right resize zones start (distance from the window edge to the visible body) and how wide they are.</summary>
    public double SideResizeInset { get; init; } = 0;
    public double SideResizeWidth { get; init; } = 6;

    // Classic card shape (ignored by custom-art skins)
    public double CardRadius { get; init; } = 12;
    public double CardBorder { get; init; } = 2;
    public double HeaderRadius { get; init; } = 10;
    public double HeaderDivider { get; init; } = 1;

    // Inner panels
    /// <summary>Cut-corner size of header cells / rows (0 = regular rounded border). See CutBorder.</summary>
    public double CompartmentCut { get; init; } = 0;
    public double ItemCut { get; init; } = 0;
    public double CompartmentRadius { get; init; } = 7;
    public double CompartmentBorder { get; init; } = 1;
    public double ItemRadius { get; init; } = 7;
    public double ItemBorder { get; init; } = 1;
    public double InputRadius { get; init; } = 7;
    public double InputBorder { get; init; } = 1;

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

    /// <summary>Selectable title / text faces (empty = the skin has fixed fonts). First entry is the default.</summary>
    public IReadOnlyList<SkinFontOption> TitleFonts { get; init; } = Array.Empty<SkinFontOption>();
    public IReadOnlyList<SkinFontOption> TextFonts { get; init; } = Array.Empty<SkinFontOption>();

    /// <summary>The title face chosen in the settings for this skin, its default, or null for fixed-font skins.</summary>
    public SkinFontOption? ResolveTitleFont(AppSettings? settings) => Pick(TitleFonts, settings?.SkinTitleFonts);
    public SkinFontOption? ResolveTextFont(AppSettings? settings) => Pick(TextFonts, settings?.SkinTextFonts);

    private SkinFontOption? Pick(IReadOnlyList<SkinFontOption> options, Dictionary<string, string>? chosen)
    {
        if (options.Count == 0) return null;
        if (chosen != null && chosen.TryGetValue(Key, out var fontKey))
        {
            foreach (var option in options)
            {
                if (string.Equals(option.Key, fontKey, StringComparison.OrdinalIgnoreCase)) return option;
            }
        }
        return options[0];
    }

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
        // Calm default: no boxes around the header button groups.
        new NoteSkin { Key = ClassicKey, LocalizationKey = "Skin_Classic", CompartmentBorder = 0 },

        // A real sticky note: light paper in the note colour, dark ink, glue strip, folded corner.
        // Painted by PaperStickyArt.
        new NoteSkin
        {
            Key = PaperKey,
            LocalizationKey = "Skin_Paper",
            CustomArt = true,
            ArtKey = "Paper",
            VisualInset = new Thickness(4, 3, 7, 10), // the sheet; its shadow is outside
            ContentInset = new Thickness(4, 3, 7, 10),
            SideResizeInset = 2,
            SideResizeWidth = 7,
            CompartmentBorder = 0,
            ItemRadius = 5,
            ItemBorder = 1,
            InputRadius = 5,
            InputBorder = 1,
            TintPanels = true,
            MutedHex = "#4F4737",
            FaintHex = "#7A715C",
            PlaceholderHex = "#8C836D",
            DangerHex = "#B42318",
            AmberHex = "#8A5A00",
            EmeraldHex = "#1F6B45",
            CyanHex = "#1E5F78",
            PanelBgHex = "#38FFFFFF",
            PanelBorderHex = "#3D2A2415",
            Recolor = t =>
            {
                string sheet = Mix("#FFF8E0", t.PrimaryHex, 0.40);
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = t.PrimaryHex,
                    GlowHex = Mix(t.PrimaryHex, "#2A2415", 0.62),   // accent ink
                    AccentHex = t.PrimaryHex,
                    BgHex = sheet,
                    HeaderBgHex = Mix(sheet, t.PrimaryHex, 0.30),    // glue strip
                    BorderHex = Mix(sheet, "#2A2415", 0.45),
                    TextPrimaryHex = "#2A2415",
                    TextMutedHex = "#6B624E"
                };
            }
        },
        // Neon HUD panel: stepped top edge with a solid label tab, cut corners, detached hazard
        // pieces, grid + scanlines. Painted by CyberpunkArt.
        new NoteSkin
        {
            Key = CyberpunkKey,
            ArtKey = "Cyberpunk",
            LocalizationKey = "Skin_Cyberpunk",
            CustomArt = true,
            VisualInset = new Thickness(4),
            ContentInset = new Thickness(7, 5, 9, 6),
            SideResizeInset = 3,
            SideResizeWidth = 7,
            HeaderTopPad = 14,
            CompartmentRadius = 0,
            CompartmentBorder = 1,
            ItemRadius = 0,
            ItemBorder = 1,
            InputRadius = 0,
            InputBorder = 1,
            // First entry of each list is the default; the user can pick another in Settings.
            TitleFonts = new[]
            {
                new SkinFontOption("Orbitron", "Orbitron", FontWeights.ExtraBold, 1.0),
                new SkinFontOption("RussoOne", "Russo One", FontWeights.Normal, 1.08),
                new SkinFontOption("Audiowide", "Audiowide", FontWeights.Normal, 1.0),
                new SkinFontOption("VT323", "VT323", FontWeights.Normal, 1.55),
                new SkinFontOption("Silkscreen", "Silkscreen", FontWeights.Normal, 0.95),
                new SkinFontOption("ZenDots", "Zen Dots", FontWeights.Normal, 0.95),
                new SkinFontOption("RubikGlitch", "Rubik Glitch", FontWeights.Normal, 1.1),
                new SkinFontOption("ChakraPetch", "Chakra Petch", FontWeights.Bold, 1.1)
            },
            TextFonts = new[]
            {
                new SkinFontOption("VT323", "VT323", FontWeights.Normal, 1.3),
                new SkinFontOption("Orbitron", "Orbitron", FontWeights.Medium, 0.9),
                new SkinFontOption("ChakraPetch", "Chakra Petch", FontWeights.Medium, 1.0),
                new SkinFontOption("Oxanium", "Oxanium", FontWeights.Medium, 1.0),
                new SkinFontOption("Tomorrow", "Tomorrow Medium", FontWeights.Normal, 1.0, "Tomorrow"),
                new SkinFontOption("ShareTechMono", "Share Tech Mono", FontWeights.Normal, 1.0)
            },
            IconPack = "Cyber",
            CompartmentCut = 7,
            ItemCut = 8,
            MetaFontStack = "./Assets/Fonts/#Share Tech Mono, Consolas, Courier New",
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
            Key = MedievalKey,
            LocalizationKey = "Skin_Medieval",
            ArtKey = "Scroll",
            TitleRule = true,
            CustomArt = true,
            VisualInset = new Thickness(9, 2, 9, 2), // the rods' gold finials
            ContentInset = new Thickness(26, 25, 26, 27),
            SideResizeInset = 16,
            SideResizeWidth = 11,
            HeaderTopPad = 0,
            CompartmentRadius = 3,
            CompartmentBorder = 0,
            ItemRadius = 3,
            ItemBorder = 1,
            InputRadius = 3,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("GrenzeGotisch", "Grenze Gotisch", FontWeights.Bold, 1.45),
                new SkinFontOption("PirataOne", "Pirata One", FontWeights.Normal, 1.45),
                new SkinFontOption("UnifrakturMaguntia", "UnifrakturMaguntia", FontWeights.Normal, 1.4, "Unifraktur Maguntia"),
                new SkinFontOption("UncialAntiqua", "Uncial Antiqua", FontWeights.Normal, 1.15),
                new SkinFontOption("Metamorphous", "Metamorphous", FontWeights.Normal, 1.1)
            },
            TextFonts = new[]
            {
                new SkinFontOption("EagleLake", "Eagle Lake", FontWeights.Normal, 0.88),
                new SkinFontOption("IMFellEnglish", "IM FELL English", FontWeights.Normal, 1.05, "IM Fell English"),
                new SkinFontOption("MedievalSharp", "MedievalSharp", FontWeights.Normal, 1.0),
                new SkinFontOption("Almendra", "Almendra", FontWeights.Normal, 1.08),
                new SkinFontOption("Macondo", "Macondo", FontWeights.Normal, 1.08)
            },
            MetaFontStack = "./Assets/Fonts/#IM FELL English, Palatino Linotype, Georgia, serif",
            IconPack = "Medieval|Elden", // Elden's inked glyphs, but a dye flask (not a rune) on the colour button
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
        },

        // The game's menu look, worn down: murky olive panel with grain, mist, grime and scratches,
        // a chipped pale-gold double frame with notched corners, the note's Great Rune glowing in
        // the corner and drifting motes. Painted by EldenRingArt + RuneArt. The colour picker offers
        // the Great Runes; Recolor below only applies where no rune is known (e.g. Settings preview).
        new NoteSkin
        {
            Key = EldenRingKey,
            LocalizationKey = "Skin_EldenRing",
            CustomArt = true,
            UsesRunes = true,
            ArtKey = "Elden",
            TitleRule = true,
            VisualInset = new Thickness(5),
            ContentInset = new Thickness(13, 11, 13, 13),
            SideResizeInset = 4,
            SideResizeWidth = 7,
            CompartmentRadius = 0,
            CompartmentBorder = 0,
            ItemRadius = 1,
            ItemBorder = 1,
            InputRadius = 1,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("Cinzel", "Cinzel", FontWeights.Bold, 1.05),
                new SkinFontOption("GrenzeGotisch", "Grenze Gotisch", FontWeights.Bold, 1.4),
                new SkinFontOption("Metamorphous", "Metamorphous", FontWeights.Normal, 1.05)
            },
            TextFonts = new[]
            {
                new SkinFontOption("EBGaramond", "EB Garamond", FontWeights.Medium, 1.1),
                new SkinFontOption("IMFellEnglish", "IM FELL English", FontWeights.Normal, 1.05, "IM Fell English"),
                new SkinFontOption("Almendra", "Almendra", FontWeights.Normal, 1.08)
            },
            MetaFontStack = "./Assets/Fonts/#EB Garamond, Palatino Linotype, Georgia, serif",
            IconPack = "Elden",
            AccentTitle = true,
            TintPanels = true,
            MutedHex = "#A39A7C",
            FaintHex = "#7F7861",
            PlaceholderHex = "#716B56",
            DangerHex = "#C8503F",
            AmberHex = "#D9B45A",
            EmeraldHex = "#9CC283",
            CyanHex = "#86B9CB",
            PanelBgHex = "#40000000",
            PanelBorderHex = "#806F6A52",
            Recolor = t =>
            {
                string glow = Mix(t.PrimaryHex, "#F3D98C", 0.38);
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = glow,
                    GlowHex = glow,
                    AccentHex = glow,
                    BgHex = "#1B1C15",
                    HeaderBgHex = "#2A2B21",
                    BorderHex = Mix("#8E8665", glow, 0.18),   // frame
                    TextPrimaryHex = "#DDD6C0",
                    TextMutedHex = "#9A927A"
                };
            }
        },

        // Night sky: deep navy panel, a nebula in the note's colour, a star field, one constellation
        // per note (lower right) and a thin silver frame with sparkles. Own star icons ("Astral" pack).
        // Painted by AstralArt.
        new NoteSkin
        {
            Key = AstralKey,
            LocalizationKey = "Skin_Astral",
            CustomArt = true,
            ArtKey = "Astral",
            TitleRule = true,
            VisualInset = new Thickness(5),
            ContentInset = new Thickness(13, 11, 13, 13),
            SideResizeInset = 4,
            SideResizeWidth = 7,
            CompartmentRadius = 0,
            CompartmentBorder = 0,
            ItemRadius = 1,
            ItemBorder = 1,
            InputRadius = 1,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("Cinzel", "Cinzel", FontWeights.Bold, 1.05),
                new SkinFontOption("Metamorphous", "Metamorphous", FontWeights.Normal, 1.05),
                new SkinFontOption("ZenDots", "Zen Dots", FontWeights.Normal, 0.95)
            },
            TextFonts = new[]
            {
                new SkinFontOption("EBGaramond", "EB Garamond", FontWeights.Medium, 1.1),
                new SkinFontOption("Almendra", "Almendra", FontWeights.Normal, 1.08),
                new SkinFontOption("Oxanium", "Oxanium", FontWeights.Medium, 1.0)
            },
            MetaFontStack = "./Assets/Fonts/#EB Garamond, Palatino Linotype, Georgia, serif",
            IconPack = "Astral",
            AccentTitle = true,
            TintPanels = true,
            MutedHex = "#9AA6C6",
            FaintHex = "#6B7593",
            PlaceholderHex = "#646E8C",
            DangerHex = "#E07A8A",
            AmberHex = "#E8C77A",
            EmeraldHex = "#8FD3B5",
            CyanHex = "#8FC4E8",
            PanelBgHex = "#40000010",
            PanelBorderHex = "#665F6E99",
            Recolor = t =>
            {
                // the note colour, washed toward starlight
                string glow = Mix(t.PrimaryHex, "#CFE0FF", 0.42);
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = glow,
                    GlowHex = glow,
                    AccentHex = glow,
                    BgHex = "#0F1324",
                    HeaderBgHex = "#182038",
                    BorderHex = Mix("#A9B8E0", glow, 0.2),    // silver frame
                    TextPrimaryHex = "#E1E6F4",
                    TextMutedHex = "#8A93AD"
                };
            }
        },

        // An old CRT terminal: phosphor text in the note's colour on black glass, scanlines, a bezel,
        // "[x]" ticks and a block cursor that blinks while the note has focus. Painted by TerminalArt.
        new NoteSkin
        {
            Key = TerminalKey,
            LocalizationKey = "Skin_Terminal",
            CustomArt = true,
            ArtKey = "Terminal",
            VisualInset = new Thickness(4),
            ContentInset = new Thickness(10, 7, 10, 9),
            SideResizeInset = 3,
            SideResizeWidth = 7,
            CompartmentRadius = 0,
            CompartmentBorder = 0,
            ItemRadius = 0,
            ItemBorder = 0,
            InputRadius = 0,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("VT323", "VT323", FontWeights.Normal, 1.55),
                new SkinFontOption("ShareTechMono", "Share Tech Mono", FontWeights.Normal, 1.0),
                new SkinFontOption("Silkscreen", "Silkscreen", FontWeights.Normal, 0.95)
            },
            TextFonts = new[]
            {
                new SkinFontOption("VT323", "VT323", FontWeights.Normal, 1.3),
                new SkinFontOption("ShareTechMono", "Share Tech Mono", FontWeights.Normal, 1.0)
            },
            MetaFontStack = "./Assets/Fonts/#Share Tech Mono, Consolas, Courier New",
            AccentTitle = true,
            TintPanels = true,
            TextCheckboxes = true,
            BlockCursor = true,
            InkFromTheme = true,
            PanelBgHex = "#00000000",
            Recolor = t =>
            {
                string p = Mix(t.GlowHex, "#FFFFFF", 0.12); // the phosphor
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = p,
                    GlowHex = p,
                    AccentHex = p,
                    BgHex = Mix("#030503", p, 0.05),
                    HeaderBgHex = Mix("#050805", p, 0.10),
                    BorderHex = Mix(p, "#000000", 0.35),
                    TextPrimaryHex = p,
                    TextMutedHex = Mix(p, "#000000", 0.42)
                };
            }
        },

        // A blueprint: blue drawing paper with a fine and a coarse grid, a white double border with zone ticks,
        // and the title row drawn as the drawing's title block, the note's id as the drawing number.
        // Painted by BlueprintArt.
        new NoteSkin
        {
            Key = BlueprintKey,
            LocalizationKey = "Skin_Blueprint",
            CustomArt = true,
            ArtKey = "Blueprint",
            TitleReserveRight = 92,
            VisualInset = new Thickness(4),
            ContentInset = new Thickness(15, 13, 15, 15),
            SideResizeInset = 3,
            SideResizeWidth = 8,
            CompartmentRadius = 0,
            CompartmentBorder = 1,
            ItemRadius = 0,
            ItemBorder = 1,
            InputRadius = 0,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("ChakraPetch", "Chakra Petch", FontWeights.Bold, 1.05),
                new SkinFontOption("Oxanium", "Oxanium", FontWeights.Medium, 1.0),
                new SkinFontOption("ShareTechMono", "Share Tech Mono", FontWeights.Normal, 1.0)
            },
            TextFonts = new[]
            {
                new SkinFontOption("ShareTechMono", "Share Tech Mono", FontWeights.Normal, 1.0),
                new SkinFontOption("Tomorrow", "Tomorrow Medium", FontWeights.Normal, 1.0, "Tomorrow"),
                new SkinFontOption("ChakraPetch", "Chakra Petch", FontWeights.Medium, 1.05)
            },
            MetaFontStack = "./Assets/Fonts/#Share Tech Mono, Consolas, Courier New",
            TintPanels = true,
            MutedHex = "#A9C7E8",
            FaintHex = "#7FA3C9",
            PlaceholderHex = "#86A9CF",
            DangerHex = "#FF9A9A",
            AmberHex = "#FFD27A",
            EmeraldHex = "#A6F0CF",
            CyanHex = "#BFE8FF",
            PanelBgHex = "#12FFFFFF",
            PanelBorderHex = "#55EAF4FF",
            Recolor = t =>
            {
                string paper = Mix("#123B6B", t.PrimaryHex, 0.10); // violet notes print like a diazo blueline
                string line = Mix("#E6F3FF", t.PrimaryHex, 0.22);
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = t.PrimaryHex,
                    GlowHex = line,
                    AccentHex = line,
                    BgHex = paper,
                    HeaderBgHex = Mix(paper, "#000000", 0.12),
                    BorderHex = "#D7EAFF",
                    TextPrimaryHex = "#F2F8FF",
                    TextMutedHex = "#A9C7E8"
                };
            }
        },

        // Rice paper with fibres, a sumi brush stroke under the header and a faint ink-wash ring in the note's
        // colour, and a red name seal (hanko) beside the title carrying its first letter. Painted by InkWashiArt.
        new NoteSkin
        {
            Key = InkWashiKey,
            LocalizationKey = "Skin_InkWashi",
            CustomArt = true,
            ArtKey = "InkWashi",
            ArtShowsTitle = true,
            TitleReserveRight = 36,
            VisualInset = new Thickness(4, 3, 6, 8),
            ContentInset = new Thickness(12, 7, 14, 14),
            SideResizeInset = 2,
            SideResizeWidth = 7,
            CompartmentRadius = 2,
            CompartmentBorder = 0,
            ItemRadius = 2,
            ItemBorder = 1,
            InputRadius = 2,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("EBGaramond", "EB Garamond", FontWeights.Medium, 1.18),
                new SkinFontOption("IMFellEnglish", "IM FELL English", FontWeights.Normal, 1.18, "IM Fell English"),
                new SkinFontOption("Macondo", "Macondo", FontWeights.Normal, 1.1)
            },
            TextFonts = new[]
            {
                new SkinFontOption("EBGaramond", "EB Garamond", FontWeights.Medium, 1.1),
                new SkinFontOption("IMFellEnglish", "IM FELL English", FontWeights.Normal, 1.05, "IM Fell English")
            },
            MetaFontStack = "./Assets/Fonts/#EB Garamond, Palatino Linotype, Georgia, serif",
            AccentTitle = true,
            TintPanels = true,
            MutedHex = "#5E574C",
            FaintHex = "#8A8172",
            PlaceholderHex = "#9A907F",
            DangerHex = "#B3261E",
            AmberHex = "#8A5A00",
            EmeraldHex = "#2F6B4A",
            CyanHex = "#245E73",
            PanelBgHex = "#30FFFFFF",
            PanelBorderHex = "#2E201C18",
            Recolor = t =>
            {
                string paper = Mix("#F3EDE0", t.PrimaryHex, 0.06);
                string accent = Mix(t.PrimaryHex, "#1F1B17", 0.55); // the note colour as a dark ink
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = t.PrimaryHex,
                    GlowHex = accent,
                    AccentHex = accent,
                    BgHex = paper,
                    HeaderBgHex = Mix(paper, "#1F1B17", 0.05),
                    BorderHex = Mix(paper, "#1F1B17", 0.4),
                    TextPrimaryHex = "#1F1B17",
                    TextMutedHex = "#6B6358"
                };
            }
        },

        // The abyss: a dark teal gradient with faint light from above, drifting marine snow and bubbles, and one
        // glowing creature per note in the lower right (jellyfish, anglerfish lure, nautilus). Painted by DeepSeaArt.
        new NoteSkin
        {
            Key = DeepSeaKey,
            LocalizationKey = "Skin_DeepSea",
            CustomArt = true,
            ArtKey = "DeepSea",
            VisualInset = new Thickness(5),
            ContentInset = new Thickness(13, 11, 13, 13),
            SideResizeInset = 4,
            SideResizeWidth = 7,
            CompartmentRadius = 8,
            CompartmentBorder = 0,
            ItemRadius = 9,
            ItemBorder = 1,
            InputRadius = 9,
            InputBorder = 1,
            TitleFonts = new[]
            {
                new SkinFontOption("Audiowide", "Audiowide", FontWeights.Normal, 1.0),
                new SkinFontOption("ZenDots", "Zen Dots", FontWeights.Normal, 0.95),
                new SkinFontOption("Metamorphous", "Metamorphous", FontWeights.Normal, 1.05)
            },
            AccentTitle = true,
            TintPanels = true,
            MutedHex = "#86AEB8",
            FaintHex = "#5D808A",
            PlaceholderHex = "#6C919B",
            DangerHex = "#FF7A8A",
            AmberHex = "#FFD38A",
            EmeraldHex = "#7CF2C8",
            CyanHex = "#7FE7FF",
            PanelBgHex = "#2E001A24",
            PanelBorderHex = "#4D3FB6C4",
            Recolor = t =>
            {
                string glow = Mix(t.PrimaryHex, "#B8FFF4", 0.35); // bioluminescence
                return new NoteColorTheme
                {
                    Key = t.Key,
                    Name = t.Name,
                    PrimaryHex = glow,
                    GlowHex = glow,
                    AccentHex = glow,
                    BgHex = Mix("#04161F", t.PrimaryHex, 0.04),
                    HeaderBgHex = Mix("#08222E", t.PrimaryHex, 0.05),
                    BorderHex = Mix("#3FA9B8", glow, 0.35),
                    TextPrimaryHex = "#DDF4F5",
                    TextMutedHex = "#86AEB8"
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
