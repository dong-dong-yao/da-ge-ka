namespace CheckInReminder;

public sealed class AnimationTimeline
{
    public AnimationTimeline(int frameCount, TimeSpan duration, bool loop)
    {
        if (frameCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        FrameCount = frameCount;
        Duration = duration;
        Loop = loop;
    }

    public int FrameCount { get; }

    public TimeSpan Duration { get; }

    public bool Loop { get; }

    public bool HasFinished(TimeSpan elapsed) => !Loop && elapsed >= Duration;

    public int GetFrameIndex(TimeSpan elapsed)
    {
        var elapsedTicks = Math.Max(0, elapsed.Ticks);
        if (Loop)
        {
            elapsedTicks %= Duration.Ticks;
        }
        else if (elapsedTicks >= Duration.Ticks)
        {
            return FrameCount - 1;
        }

        return Math.Min(
            FrameCount - 1,
            (int)((double)elapsedTicks / Duration.Ticks * FrameCount));
    }
}
