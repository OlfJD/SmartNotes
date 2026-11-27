using System;
using System.Linq;

namespace SmartNotes.Core.Models;

public enum ReminderRepeat
{
    None,
    Daily,
    Weekdays, // Monday to Friday
    Weekly,
    Monthly
}

/// <summary>"Remind me" on a note: when, how often, and a snooze in between.</summary>
public class NoteReminder
{
    /// <summary>Next scheduled time; null once a one-off reminder has fired (only a snooze may be left).</summary>
    public DateTime? At { get; set; }
    public ReminderRepeat Repeat { get; set; } = ReminderRepeat.None;
    /// <summary>"Snooze 10 min": fires once more at this time without touching the schedule.</summary>
    public DateTime? SnoozeUntil { get; set; }
    /// <summary>The first time of a repeating series: later times are counted from it, so "the 31st" stays the 31st.</summary>
    public DateTime? Anchor { get; set; }
}

/// <summary>
/// Time rules for reminders and daily checklists, kept free of UI so they can be tested with any clock.
/// The app checks them at least once a minute, on wake-up from sleep and at startup (anything missed while the
/// PC was off fires once then).
/// </summary>
public static class NoteSchedule
{
    /// <summary>When a reminder next wants attention (the earlier of its time and its snooze), or null.</summary>
    public static DateTime? NextDue(NoteReminder? r)
    {
        if (r == null) return null;
        if (r.At is { } at && r.SnoozeUntil is { } snooze) return at < snooze ? at : snooze;
        return r.At ?? r.SnoozeUntil;
    }

    public static bool IsDue(NoteReminder? r, DateTime now) => NextDue(r) is { } due && due <= now;

    /// <summary>
    /// The reminder after it fired at <paramref name="now"/>: a snooze is used up; a scheduled time moves to its next
    /// occurrence after now (missed repeats are skipped, not fired one by one) or is gone for a one-off.
    /// Returns null when nothing is left.
    /// </summary>
    public static NoteReminder? AfterFiring(NoteReminder r, DateTime now)
    {
        var next = new NoteReminder { At = r.At, Repeat = r.Repeat, SnoozeUntil = r.SnoozeUntil, Anchor = r.Anchor };
        if (next.SnoozeUntil is { } snooze && snooze <= now) next.SnoozeUntil = null;
        if (next.At is { } at && at <= now)
        {
            next.At = next.Repeat == ReminderRepeat.None ? null : NextOccurrence(next.Anchor ?? at, next.Repeat, now);
        }
        return next.At == null && next.SnoozeUntil == null ? null : next;
    }

    /// <summary>The first time of the series that starts at <paramref name="start"/> which is after <paramref name="after"/>.</summary>
    public static DateTime NextOccurrence(DateTime start, ReminderRepeat repeat, DateTime after)
    {
        if (repeat == ReminderRepeat.None) return start;
        DateTime t = start;
        int months = 0;
        for (int guard = 0; t <= after && guard < 100000; guard++)
        {
            switch (repeat)
            {
                case ReminderRepeat.Daily:
                    t = t.AddDays(Math.Max(1, (int)Math.Floor((after - t).TotalDays)));
                    break;
                case ReminderRepeat.Weekdays:
                    t = t.AddDays(Math.Max(1, (int)Math.Floor((after - t).TotalDays) - 3)); // jump close, then day by day
                    while (t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) t = t.AddDays(1);
                    break;
                case ReminderRepeat.Weekly:
                    t = t.AddDays(7 * Math.Max(1, (int)Math.Floor((after - t).TotalDays / 7)));
                    break;
                case ReminderRepeat.Monthly:
                    // always counted from the start, so the 31st stays the 31st (or the month's last day)
                    t = start.AddMonths(++months);
                    break;
            }
        }
        return t;
    }

    /// <summary>A reminder for <paramref name="at"/> that repeats: lands on a weekday when the series is Mon–Fri.</summary>
    public static DateTime FirstOccurrence(DateTime at, ReminderRepeat repeat)
    {
        if (repeat == ReminderRepeat.Weekdays)
        {
            while (at.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) at = at.AddDays(1);
        }
        return at;
    }

    // ---------------------------------------------------------------- daily checklists

    /// <summary>The day a moment belongs to when days start at <paramref name="startHour"/> (ticks at 1 am count for the day before).</summary>
    public static DateTime DayOf(DateTime t, int startHour) => t.AddHours(-Math.Clamp(startHour, 0, 23)).Date;

    /// <summary>When the next day starts after <paramref name="now"/>.</summary>
    public static DateTime NextDayStart(DateTime now, int startHour) => DayOf(now, startHour).AddDays(1).AddHours(Math.Clamp(startHour, 0, 23));

    /// <summary>
    /// "Repeat daily": once a new day has started, every task is unticked. The streak counts days in a row on which
    /// every task was done; a day with an open task, or a day skipped entirely (PC off), starts it again from 0.
    /// Returns true when the note changed.
    /// </summary>
    public static bool ResetDaily(NoteItem note, DateTime now, int startHour)
    {
        if (!note.RepeatDaily) return false;
        var today = DayOf(now, startHour);
        if (note.DailyDay == null)
        {
            note.DailyDay = today;
            return true;
        }
        if (today <= note.DailyDay.Value) return false;

        bool allDone = AllDone(note);
        bool nextDay = today == note.DailyDay.Value.Date.AddDays(1);
        note.Streak = allDone && nextDay ? note.Streak + 1 : 0;
        note.Checklist = note.Checklist.Select(t => new TodoCheckItem { Id = t.Id, Text = t.Text, IsDone = false }).ToList();
        note.DailyDay = today;
        return true;
    }

    /// <summary>The streak as shown: the days in a row before today, plus today once every task is done.</summary>
    public static int ShownStreak(NoteItem note) => note.Streak + (AllDone(note) ? 1 : 0);

    private static bool AllDone(NoteItem note) => note.Checklist.Count > 0 && note.Checklist.All(t => t.IsDone);
}
