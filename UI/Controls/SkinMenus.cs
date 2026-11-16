using System;
using System.Windows;
using System.Windows.Controls;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Controls;

/// <summary>
/// Per-skin menu look. A skin with its own menu dictionary (UI/Themes/*Menu.xaml) gets it merged into
/// the note's ContextMenus and colour popup; every other skin keeps the default menus from DarkTheme.xaml.
/// A menu dictionary provides: the keyed "SkinContextMenu" style, implicit MenuItem and menu-separator styles,
/// and overrides for the Menu*/Popup* keys that DarkTheme.xaml defines.
/// </summary>
public static class SkinMenus
{
    private static ResourceDictionary? _grace;

    /// <summary>The skin's menu dictionary, or null for the default menus.</summary>
    public static ResourceDictionary? For(NoteSkin? skin)
    {
        if (skin == null) return null;
        if (skin.Key == NoteSkin.EldenRingKey)
            return _grace ??= new ResourceDictionary { Source = new Uri("pack://application:,,,/UI/Themes/GraceMenu.xaml", UriKind.Absolute) };
        return null;
    }

    /// <summary>Restyles a context menu (and its submenus) for the skin. No-op for skins without a menu dictionary.</summary>
    public static void Apply(ContextMenu menu, NoteSkin? skin)
    {
        var dict = For(skin);
        if (dict == null) return;
        menu.Resources.MergedDictionaries.Add(dict);
        if (dict["SkinContextMenu"] is Style style) menu.Style = style;
    }

    /// <summary>Points a panel's resources at the skin's menu dictionary (replacing a previous skin's), so
    /// DynamicResource Menu*/Popup* lookups inside it switch over.</summary>
    public static void ApplyToPanel(FrameworkElement panel, NoteSkin? skin)
    {
        var dict = For(skin);
        var merged = panel.Resources.MergedDictionaries;
        if (dict != null && merged.Count == 1 && ReferenceEquals(merged[0], dict)) return;
        merged.Clear();
        if (dict != null) merged.Add(dict);
    }
}
