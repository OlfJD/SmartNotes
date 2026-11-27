using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Controls;

/// <summary>
/// Per-skin look for the note's menus, tooltips and colour popup.
/// A skin with a menu dictionary (UI/Themes/*Menu.xaml: Menu* tokens + the shared templates in SkinMenuBase.xaml)
/// gets it merged into the note window (popup, tooltips) and into each ContextMenu the note opens.
/// Classic has none and keeps the default menus from DarkTheme.xaml.
/// </summary>
public static class SkinMenus
{
    private static readonly Dictionary<string, string> Files = new(StringComparer.Ordinal)
    {
        [NoteSkin.EldenRingKey] = "GraceMenu.xaml",
        [NoteSkin.CyberpunkKey] = "CyberpunkMenu.xaml",
        [NoteSkin.MedievalKey] = "MedievalMenu.xaml",
        [NoteSkin.PaperKey] = "PaperMenu.xaml",
        [NoteSkin.AstralKey] = "AstralMenu.xaml",
        [NoteSkin.TerminalKey] = "TerminalMenu.xaml",
        [NoteSkin.BlueprintKey] = "BlueprintMenu.xaml",
        [NoteSkin.InkWashiKey] = "InkWashiMenu.xaml",
        [NoteSkin.DeepSeaKey] = "DeepSeaMenu.xaml",
    };

    private static readonly Dictionary<string, ResourceDictionary> Loaded = new(StringComparer.Ordinal);

    /// <summary>The skin's menu dictionary, or null for the default menus.</summary>
    public static ResourceDictionary? For(NoteSkin? skin)
    {
        if (skin == null || !Files.TryGetValue(skin.Key, out var file)) return null;
        if (!Loaded.TryGetValue(skin.Key, out var dict))
        {
            dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Themes/{file}", UriKind.Absolute) };
            Loaded[skin.Key] = dict;
        }
        return dict;
    }

    /// <summary>Restyles a context menu (and its submenus) for the skin. No-op for skins without a menu dictionary.</summary>
    public static void Apply(ContextMenu menu, NoteSkin? skin)
    {
        var dict = For(skin);
        if (dict == null) return;
        menu.Resources.MergedDictionaries.Add(dict);
        if (dict["SkinContextMenu"] is Style style) menu.Style = style;
    }

    /// <summary>Swaps the skin's menu dictionary into an element's resources (replacing a previous skin's and
    /// leaving any other merged dictionaries alone), so DynamicResource Menu* lookups and implicit ToolTip /
    /// MenuItem styles below it switch over.</summary>
    public static void ApplyToPanel(FrameworkElement panel, NoteSkin? skin)
    {
        var dict = For(skin);
        var merged = panel.Resources.MergedDictionaries;
        if (dict != null && merged.Contains(dict) && merged.Count(d => Loaded.ContainsValue(d)) == 1) return;
        foreach (var old in merged.Where(d => Loaded.ContainsValue(d)).ToList()) merged.Remove(old);
        if (dict != null) merged.Add(dict);
    }
}
