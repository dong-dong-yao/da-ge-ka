namespace CheckInReminder;

public sealed class AppSettings
{
    public TimeOnly MorningStart { get; set; } = new(9, 30);

    public TimeOnly MorningEnd { get; set; } = new(10, 0);

    public int MorningIntervalMinutes { get; set; } = 5;

    public TimeOnly EveningStart { get; set; } = new(19, 0);

    public int EveningIntervalMinutes { get; set; } = 10;

    public bool AutoStart { get; set; } = true;

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() => new()
    {
        MorningStart = MorningStart,
        MorningEnd = MorningEnd,
        MorningIntervalMinutes = MorningIntervalMinutes,
        EveningStart = EveningStart,
        EveningIntervalMinutes = EveningIntervalMinutes,
        AutoStart = AutoStart,
    };

}
