namespace CheckInReminder;

/// <summary>
/// 按键事件节流（纯逻辑）：时间窗内的连续按键合并为一次敲击信号，
/// 避免快速连打导致动画状态高频抖动。
/// </summary>
public sealed class KeyTapThrottler
{
    private readonly TimeSpan window;
    private TimeSpan lastSignaled = TimeSpan.MinValue;

    public KeyTapThrottler(TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        this.window = window;
    }

    /// <summary>此刻的按键是否应升级为一次敲击信号。</summary>
    public bool ShouldSignal(TimeSpan now)
    {
        if (lastSignaled != TimeSpan.MinValue && now - lastSignaled < window)
        {
            return false;
        }

        lastSignaled = now;
        return true;
    }

    public void Reset() => lastSignaled = TimeSpan.MinValue;
}
