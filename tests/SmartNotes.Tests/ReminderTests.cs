using SmartNotes.Core.Models;
using SmartNotes.Core.Services;
using static SmartNotes.Tests.AppDriver;

namespace SmartNotes.Tests;

/// <summary>Reminders and "Repeat Daily" checklists: the time rules, and the real app ringing and resetting.</summary>
public static class ReminderTests
{
    private static readonly DateTime Fri = new(2026, 11, 27, 9, 0, 0); // a Friday

    [Test]
    public static void Reminder_OneOff_IsUsedUpWhenItRings()
    {
        var r = new NoteReminder { At = Fri };
        Assert.False(NoteSchedule.IsDue(r, Fri.AddMinutes(-1)), "not yet");
        Assert.True(NoteSchedule.IsDue(r, Fri), "due on the minute");
        Assert.Equal(null, NoteSchedule.AfterFiring(r, Fri), "nothing left afterwards");
    }

    [Test]
    public static void Reminder_Repeating_MovesOn_AndSkipsWhatWasMissed()
    {
        var daily = new NoteReminder { At = Fri, Repeat = ReminderRepeat.Daily, Anchor = Fri };
        Assert.Equal(Fri.AddDays(1), NoteSchedule.AfterFiring(daily, Fri)!.At, "daily: the next day, same time");
        // the PC was off for three days: it rings once, then the next time is in the future
        Assert.Equal(Fri.AddDays(4), NoteSchedule.AfterFiring(daily, Fri.AddDays(3).AddHours(2))!.At, "missed days are skipped");

        var weekdays = new NoteReminder { At = Fri, Repeat = ReminderRepeat.Weekdays, Anchor = Fri };
        Assert.Equal(Fri.AddDays(3), NoteSchedule.AfterFiring(weekdays, Fri)!.At, "Monday to Friday: Friday goes on to Monday");

        var weekly = new NoteReminder { At = Fri, Repeat = ReminderRepeat.Weekly, Anchor = Fri };
        Assert.Equal(Fri.AddDays(14), NoteSchedule.AfterFiring(weekly, Fri.AddDays(8))!.At, "weekly, a week missed");
    }

    [Test]
    public static void Reminder_Monthly_KeepsTheDayOfTheMonth()
    {
        var jan31 = new DateTime(2027, 1, 31, 8, 0, 0);
        var r = new NoteReminder { At = jan31, Repeat = ReminderRepeat.Monthly, Anchor = jan31 };
        r = NoteSchedule.AfterFiring(r, jan31)!;
        Assert.Equal(new DateTime(2027, 2, 28, 8, 0, 0), r.At, "February has no 31st: its last day");
        r = NoteSchedule.AfterFiring(r, r.At!.Value)!;
        Assert.Equal(new DateTime(2027, 3, 31, 8, 0, 0), r.At, "and March is back on the 31st");
    }

    [Test]
    public static void Reminder_Snooze_RingsOnceMore_WithoutTouchingTheSchedule()
    {
        var r = new NoteReminder { At = Fri.AddDays(1), Repeat = ReminderRepeat.Daily, Anchor = Fri, SnoozeUntil = Fri.AddMinutes(10) };
        Assert.Equal(Fri.AddMinutes(10), NoteSchedule.NextDue(r), "the snooze comes first");
        var after = NoteSchedule.AfterFiring(r, Fri.AddMinutes(10))!;
        Assert.Equal(null, after.SnoozeUntil, "snooze used up");
        Assert.Equal(Fri.AddDays(1), after.At, "tomorrow's time unchanged");

        var oneOffSnoozed = new NoteReminder { SnoozeUntil = Fri };
        Assert.Equal(null, NoteSchedule.AfterFiring(oneOffSnoozed, Fri), "a snoozed one-off is gone after ringing again");
    }

    [Test]
    public static void Reminder_IsSavedWithTheNote()
    {
        var storage = new NoteStorageService();
        var note = new NoteItem { Title = "Call", Reminder = new NoteReminder { At = Fri, Repeat = ReminderRepeat.Weekdays, Anchor = Fri }, RepeatDaily = true, Streak = 4, DailyDay = Fri.Date };
        storage.AddNote(note);
        var back = new NoteStorageService().Notes.Single(n => n.Id == note.Id);
        Assert.Equal(Fri, back.Reminder?.At, "time");
        Assert.Equal(ReminderRepeat.Weekdays, back.Reminder?.Repeat, "repeat");
        Assert.True(back.RepeatDaily, "repeat daily");
        Assert.Equal(4, back.Streak, "streak");
        Assert.Equal(Fri.Date, back.DailyDay, "day of the ticks");
    }

    private static NoteItem Daily(DateTime day, int streak, params bool[] done) => new()
    {
        ViewMode = NoteViewMode.Checklist,
        RepeatDaily = true,
        DailyDay = day,
        Streak = streak,
        Checklist = done.Select((d, i) => new TodoCheckItem { Text = "task " + i, IsDone = d }).ToList(),
    };

    [Test]
    public static void Daily_NewDay_UnticksEverything_AndCountsTheStreak()
    {
        var note = Daily(Fri.Date, 2, true, true);
        Assert.True(NoteSchedule.ResetDaily(note, Fri.Date.AddDays(1).AddHours(7), startHour: 4), "a new day started");
        Assert.False(note.Checklist.Any(t => t.IsDone), "every task unticked");
        Assert.Equal(3, note.Streak, "yesterday was complete: streak 2 → 3");
        Assert.Equal("task 0|task 1", string.Join("|", note.Checklist.Select(t => t.Text)), "tasks kept");

        var open = Daily(Fri.Date, 5, true, false);
        NoteSchedule.ResetDaily(open, Fri.Date.AddDays(1).AddHours(7), 4);
        Assert.Equal(0, open.Streak, "a task was left open: the streak starts again");

        var skipped = Daily(Fri.Date, 5, true, true);
        NoteSchedule.ResetDaily(skipped, Fri.Date.AddDays(3).AddHours(7), 4);
        Assert.Equal(0, skipped.Streak, "whole days in between without the list: starts again");
    }

