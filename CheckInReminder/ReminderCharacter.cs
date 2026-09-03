namespace CheckInReminder;

/// <summary>
/// 一个可提醒角色。提醒动画序列由 <see cref="SequenceName"/> 指向嵌入资源目录；
/// 桌面宠物素材按约定从 <see cref="Id"/> 派生命名（{Id}-pet-idle / {Id}-pet-tap），
/// 是否就绪由 <see cref="AnimationCatalog"/> 在启动时探测嵌入资源后填充。
/// </summary>
public sealed record ReminderCharacter(
    string Id,
    string DisplayName,
    string SequenceName,
    TimeSpan Duration,
    bool Loop)
{
    public string PetIdleSequenceName => $"{Id}-pet-idle";

    public string PetTapSequenceName => $"{Id}-pet-tap";

    /// <summary>宠物素材（待机 + 敲击）是否已嵌入。由目录探测填充，默认 false。</summary>
    public bool HasPetAssets { get; init; }

    /// <summary>宠物待机动画一轮的时长（素材到位后按素材节奏调整）。</summary>
    public TimeSpan PetIdleDuration { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>宠物敲击动画一轮（左右爪各一次）的时长。</summary>
    public TimeSpan PetTapDuration { get; init; } = TimeSpan.FromMilliseconds(480);
}
