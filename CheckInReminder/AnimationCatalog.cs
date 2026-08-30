namespace CheckInReminder;

public static class AnimationCatalog
{
    public const string DefaultCharacterId = "white-bear";
    public static readonly TimeSpan EdgeDuration = TimeSpan.FromSeconds(7.104);
    public static readonly TimeSpan GateDuration = TimeSpan.FromSeconds(4.086009);
    public static bool EdgeLoops => false;
    public static bool GateLoops => true;

    public static IReadOnlyList<ReminderCharacter> Characters { get; } =
    [
        new(DefaultCharacterId, "白熊", "Edge", EdgeDuration, EdgeLoops),
    ];

    public static ReminderCharacter? FindCharacter(string? id) =>
        Characters.FirstOrDefault(character =>
            string.Equals(character.Id, id, StringComparison.Ordinal));
}
