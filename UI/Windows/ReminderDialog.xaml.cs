using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartNotes.Core.Models;
using SmartNotes.Core.Native;
using SmartNotes.Core.Services;

namespace SmartNotes.UI.Windows;

/// <summary>"Remind Me > Pick a Time…": day, time (5-minute steps) and how often, with a line saying when it will ring.</summary>
public partial class ReminderDialog : Window
{
    public enum Outcome { Cancelled, Set, Removed }

    public Outcome Result { get; private set; } = Outcome.Cancelled;
    public NoteReminder? Reminder { get; private set; }

    private readonly DateTime _now;
    private bool _ready;

    public ReminderDialog(NoteReminder? current, DateTime now, NoteSkin? skin = null)
    {
        InitializeComponent();
        Controls.SkinMenus.ApplyToPanel(this, skin); // in the note's theme
        _now = now;

        TxtTitle.Text = LocalizationService.T("Reminder_Title");
        TxtDayLabel.Text = LocalizationService.T("Reminder_Day");
        TxtTimeLabel.Text = LocalizationService.T("Reminder_Time");
        TxtRepeatLabel.Text = LocalizationService.T("Reminder_Repeat");
        TxtRemove.Text = LocalizationService.T("Reminder_RemoveShort"); // short: room next to Cancel / Set in wide theme fonts
        BtnRemove.ToolTip = LocalizationService.T("Reminder_Remove");
        BtnCancel.Content = LocalizationService.T("MsgBox_Cancel");
        BtnSet.Content = LocalizationService.T("Reminder_Set");
        BtnRemove.Visibility = current != null ? Visibility.Visible : Visibility.Collapsed;

        // what to start from: the current reminder, or the next full quarter hour at least 30 minutes away
        DateTime start = current?.At ?? NoteSchedule.NextDue(current) ?? RoundUp(now.AddMinutes(30), 15);

        var culture = System.Globalization.CultureInfo.GetCultureInfo(LocalizationService.Instance.CurrentLanguage == "de" ? "de-DE" : "en-US");
        int days = Math.Max(14, (start.Date - now.Date).Days + 1);
        for (int i = 0; i < days; i++)
        {
            var day = now.Date.AddDays(i);
            string label = i == 0 ? LocalizationService.T("Reminder_Today")
                : i == 1 ? LocalizationService.T("Reminder_TomorrowDay")
                : day.ToString("dddd, d MMM", culture);
            CmbDay.Items.Add(new ComboBoxItem { Content = label, Tag = day });
        }
        for (int h = 0; h < 24; h++) CmbHour.Items.Add(new ComboBoxItem { Content = h.ToString("00"), Tag = h });
        var minutes = Enumerable.Range(0, 12).Select(m => m * 5).ToList();
        if (!minutes.Contains(start.Minute)) minutes.Add(start.Minute);
        foreach (int m in minutes.OrderBy(m => m)) CmbMinute.Items.Add(new ComboBoxItem { Content = m.ToString("00"), Tag = m });
        foreach (ReminderRepeat r in Enum.GetValues<ReminderRepeat>()) CmbRepeat.Items.Add(new ComboBoxItem { Content = ReminderText.Repeat(r), Tag = r });

        Select(CmbDay, (object)Max(start.Date, now.Date));
        Select(CmbHour, start.Hour);
        Select(CmbMinute, start.Minute);
        Select(CmbRepeat, current?.Repeat ?? ReminderRepeat.None);

        _ready = true;
        UpdateSummary();
        Loaded += (s, e) =>
        {
            WindowBlurHelper.ApplyModernWindowStyles(this);
            BtnSet.Focus();
        };
        KeyDown += OnKeyDown;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime RoundUp(DateTime t, int minutes)
    {
        var rounded = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0).AddMinutes(Math.Ceiling(t.Minute / (double)minutes) * minutes);
        return rounded < t ? rounded.AddMinutes(minutes) : rounded;
    }

    private static void Select(ComboBox box, object tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, tag)) { box.SelectedItem = item; return; }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static T TagOf<T>(ComboBox box, T fallback) => box.SelectedItem is ComboBoxItem { Tag: T value } ? value : fallback;

    private ReminderRepeat ChosenRepeat => TagOf(CmbRepeat, ReminderRepeat.None);

    /// <summary>The time it will ring: a time that has already passed today moves to the next day (or the next repeat).</summary>
    private DateTime EffectiveTime()
    {
        var day = TagOf(CmbDay, _now.Date);
        var at = day.AddHours(TagOf(CmbHour, 9)).AddMinutes(TagOf(CmbMinute, 0));
        var repeat = ChosenRepeat;
        at = NoteSchedule.FirstOccurrence(at, repeat);
        if (at <= _now) at = repeat == ReminderRepeat.None ? at.AddDays(1) : NoteSchedule.NextOccurrence(at, repeat, _now);
        return at;
    }

    private void Inputs_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) UpdateSummary();
    }

    private void UpdateSummary()
    {
        var reminder = new NoteReminder { At = EffectiveTime(), Repeat = ChosenRepeat };
        TxtSummary.Text = LocalizationService.T("Reminder_Summary", ReminderText.Describe(reminder, _now));
    }

    private void BtnSet_Click(object sender, RoutedEventArgs e)
    {
        var at = EffectiveTime();
        Reminder = new NoteReminder { At = at, Repeat = ChosenRepeat, Anchor = ChosenRepeat == ReminderRepeat.None ? null : at };
        Result = Outcome.Set;
        Close();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        Result = Outcome.Removed;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Enter && !(e.OriginalSource is ComboBox { IsDropDownOpen: true })) BtnSet_Click(BtnSet, new RoutedEventArgs());
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }
}
