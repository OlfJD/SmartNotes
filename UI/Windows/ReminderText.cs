using System;
using System.Globalization;
using SmartNotes.Core.Models;
using SmartNotes.Core.Services;

namespace SmartNotes.UI.Windows;

/// <summary>How reminder times read in the menu, the dialog, the footer and the banner (English or German).</summary>
public static class ReminderText
{
    private static CultureInfo Culture => CultureInfo.GetCultureInfo(LocalizationService.Instance.CurrentLanguage == "de" ? "de-DE" : "en-US");

    public static string Time(DateTime at) => at.ToString("HH:mm", Culture);

    /// <summary>"Today 14:30", "Tomorrow 09:00", "Fri 28 Nov 09:00".</summary>
    public static string When(DateTime at, DateTime now)
    {
        if (at.Date == now.Date) return LocalizationService.T("History_Today", Time(at));
        if (at.Date == now.Date.AddDays(1)) return LocalizationService.T("Reminder_Tomorrow", Time(at));
        return at.ToString("ddd d MMM", Culture) + " " + Time(at);
    }

    /// <summary>For the footer badge: "14:30" today, "Fri 09:00" within a week, "28 Nov" later.</summary>
    public static string Short(DateTime at, DateTime now)
    {
        if (at.Date == now.Date) return Time(at);
        if (at.Date < now.Date.AddDays(7)) return at.ToString("ddd", Culture) + " " + Time(at);
        return at.ToString("d MMM", Culture);
    }

    public static string Repeat(ReminderRepeat repeat) => LocalizationService.T(repeat switch
    {
        ReminderRepeat.Daily => "Repeat_Daily",
        ReminderRepeat.Weekdays => "Repeat_Weekdays",
        ReminderRepeat.Weekly => "Repeat_Weekly",
        ReminderRepeat.Monthly => "Repeat_Monthly",
        _ => "Repeat_None"
    });

    /// <summary>"Today 14:30 · every day" (the next time it wants attention, snooze included).</summary>
    public static string Describe(NoteReminder r, DateTime now)
    {
        if (NoteSchedule.NextDue(r) is not { } due) return "";
        string text = When(due, now);
        if (r.Repeat != ReminderRepeat.None) text += " · " + Repeat(r.Repeat);
        return text;
    }
}
