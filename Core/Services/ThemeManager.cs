using System;
using System.Collections.Generic;
using System.Windows;

namespace SmartNotes.Core.Services;

public static class ThemeManager
{
    private static readonly Dictionary<string, string> ThemeDictionaryPaths = new()
    {
        { "Default", "pack://application:,,,/UI/Themes/DefaultStickyNoteTemplate.xaml" },
        { "Cyberpunk", "pack://application:,,,/UI/Themes/CyberpunkTheme.xaml" },
    };

    public static void ApplyTheme(Window window, string themeKey)
    {
        if (string.IsNullOrEmpty(themeKey) || !ThemeDictionaryPaths.ContainsKey(themeKey))
        {
            themeKey = "Default";
        }

        string uriPath = ThemeDictionaryPaths[themeKey];
        var uri = new Uri(uriPath, UriKind.Absolute);

        // Find the existing theme dictionary in the window's merged dictionaries
        ResourceDictionary? existingTheme = null;
        foreach (var dict in window.Resources.MergedDictionaries)
        {
            if (dict.Source != null && dict.Source.ToString().Contains("UI/Themes/"))
            {
                existingTheme = dict;
                break;
            }
        }

        // Add the new one
        var newThemeDict = new ResourceDictionary { Source = uri };
        window.Resources.MergedDictionaries.Add(newThemeDict);

        // Remove the old one
        if (existingTheme != null)
        {
            window.Resources.MergedDictionaries.Remove(existingTheme);
        }

        // Apply the template defined in the dictionary
        // We assume the theme dictionary has a ControlTemplate with x:Key="StickyNoteTemplate"
        var template = newThemeDict["StickyNoteTemplate"] as System.Windows.Controls.ControlTemplate;
        if (template != null)
        {
            window.Template = template;
            window.ApplyTemplate(); // Force regeneration of the visual tree
        }
    }
}
