namespace CheckInReminder;

public static class ReminderEdgeSelector
{
    public static ScreenEdge Select(ReminderCharacter character, Random random)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(random);

        var allowedEdges = character.AllowedEdges;
        if (allowedEdges.Count == 0)
        {
            throw new InvalidOperationException($"角色 {character.Id} 没有可用的提醒出现方向。");
        }

        return allowedEdges[random.Next(allowedEdges.Count)];
    }
}
