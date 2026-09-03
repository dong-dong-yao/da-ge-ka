using System.Reflection;

namespace CheckInReminder;

public static class AnimationCatalog
{
    public const string DefaultCharacterId = "white-bear";
    public static readonly TimeSpan EdgeDuration = TimeSpan.FromSeconds(7.104);
    public static readonly TimeSpan GateDuration = TimeSpan.FromSeconds(4.086009);
    public static bool EdgeLoops => false;
    public static bool GateLoops => true;

    public static IReadOnlyList<ReminderCharacter> Characters { get; } = BuildCharacters();

    // 新增角色只需在此注册一行（素材放入 Assets/Animations/{角色Id}/ 目录）：
    // new("shiba", "柴犬", "shiba", TimeSpan.FromSeconds(7.1), false),
    private static IReadOnlyList<ReminderCharacter> BuildCharacters()
    {
        ReminderCharacter[] declared =
        [
            new(DefaultCharacterId, "白熊", "Edge", EdgeDuration, EdgeLoops),
        ];
        return declared
            .Select(character => character with
            {
                HasPetAssets = HasSequenceResources(character.PetIdleSequenceName),
            })
            .ToArray();
    }

    /// <summary>判断指定动画序列名的嵌入资源是否存在（至少一帧）。</summary>
    public static bool HasSequenceResources(string sequenceName)
    {
        var prefix = $"CheckInReminder.Assets.Animations.{sequenceName}.frame_";
        return Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Any(name => name.StartsWith(prefix, StringComparison.Ordinal) &&
                name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    public static ReminderCharacter? FindCharacter(string? id) =>
        Characters.FirstOrDefault(character =>
            string.Equals(character.Id, id, StringComparison.Ordinal));
}
