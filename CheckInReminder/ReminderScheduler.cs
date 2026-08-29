namespace CheckInReminder;

public enum ReminderKind
{
    Morning,
    Evening,
    Break,
    Test,
}

internal sealed class ReminderScheduler : IDisposable
{
    private readonly System.Windows.Forms.Timer timer;
    private readonly Func<bool> morningIsCompleted;
    private readonly Func<bool> eveningIsCompleted;
    private readonly Action<ReminderKind> requestReminder;
    private readonly Action<DateTime> onTick;
    private AppSettings settings;
    private DateTime? nextMorningDue;
    private DateTime? nextEveningDue;
    private DateTime? nextBreakDue;
    private bool started;

    public ReminderScheduler(
        AppSettings settings,
        Func<bool> morningIsCompleted,
        Func<bool> eveningIsCompleted,
        Action<ReminderKind> requestReminder,
        Action<DateTime> onTick)
    {
        this.settings = settings.Clone();
        this.morningIsCompleted = morningIsCompleted;
        this.eveningIsCompleted = eveningIsCompleted;
        this.requestReminder = requestReminder;
        this.onTick = onTick;

        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => Tick(DateTime.Now);
    }

    public void Start(DateTime now)
    {
        if (started)
        {
            return;
        }

        started = true;
        Recalculate(now, requestImmediate: true);
        onTick(now);
        timer.Start();
    }

    public void ApplySettings(AppSettings newSettings, DateTime now)
    {
        settings = newSettings.Clone();
        Recalculate(now, requestImmediate: false);
        onTick(now);
    }

    public void HandleResumeOrUnlock(DateTime now)
    {
        Recalculate(now, requestImmediate: true);
        onTick(now);
    }

    public void CompletionChanged(DateTime now)
    {
        Recalculate(now, requestImmediate: false);
        onTick(now);
    }

    private void Tick(DateTime now)
    {
        if (!morningIsCompleted() && nextMorningDue is { } morningDue && now >= morningDue)
        {
            if (ScheduleCalculator.IsInMorningWindow(now, settings.MorningStart, settings.MorningEnd))
            {
                requestReminder(ReminderKind.Morning);
                nextMorningDue = ScheduleCalculator.GetNextMorningDue(
                    now,
                    settings.MorningStart,
                    settings.MorningEnd,
                    settings.MorningIntervalMinutes);
            }
            else
            {
                nextMorningDue = null;
            }
        }

        if (!eveningIsCompleted() && nextEveningDue is { } eveningDue && now >= eveningDue)
        {
            if (ScheduleCalculator.IsInEveningWindow(now, settings.EveningStart))
            {
                requestReminder(ReminderKind.Evening);
                nextEveningDue = ScheduleCalculator.GetNextEveningDue(
                    now,
                    settings.EveningStart,
                    settings.EveningIntervalMinutes);
            }
            else
            {
                nextEveningDue = ScheduleCalculator.GetNextEveningDue(
                    now,
                    settings.EveningStart,
                    settings.EveningIntervalMinutes);
            }
        }

        if (settings.BreakReminderEnabled && nextBreakDue is { } breakDue && now >= breakDue)
        {
            if (ScheduleCalculator.IsInBreakWindow(now, settings.BreakStart, settings.BreakEnd))
            {
                requestReminder(ReminderKind.Break);
            }

            nextBreakDue = ScheduleCalculator.GetNextDailyBreakDue(
                now,
                settings.BreakStart,
                settings.BreakEnd,
                settings.BreakIntervalMinutes);
        }

        onTick(now);
    }

    private void Recalculate(DateTime now, bool requestImmediate)
    {
        if (morningIsCompleted())
        {
            nextMorningDue = null;
        }
        else
        {
            var inMorningWindow = ScheduleCalculator.IsInMorningWindow(
                now,
                settings.MorningStart,
                settings.MorningEnd);
            if (requestImmediate && inMorningWindow)
            {
                requestReminder(ReminderKind.Morning);
            }

            nextMorningDue = TimeOnly.FromDateTime(now) < settings.MorningStart || inMorningWindow
                ? ScheduleCalculator.GetNextMorningDue(
                    now,
                    settings.MorningStart,
                    settings.MorningEnd,
                    settings.MorningIntervalMinutes)
                : null;
        }

        if (eveningIsCompleted())
        {
            nextEveningDue = null;
        }
        else
        {
            var inEveningWindow = ScheduleCalculator.IsInEveningWindow(now, settings.EveningStart);
            if (requestImmediate && inEveningWindow)
            {
                requestReminder(ReminderKind.Evening);
            }

            nextEveningDue = ScheduleCalculator.GetNextEveningDue(
                now,
                settings.EveningStart,
                settings.EveningIntervalMinutes);
        }

        if (settings.BreakReminderEnabled)
        {
            if (requestImmediate &&
                ScheduleCalculator.IsAtBreakStartAnchor(now, settings.BreakStart))
            {
                requestReminder(ReminderKind.Break);
            }

            nextBreakDue = ScheduleCalculator.GetNextDailyBreakDue(
                now,
                settings.BreakStart,
                settings.BreakEnd,
                settings.BreakIntervalMinutes);
        }
        else
        {
            nextBreakDue = null;
        }
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
    }
}
