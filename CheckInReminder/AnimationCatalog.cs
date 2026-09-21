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
    public static IReadOnlyList<ReminderCharacter> AllCharacters { get; private set; } = Characters;
    public static IReadOnlyList<string> CustomLoadErrors { get; private set; } = [];

    public static void RefreshCustomCharacters()
    {
        var store = new CustomCharacterStore();
        AllCharacters = Characters.Concat(store.Load()).ToArray();
        CustomLoadErrors = store.Errors.ToArray();
    }

    // 新增角色只需在此注册一行（素材放入 Assets/Animations/{角色Id}/ 目录）：
    // new("shiba", "柴犬", "shiba", TimeSpan.FromSeconds(7.1), false),
    private static IReadOnlyList<ReminderCharacter> BuildCharacters()
    {
        ReminderCharacter[] declared =
        [
            new(DefaultCharacterId, "白熊", "Edge", EdgeDuration, EdgeLoops),
            new("yellow-hippo", "黄色河马", "yellow-hippo", EdgeDuration, false),
            new("blue-hat-cat", "蓝帽小猫", "blue-hat-cat", EdgeDuration, false)
            {
                SourceEdge = ScreenEdge.Bottom,
            },
            new("stick-dog", "持棒小狗", "stick-dog", EdgeDuration, false)
            {
                SourceEdge = ScreenEdge.Left,
                AllowedEdges = Array.AsReadOnly(new[] { ScreenEdge.Left, ScreenEdge.Right }),
            },
            new("scooter-dinosaur", "滑板绿恐龙", "scooter-dinosaur", EdgeDuration, false)
            {
                SourceEdge = ScreenEdge.Left,
                AllowedEdges = Array.AsReadOnly(new[] { ScreenEdge.Left, ScreenEdge.Right }),
            },
        ];
        return declared
            .Select(character => character with
            {
                HasPetAssets = HasDesktopPetResources(character.Id)
                    || HasSequenceResources(character.PetIdleSequenceName),
            })
            .ToArray();
    }

    /// <summary>判断指定动画序列名的嵌入资源是否存在（至少一帧）。</summary>
    public static bool HasSequenceResources(string sequenceName)
    {
        var resourceSequenceName = sequenceName.Replace('-', '_');
        var prefix = $"CheckInReminder.Assets.Animations.{resourceSequenceName}.frame_";
        return Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Any(name => name.StartsWith(prefix, StringComparison.Ordinal) &&
                name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasDesktopPetResources(string characterId) =>
        DesktopPetSpriteSet.HasResources(characterId);

    public static ReminderCharacter? FindCharacter(string? id) =>
        AllCharacters.FirstOrDefault(character =>
            string.Equals(character.Id, id, StringComparison.Ordinal));
}
