using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfApplication = System.Windows.Application;

namespace SmartNotes.UI.Controls;

public enum MenuTone
{
    Normal,
    Accent,
    Danger,
    Muted
}

/// <summary>
/// The one way menus are built in the app (tray menu, note "more" menu, text right-click menu), so
/// they all share the ContextMenu / MenuItem styles from DarkTheme.xaml and look identical.
/// </summary>
public static class AppMenu
{
    public static ContextMenu Create() => new ContextMenu();

    /// <summary>A clickable row. <paramref name="isChecked"/> shows the accent check in the icon column.</summary>
    public static MenuItem Item(string header, string? iconKey, Action? onClick, MenuTone tone = MenuTone.Normal, bool isChecked = false, bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled, IsChecked = isChecked };
        Decorate(item, iconKey, tone, isChecked);
        if (onClick != null) item.Click += (s, e) => onClick();
        return item;
    }

    /// <summary>A row that opens a submenu; add children to its Items.</summary>
    public static MenuItem Sub(string header, string? iconKey, MenuTone tone = MenuTone.Normal)
    {
        var item = new MenuItem { Header = header };
        Decorate(item, iconKey, tone, false);
        return item;
    }

    /// <summary>A small non-clickable section label.</summary>
    public static MenuItem Heading(string text)
    {
        return new MenuItem
        {
            Header = text.ToUpperInvariant(),
            IsEnabled = false,
            Opacity = 1.0,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Resource("TextMutedBrush"),
            Padding = new Thickness(9, 5, 9, 2)
        };
    }

    public static Separator Separator() => new Separator();

    private static void Decorate(MenuItem item, string? iconKey, MenuTone tone, bool isChecked)
    {
        WpfBrush? text = tone switch
        {
            MenuTone.Danger => Resource("AlertRedBrush"),
            MenuTone.Muted => Resource("TextMutedBrush"),
            _ => null
        };
        if (text != null) item.Foreground = text;
        if (tone == MenuTone.Accent) item.FontWeight = FontWeights.SemiBold;

        // A checked row shows the check glyph instead of its icon.
        if (iconKey != null && !isChecked)
        {
            WpfBrush iconBrush = tone switch
            {
                MenuTone.Danger => Resource("AlertRedBrush"),
                MenuTone.Accent => Resource("AccentBrush"),
                MenuTone.Muted => Resource("TextMutedBrush"),
                _ => Resource("TextSecondaryBrush")
            } ?? Brushes.White;
            item.Icon = new LucideIcon { IconKey = iconKey, Size = 14, StrokeThickness = 1.9, Foreground = iconBrush };
        }
    }

    private static WpfBrush? Resource(string key) => WpfApplication.Current?.TryFindResource(key) as WpfBrush;
}
