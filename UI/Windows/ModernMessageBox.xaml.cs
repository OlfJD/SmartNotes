using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;

namespace SmartNotes.UI.Windows;

public enum ModernMessageButtons
{
    OK,
    OKCancel,
    YesNo
}

public enum ModernMessageIcon
{
    Info,
    Success,
    Warning,
    Error,
    Update
}

public partial class ModernMessageBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
    private ModernMessageButtons _buttonsMode = ModernMessageButtons.OK;

    public ModernMessageBox()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowBlurHelper.ApplyModernWindowStyles(this);
        BtnPrimary.Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (BtnSecondary.Visibility == Visibility.Visible)
            {
                Result = (_buttonsMode == ModernMessageButtons.YesNo) ? MessageBoxResult.No : MessageBoxResult.Cancel;
            }
            else
            {
                Result = MessageBoxResult.OK;
            }
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            BtnPrimary_Click(BtnPrimary, new RoutedEventArgs());
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Result = (BtnSecondary.Visibility == Visibility.Visible && _buttonsMode == ModernMessageButtons.YesNo)
            ? MessageBoxResult.No
            : MessageBoxResult.Cancel;
        Close();
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        Result = (_buttonsMode == ModernMessageButtons.YesNo)
            ? MessageBoxResult.Yes
            : MessageBoxResult.OK;
        Close();
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        Result = (_buttonsMode == ModernMessageButtons.YesNo)
            ? MessageBoxResult.No
            : MessageBoxResult.Cancel;
        Close();
    }

    public static MessageBoxResult Show(
        string message,
        string title = "SmartNotes",
        ModernMessageButtons buttons = ModernMessageButtons.OK,
        ModernMessageIcon icon = ModernMessageIcon.Info,
        string? heading = null,
        Window? owner = null)
    {
        if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
        {
            return Application.Current.Dispatcher.Invoke(() => Show(message, title, buttons, icon, heading, owner));
        }

        var box = new ModernMessageBox();
        box._buttonsMode = buttons;

        if (owner != null)
        {
            box.Owner = owner;
            box.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        box.TxtTitle.Text = title;
        box.TxtMessage.Text = message;
        box.TxtHeading.Text = heading ?? title;

        // Configure Icon & Palette
        switch (icon)
        {
            case ModernMessageIcon.Success:
                box.MainIcon.IconKey = "check-check";
                box.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Emerald
                box.IconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                box.IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129));
                break;

            case ModernMessageIcon.Update:
                box.MainIcon.IconKey = "sparkles";
                box.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                box.IconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                box.IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 245, 158, 11));
                break;

            case ModernMessageIcon.Warning:
                box.MainIcon.IconKey = "eye-off";
                box.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(251, 113, 133)); // Rose
                box.IconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 251, 113, 133));
                box.IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 251, 113, 133));
                break;

            case ModernMessageIcon.Error:
                box.MainIcon.IconKey = "x";
                box.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                box.IconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68));
                box.IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68));
                break;

            default: // Info
                box.MainIcon.IconKey = "zap";
                box.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Cyan
                box.IconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248));
                box.IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248));
                break;
        }

        // Configure Buttons with Localization
        switch (buttons)
        {
            case ModernMessageButtons.YesNo:
                box.BtnPrimary.Content = LocalizationService.T("MsgBox_YesUpdate");
                box.BtnSecondary.Content = LocalizationService.T("MsgBox_No");
                box.BtnSecondary.Visibility = Visibility.Visible;
                break;

            case ModernMessageButtons.OKCancel:
                box.BtnPrimary.Content = LocalizationService.T("MsgBox_OK");
                box.BtnSecondary.Content = LocalizationService.T("MsgBox_Cancel");
                box.BtnSecondary.Visibility = Visibility.Visible;
                break;

            default: // OK
                box.BtnPrimary.Content = LocalizationService.T("MsgBox_GotIt");
                box.BtnSecondary.Visibility = Visibility.Collapsed;
                break;
        }

        box.ShowDialog();
        return box.Result;
    }
}
