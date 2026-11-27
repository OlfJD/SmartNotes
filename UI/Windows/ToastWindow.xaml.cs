using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;
using SmartNotes.UI.Controls;

namespace SmartNotes.UI.Windows;

public enum ToastTone { Info, Success, Warning }

/// <summary>
/// SmartNotes' own messages, in place of Windows tray balloons: a small panel in the lower right corner that never
/// takes the focus, styled like the menus of the app (or of the note it is about), stacked when several come at once.
/// It closes after a few seconds (not while the mouse is on it), on a click, or with its × button.
/// </summary>
public static class Toasts
{
    private static readonly List<ToastWindow> Open = new();
    private const int MaxOpen = 4;

    /// <param name="skin">The note's theme when the message is about a note; null = the app's own look.</param>
    /// <param name="onClick">What a click on the message does (e.g. bring the note forward); it closes either way.</param>
    /// <param name="actionLabel">An optional button on the message; it runs <paramref name="onClick"/>.</param>
    public static void Show(string title, string message, string icon = "sparkles", NoteSkin? skin = null,
        ToastTone tone = ToastTone.Info, Action? onClick = null, string? actionLabel = null, TimeSpan? duration = null)
    {
        var app = Application.Current;
        if (app == null) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => Show(title, message, icon, skin, tone, onClick, actionLabel, duration));
            return;
        }
        try
        {
            while (Open.Count >= MaxOpen) Open[0].CloseNow();
            var toast = new ToastWindow(title, message, icon, skin, tone, onClick, actionLabel, duration ?? TimeSpan.FromSeconds(onClick != null ? 8 : 5));
            toast.Closed += (s, e) =>
            {
                Open.Remove(toast);
                Restack();
            };
            Open.Add(toast);
            toast.Show();
            Restack();
            AppLog.Write($"Message: {title}: {message}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Showing a message", ex);
        }
    }

    /// <summary>Newest at the bottom right of the main screen's work area, older ones above it.</summary>
    private static void Restack()
    {
        var area = SystemParameters.WorkArea;
        double bottom = area.Bottom - 6;
        for (int i = Open.Count - 1; i >= 0; i--)
        {
            var t = Open[i];
            t.UpdateLayout();
            double h = t.ActualHeight > 0 ? t.ActualHeight : 110;
            t.Left = area.Right - t.Width - 6;
            t.MoveTo(bottom - h);
            bottom -= h - 4;
        }
    }
}

public partial class ToastWindow : Window
{
    private readonly Action? _onClick;
    private readonly DispatcherTimer _life;
    private bool _closing;

    internal ToastWindow(string title, string message, string icon, NoteSkin? skin, ToastTone tone, Action? onClick, string? actionLabel, TimeSpan duration)
    {
        InitializeComponent();
        SkinMenus.ApplyToPanel(this, skin);
        TxtToastTitle.Text = title;
        TxtToastMessage.Text = message;
        ToastIcon.IconKey = icon;
        _onClick = onClick;
        if (tone == ToastTone.Warning)
        {
            ToastIcon.SetResourceReference(LucideIcon.ForegroundProperty, "MenuDangerBrush");
            ToneBar.SetResourceReference(BackgroundProperty, "MenuDangerBrush");
        }
        if (actionLabel != null && onClick != null)
        {
            BtnAction.Content = actionLabel;
            BtnAction.Visibility = Visibility.Visible;
        }
        BtnClose.ToolTip = LocalizationService.T("Toast_Close");

        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 6;
        Top = area.Bottom - 120;
        Opacity = 0;

        _life = new DispatcherTimer { Interval = duration };
        _life.Tick += (s, e) => FadeOut();
        MouseEnter += (s, e) => _life.Stop();
        MouseLeave += (s, e) => { _life.Stop(); _life.Start(); };
        SourceInitialized += (s, e) =>
        {
            // never takes the focus, never in Alt+Tab
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = Win32Api.GetWindowLong(hwnd, Win32Api.GWL_EXSTYLE);
            Win32Api.SetWindowLong(hwnd, Win32Api.GWL_EXSTYLE, ex | Win32Api.WS_EX_NOACTIVATE | Win32Api.WS_EX_TOOLWINDOW);
        };
        Loaded += (s, e) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)).AtFps(30));
            _life.Start();
        };
    }

    /// <summary>Slides to a new height in the stack.</summary>
    internal void MoveTo(double top)
    {
        if (!IsLoaded)
        {
            Top = top;
            return;
        }
        BeginAnimation(TopProperty, new DoubleAnimation(top, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase() }.AtFps(30));
    }

    private void FadeOut()
    {
        if (_closing) return;
        _closing = true;
        _life.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)).AtFps(30);
        fade.Completed += (s, e) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    internal void CloseNow()
    {
        _closing = true;
        _life.Stop();
        Close();
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && IsInside(d, BtnClose)) return;
        RunAction();
    }

    private void BtnAction_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RunAction();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        FadeOut();
    }

    private void RunAction()
    {
        try { _onClick?.Invoke(); } catch (Exception ex) { AppLog.Error("Message action", ex); }
        FadeOut();
    }

    private static bool IsInside(DependencyObject d, DependencyObject container)
    {
        for (var x = d; x != null; x = VisualTreeHelper.GetParent(x)) if (x == container) return true;
        return false;
    }
}
