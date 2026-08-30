namespace CheckInReminder;

public sealed record ReminderCharacter(
    string Id,
    string DisplayName,
    string SequenceName,
    TimeSpan Duration,
    bool Loop);
