using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Media;

namespace SmartNotes.Core.Models;

public class NoteColorTheme
{
    public string Key { get; set; } = "Amber";
    public string Name { get; set; } = "Radiant Amber";
    public string PrimaryHex { get; set; } = "#F59E0B";
    public string GlowHex { get; set; } = "#FBBF24";
    public string BgHex { get; set; } = "#1C1710";
    public string HeaderBgHex { get; set; } = "#261D12";
    public string BorderHex { get; set; } = "#5A3E16";
    public string TextPrimaryHex { get; set; } = "#FFFBEB";
    public string TextMutedHex { get; set; } = "#FDE68A";
    public string AccentHex { get; set; } = "#F59E0B";

    // Pre-frozen cached brushes to save memory and boost WPF rendering
    private SolidColorBrush? _bgBrush;
    private SolidColorBrush? _headerBgBrush;
    private SolidColorBrush? _borderBrush;
    private SolidColorBrush? _primaryBrush;
    private SolidColorBrush? _glowBrush;
    private SolidColorBrush? _textPrimaryBrush;
    private SolidColorBrush? _textMutedBrush;

    public SolidColorBrush BgBrush => _bgBrush ??= CreateFrozenBrush(BgHex);
    public SolidColorBrush HeaderBgBrush => _headerBgBrush ??= CreateFrozenBrush(HeaderBgHex);
    public SolidColorBrush BorderBrush => _borderBrush ??= CreateFrozenBrush(BorderHex);
    public SolidColorBrush PrimaryBrush => _primaryBrush ??= CreateFrozenBrush(PrimaryHex);
    public SolidColorBrush GlowBrush => _glowBrush ??= CreateFrozenBrush(GlowHex);
    public SolidColorBrush TextPrimaryBrush => _textPrimaryBrush ??= CreateFrozenBrush(TextPrimaryHex);
    public SolidColorBrush TextMutedBrush => _textMutedBrush ??= CreateFrozenBrush(TextMutedHex);

