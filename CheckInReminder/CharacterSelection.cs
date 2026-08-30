namespace CheckInReminder;

public sealed class CharacterSelection
{
    private readonly IReadOnlyList<ReminderCharacter> characters;
    private int selectedIndex;

    public CharacterSelection(IReadOnlyList<ReminderCharacter> characters, string? initialCharacterId)
    {
        ArgumentNullException.ThrowIfNull(characters);
        if (characters.Count == 0)
        {
            throw new ArgumentException("至少需要一个提醒角色。", nameof(characters));
        }

        this.characters = characters;
        selectedIndex = FindIndex(initialCharacterId);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        ConfirmedCharacterId = characters[selectedIndex].Id;
    }

    public bool CanBrowse => characters.Count > 1;

    public ReminderCharacter SelectedCharacter => characters[selectedIndex];

    public string ConfirmedCharacterId { get; private set; }

    public bool MoveNext() => Move(1);

    public bool MovePrevious() => Move(-1);

    public string Confirm()
    {
        ConfirmedCharacterId = SelectedCharacter.Id;
        return ConfirmedCharacterId;
    }

    private bool Move(int offset)
    {
        if (!CanBrowse)
        {
            return false;
        }

        selectedIndex = (selectedIndex + offset + characters.Count) % characters.Count;
        return true;
    }

    private int FindIndex(string? id)
    {
        for (var index = 0; index < characters.Count; index++)
        {
            if (string.Equals(characters[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
