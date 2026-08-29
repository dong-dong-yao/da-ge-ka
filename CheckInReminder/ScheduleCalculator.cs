namespace CheckInReminder;

public static class ScheduleCalculator
{
    public static bool IsInMorningWindow(DateTime now, TimeOnly start, TimeOnly end)
    {
        var current = TimeOnly.FromDateTime(now);
        return current >= start && current < end;
    }

    public static bool IsInEveningWindow(DateTime now, TimeOnly start) =>
        TimeOnly.FromDateTime(now) >= start;

    public static bool IsInBreakWindow(DateTime now, TimeOnly start, TimeOnly end)
    {
        var current = TimeOnly.FromDateTime(now);
        return current >= start && current < end;
    }

    public static DateTime? GetNextMorningDue(
        DateTime now,
        TimeOnly start,
        TimeOnly end,
        int intervalMinutes)
    {
        var candidate = GetNextFixedDue(now, start, intervalMinutes);
        var endBoundary = now.Date.Add(end.ToTimeSpan());
        return candidate < endBoundary ? candidate : null;
    }

    public static DateTime GetNextEveningDue(DateTime now, TimeOnly start, int intervalMinutes) =>
        GetNextFixedDue(now, start, intervalMinutes);

    public static DateTime? GetNextBreakDue(
        DateTime now,
        TimeOnly start,
        TimeOnly end,
        int intervalMinutes)
    {
        var candidate = GetNextFixedDue(now, start, intervalMinutes);
        var endBoundary = now.Date.Add(end.ToTimeSpan());
        return candidate < endBoundary ? candidate : null;
    }

    public static DateTime GetNextDailyBreakDue(
        DateTime now,
        TimeOnly start,
        TimeOnly end,
        int intervalMinutes) =>
        GetNextBreakDue(now, start, end, intervalMinutes) ??
        now.Date.AddDays(1).Add(start.ToTimeSpan());

    public static bool IsAtBreakStartAnchor(DateTime now, TimeOnly start) =>
        TimeOnly.FromDateTime(now) == start;

    private static DateTime GetNextFixedDue(DateTime now, TimeOnly start, int intervalMinutes)
    {
        if (intervalMinutes is < 1 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMinutes));
        }

        var anchor = now.Date.Add(start.ToTimeSpan());
        if (now < anchor)
        {
            return anchor;
        }

        var elapsedTicks = now.Ticks - anchor.Ticks;
        var intervalTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
        var completedIntervals = elapsedTicks / intervalTicks;
        return anchor.AddTicks((completedIntervals + 1) * intervalTicks);
    }
}
