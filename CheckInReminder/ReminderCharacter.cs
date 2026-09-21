namespace CheckInReminder;

/// <summary>
/// 一个可提醒角色。提醒动画序列由 <see cref="SequenceName"/> 指向嵌入资源目录；
/// 桌面宠物素材按约定从 <see cref="Id"/> 派生完整姿势图或旧式动画序列名，
/// 是否就绪由 <see cref="AnimationCatalog"/> 在启动时探测嵌入资源后填充。
/// </summary>
public sealed record ReminderCharacter(
    string Id,
    string DisplayName,
    string SequenceName,
    TimeSpan Duration,
    bool Loop)
{
    public string? CustomPackagePath { get; init; }
    public CustomCharacterManifest? CustomManifest { get; init; }
    private static readonly IReadOnlyList<ScreenEdge> EveryEdge = Array.AsReadOnly(
        new[] { ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Right, ScreenEdge.Bottom });

    public IReadOnlyList<ScreenEdge> AllowedEdges { get; init; } = EveryEdge;

    public ScreenEdge SourceEdge { get; init; } = ScreenEdge.Right;

    public string PetIdleSequenceName => $"{Id}-pet-idle";

    public string PetTapSequenceName => $"{Id}-pet-tap";

    /// <summary>宠物素材（精灵姿势或待机 + 敲击序列）是否已嵌入。</summary>
    public bool HasPetAssets { get; init; }

    /// <summary>宠物待机动画一轮的时长（素材到位后按素材节奏调整）。</summary>
    public TimeSpan PetIdleDuration { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>宠物敲击动画一轮（左右爪各一次）的时长。</summary>
    public TimeSpan PetTapDuration { get; init; } = TimeSpan.FromMilliseconds(480);
}
