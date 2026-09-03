namespace CheckInReminder;

/// <summary>
/// 桌面宠物敲击状态机（纯逻辑）：待机 → 左爪 → 待机 → 右爪交替；
/// 静默超时后由调用方驱动 <see cref="OnSilence"/> 回到待机。
/// </summary>
public sealed class PetTapStateMachine
{
    public enum Pose
    {
        Idle,
        Left,
        Right,
    }

    private bool nextIsLeft = true;

    public Pose Current { get; private set; } = Pose.Idle;

    /// <summary>一次按键：从待机抬爪，或换另一只爪继续敲。</summary>
    public Pose OnTap()
    {
        Current = nextIsLeft ? Pose.Left : Pose.Right;
        nextIsLeft = !nextIsLeft;
        return Current;
    }

    /// <summary>静默超时：回到待机。</summary>
    public Pose OnSilence()
    {
        Current = Pose.Idle;
        return Current;
    }
}
