using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;
using Image = System.Windows.Controls.Image;

namespace SmartNotes.UI.Windows;

/// <summary>
/// Picks the program a note shows with: type to search the programs that are open now (listed first) and the ones
/// installed on the PC (Start menu), or browse for any program file. In the note's theme.
/// </summary>
public partial class ProgramPickerDialog : Window
{
    public OpenProgram? Chosen { get; private set; }

    private readonly List<OpenProgram> _all = new();
    private readonly string? _current;

    public ProgramPickerDialog(NoteSkin? skin, string? currentExe)
    {
        InitializeComponent();
        SkinMenus.ApplyToPanel(this, skin);
        _current = currentExe;
        TxtTitle.Text = LocalizationService.T("Menu_ShowOnlyWith");
        TxtSearchHint.Text = LocalizationService.T("Picker_Search");
        TxtBrowse.Text = LocalizationService.T("Picker_Browse");
        BtnCancel.Content = LocalizationService.T("MsgBox_Cancel");
        BtnChoose.Content = LocalizationService.T("Picker_Link");
        KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };

        _all.AddRange(ProgramWindows.ListOpenPrograms());
        TxtStatus.Text = LocalizationService.T("Picker_Loading");
        Fill();
        Loaded += async (s, e) =>
        {
            WindowBlurHelper.ApplyModernWindowStyles(this);
            TxtSearch.Focus();
            var installed = await Task.Run(ProgramWindows.ListInstalledPrograms);
            foreach (var p in installed)
            {
                if (!_all.Any(a => string.Equals(a.ExeName, p.ExeName, StringComparison.OrdinalIgnoreCase))) _all.Add(p);
            }
            TxtStatus.Text = LocalizationService.T("Picker_Count", _all.Count);
            Fill();
        };
    }

    /// <summary>Open programs first, then the rest by name; filtered by the search text (name or exe).</summary>
    private void Fill()
    {
        string q = TxtSearch.Text.Trim();
        var shown = _all
            .Where(p => q.Length == 0 || p.DisplayName.Contains(q, StringComparison.CurrentCultureIgnoreCase) || p.ExeName.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.IsOpen)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(200)
            .ToList();
        ProgramList.Items.Clear();
        foreach (var p in shown) ProgramList.Items.Add(Row(p));
        if (ProgramList.Items.Count > 0 && q.Length > 0) ProgramList.SelectedIndex = 0;
        BtnChoose.IsEnabled = ProgramList.SelectedItem != null;
    }

    private ListBoxItem Row(OpenProgram p)
    {
        var panel = new DockPanel { LastChildFill = true };
        var icon = new Image { Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0), Source = IconOf(p.Path) };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        DockPanel.SetDock(icon, Dock.Left);
        panel.Children.Add(icon);
        if (p.IsOpen)
        {
            var open = new TextBlock { Text = LocalizationService.T("Picker_Open"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            open.SetResourceReference(TextBlock.ForegroundProperty, "MenuAccentBrush");
            DockPanel.SetDock(open, Dock.Right);
            panel.Children.Add(open);
        }
        var text = new StackPanel();
        bool linked = string.Equals(AppLink.Normalize(_current), p.ExeName, StringComparison.OrdinalIgnoreCase);
        text.Children.Add(new TextBlock { Text = p.DisplayName, FontWeight = linked ? FontWeights.Bold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis });
        var exe = new TextBlock { Text = p.ExeName, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        exe.SetResourceReference(TextBlock.ForegroundProperty, "MenuLabelMutedBrush");
        text.Children.Add(exe);
        panel.Children.Add(text);
        return new ListBoxItem { Content = panel, Tag = p, ToolTip = p.Path ?? p.ExeName };
    }

    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    private static ImageSource? IconOf(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (IconCache.TryGetValue(path, out var cached)) return cached;
        ImageSource? source = null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon != null)
            {
                source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
            }
        }
        catch
        {
            // no icon in that file
        }
        return IconCache[path] = source;
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchHint.Visibility = TxtSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Fill();
    }

    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && ProgramList.Items.Count > 0)
        {
            ProgramList.SelectedIndex = Math.Min(ProgramList.Items.Count - 1, ProgramList.SelectedIndex + 1);
            ProgramList.ScrollIntoView(ProgramList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ProgramList.SelectedIndex > 0)
        {
            ProgramList.SelectedIndex--;
            ProgramList.ScrollIntoView(ProgramList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ProgramList.SelectedItem != null)
        {
            BtnChoose_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void ProgramList_SelectionChanged(object sender, SelectionChangedEventArgs e) => BtnChoose.IsEnabled = ProgramList.SelectedItem != null;

    private void ProgramList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProgramList.SelectedItem != null) BtnChoose_Click(this, new RoutedEventArgs());
    }

    private void BtnChoose_Click(object sender, RoutedEventArgs e)
    {
        if (ProgramList.SelectedItem is ListBoxItem { Tag: OpenProgram p })
        {
            Chosen = p;
            DialogResult = true;
        }
    }

    /// <summary>Any program file on the PC (Program Files to start with); a shortcut is followed to its program.</summary>
    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LocalizationService.T("Picker_FileFilter"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            DereferenceLinks = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true && dialog.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Chosen = ProgramWindows.FromFile(dialog.FileName);
            DialogResult = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }
}
