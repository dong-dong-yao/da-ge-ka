namespace CheckInReminder;

public sealed class AppSettings
{
    public TimeOnly MorningStart { get; set; } = new(9, 30);

    public TimeOnly MorningEnd { get; set; } = new(10, 0);

    public int MorningIntervalMinutes { get; set; } = 5;

    public TimeOnly EveningStart { get; set; } = new(19, 0);

    public int EveningIntervalMinutes { get; set; } = 10;

    public bool AutoStart { get; set; } = true;

    public bool BreakReminderEnabled { get; set; }

    public TimeOnly BreakStart { get; set; } = new(9, 0);

    public TimeOnly BreakEnd { get; set; } = new(18, 0);

    public int BreakIntervalMinutes { get; set; } = 60;

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() => new()
    {
        MorningStart = MorningStart,
        MorningEnd = MorningEnd,
        MorningIntervalMinutes = MorningIntervalMinutes,
        EveningStart = EveningStart,
        EveningIntervalMinutes = EveningIntervalMinutes,
        AutoStart = AutoStart,
        BreakReminderEnabled = BreakReminderEnabled,
        BreakStart = BreakStart,
        BreakEnd = BreakEnd,
        BreakIntervalMinutes = BreakIntervalMinutes,
    };

}
