using System.Diagnostics;

namespace CheckInReminder;

/// <summary>宠物帧呈现目标（PetOverlayForm 实现；测试可用假实现）。</summary>
public interface IPetFrameSink
{
    void SetFrame(Bitmap frame);
}

/// <summary>
/// 桌面宠物动画控制器：驱动待机/敲击状态与帧呈现。
/// 宠物素材（{角色Id}-pet-idle / {角色Id}-pet-tap）存在时使用素材播放；
/// 缺失时从角色提醒动画第 0 帧派生左右镜像占位帧，视觉上即"左右拍拍"。
/// </summary>
public sealed class DesktopPetController : IDisposable
{
    private const int IdleFrameIntervalMs = 90;
    private const int TapFrameIntervalMs = 36;
    private const int PlaceholderWatchdogMs = 60;
    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromMilliseconds(400);

    private readonly IPetFrameSink sink;
    private readonly PetTapStateMachine machine = new();
    private readonly System.Windows.Forms.Timer frameTimer;
    private readonly Stopwatch playbackClock = new();
    private readonly Stopwatch silenceClock = new();
    private AnimationSequence? idleSequence;
    private AnimationSequence? tapSequence;
    private AnimationTimeline? idleTimeline;
    private AnimationTimeline? tapTimeline;
    private Bitmap? placeholderIdle;
    private Bitmap? placeholderTapLeft;
    private Bitmap? placeholderTapRight;
    private int currentFrame = -1;
    private bool disposed;

    public DesktopPetController(IPetFrameSink sink)
    {
        this.sink = sink;
        frameTimer = new System.Windows.Forms.Timer { Interval = IdleFrameIntervalMs };
        frameTimer.Tick += (_, _) => OnFrameTick();
    }

    internal PetTapStateMachine.Pose CurrentPose => machine.Current;

    /// <summary>切换到指定角色的宠物素材（或派生占位帧）。</summary>
    public void SetCharacter(ReminderCharacter character)
    {
        DisposeAssets();
        machine.OnSilence();
        silenceClock.Reset();

        if (character.HasPetAssets)
        {
            idleSequence = AnimationSequence.Load(character.PetIdleSequenceName, character.PetIdleDuration, loop: true);
            tapSequence = AnimationSequence.Load(character.PetTapSequenceName, character.PetTapDuration, loop: true);
            idleTimeline = new AnimationTimeline(idleSequence.Frames.Count, character.PetIdleDuration, loop: true);
            tapTimeline = new AnimationTimeline(tapSequence.Frames.Count, character.PetTapDuration, loop: true);
            currentFrame = 0;
            sink.SetFrame(idleSequence.Frames[0]);
            playbackClock.Restart();
            frameTimer.Interval = IdleFrameIntervalMs;
            frameTimer.Start();
            return;
        }

        // 占位模式：待机是静帧（零 CPU），敲击时直接呈现左右派生帧
        using var reminder = AnimationSequence.Load(character.SequenceName, character.Duration, character.Loop);
        placeholderIdle = new Bitmap(reminder.Frames[0]);
        placeholderTapLeft = FlipHorizontal(placeholderIdle);
        placeholderTapRight = OffsetVertical(placeholderIdle, 6);
        currentFrame = -1;
        sink.SetFrame(placeholderIdle);
        frameTimer.Stop();
        playbackClock.Reset();
    }

    /// <summary>一次按键（已由钩子侧节流合并）。</summary>
    public void OnKeyTapped()
    {
        if (disposed)
        {
            return;
        }

        silenceClock.Restart();
        var pose = machine.OnTap();
        if (tapSequence is not null)
        {
            if (!frameTimer.Enabled || frameTimer.Interval != TapFrameIntervalMs)
            {
                playbackClock.Restart();
                currentFrame = -1;
                frameTimer.Interval = TapFrameIntervalMs;
                frameTimer.Start();
            }

            return;
        }

        sink.SetFrame(pose == PetTapStateMachine.Pose.Left ? placeholderTapLeft! : placeholderTapRight!);
        if (!frameTimer.Enabled)
        {
            frameTimer.Interval = PlaceholderWatchdogMs;
            frameTimer.Start();
        }
    }

    private void OnFrameTick()
    {
        if (disposed)
        {
            return;
        }

        if (machine.Current == PetTapStateMachine.Pose.Idle)
        {
            if (idleSequence is not null && idleTimeline is not null)
            {
                var frame = idleTimeline.GetFrameIndex(playbackClock.Elapsed);
                if (frame != currentFrame)
                {
                    currentFrame = frame;
                    sink.SetFrame(idleSequence.Frames[frame]);
                }
            }

            return;
        }

        if (silenceClock.Elapsed >= SilenceTimeout)
        {
            machine.OnSilence();
            if (idleSequence is not null)
            {
                playbackClock.Restart();
                currentFrame = -1;
                frameTimer.Interval = IdleFrameIntervalMs;
            }
            else
            {
                frameTimer.Stop();
                sink.SetFrame(placeholderIdle!);
            }

            return;
        }

        if (tapSequence is not null && tapTimeline is not null)
        {
            var frame = tapTimeline.GetFrameIndex(playbackClock.Elapsed);
            if (frame != currentFrame)
            {
                currentFrame = frame;
                sink.SetFrame(tapSequence.Frames[frame]);
            }
        }
    }

    private static Bitmap FlipHorizontal(Bitmap source)
    {
        var copy = new Bitmap(source);
        copy.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return copy;
    }

    private static Bitmap OffsetVertical(Bitmap source, int offsetY)
    {
        var result = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(source, 0, offsetY, source.Width, source.Height);
        return result;
    }

    private void DisposeAssets()
    {
        frameTimer.Stop();
        idleSequence?.Dispose();
        tapSequence?.Dispose();
        idleSequence = null;
        tapSequence = null;
        idleTimeline = null;
        tapTimeline = null;
        placeholderIdle?.Dispose();
        placeholderTapLeft?.Dispose();
        placeholderTapRight?.Dispose();
        placeholderIdle = null;
        placeholderTapLeft = null;
        placeholderTapRight = null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DisposeAssets();
        frameTimer.Dispose();
    }
}
