using System.Collections.Generic;

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
        if (string.IsNullOrEmpty(key) || !Themes.TryGetValue(key, out var theme))
        {
            return Themes["Amber"];
        }
        return theme;
    }
}