    [Test]
    public static void Daily_TheDayStartsAtFourInTheMorning()
    {
        var note = Daily(Fri.Date, 0, true);
        Assert.False(NoteSchedule.ResetDaily(note, Fri.Date.AddDays(1).AddHours(2), 4), "2 am still counts for Friday");
        Assert.True(note.Checklist.Single().IsDone, "ticks kept overnight");
        Assert.Equal(Fri.Date.AddDays(1).AddHours(4), NoteSchedule.NextDayStart(Fri.Date.AddDays(1).AddHours(2), 4), "the next day starts at 4:00");
        Assert.Equal(1, NoteSchedule.ShownStreak(note), "today complete counts as a day already");
    }

    // ---------------------------------------------------------------- end-to-end

    private static NoteItem Note(string title) => new() { Title = title, X = 140, Y = 160, Width = 360, Height = 320 };

    [Test(E2E = true)]
    public static void Reminder_MissedWhileOff_RingsAtStart_AndSnoozes()
    {
        var note = Note("Call the bank");
        note.Reminder = new NoteReminder { At = DateTime.Now.AddMinutes(-5) };
        using var app = Start(nameof(Reminder_MissedWhileOff_RingsAtStart_AndSnoozes), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        var snooze = Find(w, "BtnReminderSnooze");
        Assert.True(!snooze.Current.IsOffscreen, "the reminder banner is showing");
        Assert.Contains("Reminder", Find(w, "TxtReminderBanner").Current.Name, "it says so");
        Assert.Eventually(() => app.ReadNotes().Single().Reminder == null, "the one-off reminder is used up and saved");

        Invoke(snooze);
        Assert.Eventually(() => app.ReadNotes().Single().Reminder?.SnoozeUntil is { } t && Math.Abs((t - DateTime.Now.AddMinutes(10)).TotalMinutes) < 1,
            "snoozed for 10 minutes");
        Assert.Eventually(() => TryFind(w, "BtnReminderSnooze") is not { } b || b.Current.IsOffscreen, "banner gone");
        Assert.True(Find(w, "TxtReminderBadge").Current.Name.Length > 0, "the footer shows the snoozed time");
    }

    [Test(E2E = true)]
    public static void Reminder_IsSetChangedAndRemovedFromTheMenu()
    {
        using var app = Start(nameof(Reminder_IsSetChangedAndRemovedFromTheMenu), new[] { Note("Tea") });
        var w = app.WaitForNoteWindows(1)[0];

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("Remind Me");
        Invoke(app.FindMenuItem("Pick a Time"));
        System.Windows.Automation.AutomationElement? dialog = null;
        Assert.Eventually(() => (dialog = app.WindowContaining("BtnSet")) != null, "the Pick a Time dialog");
        Assert.Contains("Reminds you", Find(dialog!, "TxtSummary").Current.Name, "the dialog says when it will ring");
        Invoke(Find(dialog!, "BtnSet"));
        Assert.Eventually(() => app.ReadNotes().Single().Reminder?.At > DateTime.Now, "a reminder in the future is saved");

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("Remind Me");
        Invoke(app.FindMenuItem("In 1 Hour"));
        Assert.Eventually(() => app.ReadNotes().Single().Reminder?.At is { } at && Math.Abs((at - DateTime.Now.AddHours(1)).TotalMinutes) < 2, "in one hour");
        Assert.Eventually(() => Find(w, "TxtReminderBadge").Current.Name.Contains(':'), "the footer shows the time");

        Invoke(Find(w, "BtnMore"));
        app.ExpandMenuItem("Remind Me");
        Invoke(app.FindMenuItem("Remove Reminder"));
        Assert.Eventually(() => app.ReadNotes().Single().Reminder == null, "removed");
    }

    [Test(E2E = true)]
    public static void DailyChecklist_IsUntickedOnANewDay_AndShowsTheStreak()
    {
        var note = Note("Morning");
        note.ViewMode = NoteViewMode.Checklist;
        note.RepeatDaily = true;
        note.Streak = 2;
        note.DailyDay = NoteSchedule.DayOf(DateTime.Now, 4).AddDays(-1); // the ticks are from yesterday
        note.Checklist = new() { new TodoCheckItem { Text = "water plants", IsDone = true }, new TodoCheckItem { Text = "stretch", IsDone = true } };
        using var app = Start(nameof(DailyChecklist_IsUntickedOnANewDay_AndShowsTheStreak), new[] { note });
        var w = app.WaitForNoteWindows(1)[0];

        Assert.Eventually(() =>
        {
            var saved = app.ReadNotes().Single();
            return saved.Checklist.All(t => !t.IsDone) && saved.Streak == 3 && saved.DailyDay == NoteSchedule.DayOf(DateTime.Now, 4);
        }, "unticked, streak 3, today's list");
        Assert.Equal("3", Find(w, "TxtStreak").Current.Name, "the footer shows the streak");
        Assert.Equal("0/2", Find(w, "TxtChecklistProgress").Current.Name, "nothing done yet today");
    }
}
