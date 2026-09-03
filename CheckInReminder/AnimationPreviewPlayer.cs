using System.Diagnostics;

namespace CheckInReminder;

/// <summary>
/// 封装角色动画预览的加载、播放、暂停与释放（PictureBox + Timer + Stopwatch）。
/// 供设置页当前角色预览与角色页网格卡片共用。
/// </summary>
internal sealed class AnimationPreviewPlayer : IDisposable
{
    private readonly PictureBox pictureBox;
    private readonly System.Windows.Forms.Timer timer;
    private readonly Stopwatch clock = new();
    private AnimationSequence? sequence;
    private AnimationTimeline? timeline;
    private int currentFrame = -1;
    private bool paused;

    public AnimationPreviewPlayer(PictureBox pictureBox, int intervalMilliseconds = 33)
    {
        this.pictureBox = pictureBox;
        timer = new System.Windows.Forms.Timer { Interval = intervalMilliseconds };
        timer.Tick += (_, _) => Advance();
    }

    public bool IsLoaded => sequence is not null;

    /// <summary>加载角色提醒动画并循环播放（除非处于暂停状态）。</summary>
    public void Load(ReminderCharacter character)
    {
        timer.Stop();
        clock.Stop();
        pictureBox.Image = null;
        sequence?.Dispose();
        sequence = AnimationSequence.Load(character.SequenceName, character.Duration, character.Loop);
        timeline = new AnimationTimeline(sequence.Frames.Count, character.Duration, loop: true);
        currentFrame = 0;
        pictureBox.Image = sequence.Frames[0];
        clock.Restart();
        if (paused)
        {
            clock.Stop();
        }
        else
        {
            timer.Start();
        }
    }

    /// <summary>回到第 0 帧静帧（暂停展示用）；下次恢复播放时从头开始。</summary>
    public void ShowFirstFrame()
    {
        if (sequence is null)
        {
            return;
        }

        timer.Stop();
        currentFrame = 0;
        pictureBox.Image = sequence.Frames[0];
        clock.Restart();
        clock.Stop();
    }

    /// <summary>暂停/恢复播放；暂停时停在当前帧，恢复时接着播。</summary>
    public void SetPaused(bool value)
    {
        if (paused == value)
        {
            return;
        }

        paused = value;
        if (paused)
        {
            timer.Stop();
            clock.Stop();
            return;
        }

        if (sequence is not null)
        {
            clock.Start();
            timer.Start();
        }
    }

    private void Advance()
    {
        if (paused || sequence is null || timeline is null)
        {
            return;
        }

        var frameIndex = timeline.GetFrameIndex(clock.Elapsed);
        if (frameIndex == currentFrame)
        {
            return;
        }

        currentFrame = frameIndex;
        pictureBox.Image = sequence.Frames[frameIndex];
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        pictureBox.Image = null;
        sequence?.Dispose();
        clock.Stop();
    }
}