    public static SolidColorBrush CreateFrozenBrush(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            var fallback = new SolidColorBrush(Colors.White);
            fallback.Freeze();
            return fallback;
        }
    }

    private static readonly ConcurrentDictionary<string, NoteColorTheme> CustomThemesCache = new();

    public static readonly Dictionary<string, NoteColorTheme> Themes = new()
    {
        ["Amber"] = new NoteColorTheme
        {
            Key = "Amber",
            Name = "Radiant Amber",
            PrimaryHex = "#F59E0B",
            GlowHex = "#FBBF24",
            BgHex = "#1C1710",
            HeaderBgHex = "#281E12",
            BorderHex = "#614418",
            TextPrimaryHex = "#FFFBEB",
            TextMutedHex = "#FDE68A",
            AccentHex = "#F59E0B"
        },
        ["Emerald"] = new NoteColorTheme
        {
            Key = "Emerald",
            Name = "Neon Emerald",
            PrimaryHex = "#10B981",
            GlowHex = "#34D399",
            BgHex = "#0E1C18",
            HeaderBgHex = "#122620",
            BorderHex = "#1D5443",
            TextPrimaryHex = "#ECFDF5",
            TextMutedHex = "#A7F3D0",
            AccentHex = "#10B981"
        },
        ["Violet"] = new NoteColorTheme
        {
            Key = "Violet",
            Name = "Cyber Violet",
            PrimaryHex = "#8B5CF6",
            GlowHex = "#A78BFA",
            BgHex = "#171228",
            HeaderBgHex = "#211A38",
            BorderHex = "#49357A",
            TextPrimaryHex = "#F5F3FF",
            TextMutedHex = "#DDD6FE",
            AccentHex = "#8B5CF6"
        },
        ["Cyan"] = new NoteColorTheme
        {
            Key = "Cyan",
            Name = "Electric Cyan",
            PrimaryHex = "#06B6D4",
            GlowHex = "#22D3EE",
            BgHex = "#0D1B24",
            HeaderBgHex = "#122533",
            BorderHex = "#184E68",
            TextPrimaryHex = "#ECFEFF",
            TextMutedHex = "#A5F3FC",
            AccentHex = "#06B6D4"
        },
        ["Rose"] = new NoteColorTheme
        {
            Key = "Rose",
            Name = "Coral Rose",
            PrimaryHex = "#F43F5E",
            GlowHex = "#FB7185",
            BgHex = "#201016",
            HeaderBgHex = "#2D1620",
            BorderHex = "#6E233B",
            TextPrimaryHex = "#FFF1F2",
            TextMutedHex = "#FECDD3",
            AccentHex = "#F43F5E"
        },
        ["Obsidian"] = new NoteColorTheme
        {
            Key = "Obsidian",
            Name = "Stealth Obsidian",
            PrimaryHex = "#3B82F6",
            GlowHex = "#60A5FA",
            BgHex = "#111622",
            HeaderBgHex = "#171F30",
            BorderHex = "#2C3E63",
            TextPrimaryHex = "#FFFFFF",
            TextMutedHex = "#94A3B8",
            AccentHex = "#3B82F6"
        },
        ["Gold"] = new NoteColorTheme
        {
            Key = "Gold",
            Name = "Classic Sticky Gold",
            PrimaryHex = "#EAB308",
            GlowHex = "#FACC15",
            BgHex = "#211E10",
            HeaderBgHex = "#2E2A14",
            BorderHex = "#6B6020",
            TextPrimaryHex = "#FEFCE8",
            TextMutedHex = "#FEF08A",
            AccentHex = "#EAB308"
        },
        ["Mint"] = new NoteColorTheme
        {
            Key = "Mint",
            Name = "Fresh Mint",
            PrimaryHex = "#14B8A6",
            GlowHex = "#2DD4BF",
            BgHex = "#0E1E1E",
            HeaderBgHex = "#132A2A",
            BorderHex = "#1F5957",
            TextPrimaryHex = "#F0FDFA",
            TextMutedHex = "#99F6E4",
            AccentHex = "#14B8A6"
        }
    };

    public static NoteColorTheme Get(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Themes["Amber"];
        }

        if (Themes.TryGetValue(key, out var theme))
        {
            return theme;
        }

        if (key.StartsWith("#") || (key.Length == 6 && IsHex(key)))
        {
            return FromCustomHex(key.StartsWith("#") ? key : $"#{key}");
        }

        return Themes["Amber"];
    }

    public static NoteColorTheme FromCustomHex(string hex)
    {
        try
        {
            string cleanHex = hex.Trim().TrimStart('#');
            if (cleanHex.Length == 6)
            {
                string primary = $"#{cleanHex.ToUpperInvariant()}";
                if (CustomThemesCache.TryGetValue(primary, out var cached))
                {
                    return cached;
                }

                byte r = Convert.ToByte(cleanHex.Substring(0, 2), 16);
                byte g = Convert.ToByte(cleanHex.Substring(2, 2), 16);
                byte b = Convert.ToByte(cleanHex.Substring(4, 2), 16);

                string glow = $"#{Math.Min(255, r + 40):X2}{Math.Min(255, g + 40):X2}{Math.Min(255, b + 40):X2}";
                
                // Obsidian dark tinted background & header
                byte bgR = (byte)Math.Clamp((int)(r * 0.10 + 10), 10, 35);
                byte bgG = (byte)Math.Clamp((int)(g * 0.10 + 10), 10, 35);
                byte bgB = (byte)Math.Clamp((int)(b * 0.10 + 14), 14, 40);
                string bg = $"#{bgR:X2}{bgG:X2}{bgB:X2}";

                byte hR = (byte)Math.Clamp((int)(r * 0.16 + 14), 14, 48);
                byte hG = (byte)Math.Clamp((int)(g * 0.16 + 14), 14, 48);
                byte hB = (byte)Math.Clamp((int)(b * 0.16 + 18), 18, 55);
                string headerBg = $"#{hR:X2}{hG:X2}{hB:X2}";

                byte bdR = (byte)Math.Clamp((int)(r * 0.40 + 20), 20, 120);
                byte bdG = (byte)Math.Clamp((int)(g * 0.40 + 20), 20, 120);
                byte bdB = (byte)Math.Clamp((int)(b * 0.40 + 24), 24, 130);
                string border = $"#{bdR:X2}{bdG:X2}{bdB:X2}";

                byte tmR = (byte)Math.Min(255, (r + 255) / 2);
                byte tmG = (byte)Math.Min(255, (g + 255) / 2);
                byte tmB = (byte)Math.Min(255, (b + 255) / 2);
                string textMuted = $"#{tmR:X2}{tmG:X2}{tmB:X2}";

                var created = new NoteColorTheme
                {
                    Key = primary,
                    Name = $"Custom ({primary})",
                    PrimaryHex = primary,
                    GlowHex = glow,
                    BgHex = bg,
                    HeaderBgHex = headerBg,
                    BorderHex = border,
                    TextPrimaryHex = "#F8FAFC",
                    TextMutedHex = textMuted,
                    AccentHex = primary
                };

                CustomThemesCache[primary] = created;
                return created;
            }
        }
        catch { }

        return Themes["Amber"];
    }

    private static bool IsHex(string str)
    {
        foreach (char c in str)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }
}
