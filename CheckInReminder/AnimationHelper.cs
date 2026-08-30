namespace CheckInReminder;

/// <summary>
/// 通用的单属性缓动动画器：在约 200ms 内把一个浮点值从起点平滑推到终点。
/// 用于设置界面里时间、间隔等数值修改时的细腻过渡动效。
/// </summary>
internal sealed class AnimationHelper : IDisposable
{
    private readonly Action<float> apply;
    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Diagnostics.Stopwatch clock = new();
    private float from;
    private float to;
    private int durationMilliseconds = 200;

    public AnimationHelper(Action<float> apply)
    {
        this.apply = apply;
        timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) => Step();
    }

    public bool IsRunning => timer.Enabled;

    public void Start(float from, float to, int durationMilliseconds = 200)
    {
        this.from = from;
        this.to = to;
        this.durationMilliseconds = Math.Max(40, durationMilliseconds);
        timer.Stop();
        clock.Restart();
        timer.Start();
    }

    public void Stop()
    {
        timer.Stop();
        clock.Stop();
    }

    private void Step()
    {
        var elapsed = (float)clock.ElapsedMilliseconds;
        var t = Math.Min(1f, elapsed / durationMilliseconds);
        var eased = EaseOutCubic(t);
        apply(from + ((to - from) * eased));
        if (t >= 1f)
        {
            Stop();
        }
    }

    private static float EaseOutCubic(float t)
    {
        var p = t - 1f;
        return (p * p * p) + 1f;
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        clock.Stop();
    }
}
