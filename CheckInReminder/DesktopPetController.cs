using System.Diagnostics;

namespace CheckInReminder;

/// <summary>A frame is borrowed until the next presentation or ClearFrame call.</summary>
public interface IPetFrameSink
{
    void SetFrame(Bitmap frame);
    void ClearFrame();
}

/// <summary>Samples live input on the UI thread and renders the white-bear rig until motion settles.</summary>
public sealed class DesktopPetController : IDisposable
{
    private readonly IPetFrameSink sink;
    private readonly DesktopInputState inputState;
    private readonly System.Windows.Forms.Timer frameTimer;
    private readonly Control dispatcher = new();
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly Stopwatch elapsedClock = new();
    private readonly Stopwatch playbackClock = new();
    private DesktopPetMotionModel motion = new();
    private DesktopPetRigPose lastPose = DesktopPetRigPose.Rest;
    private WhiteBearRigRenderer? rigRenderer;
    private AnimationSequence? idleSequence;
    private AnimationSequence? tapSequence;
    private AnimationTimeline? idleTimeline;
    private AnimationTimeline? tapTimeline;
    private Bitmap? ownedFrame;
    private long lastVersion = long.MinValue;
    private int currentFrame = -1;
    private int pendingNotification;
    private bool playingTap;
    private bool started;
    private bool inFrameTick;
    private bool wakeDuringFrame;
    private volatile bool disposed;

    public DesktopPetController(IPetFrameSink sink, DesktopInputState inputState)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.inputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
        _ = dispatcher.Handle;
        frameTimer = new System.Windows.Forms.Timer { Interval = 16 };
        frameTimer.Tick += OnFrameTick;
    }

    internal bool IsRendering => !disposed && frameTimer.Enabled;

    /// <summary>Present idle immediately; Start is called only after hook installation.</summary>
    public void SetCharacter(ReminderCharacter character)
    {
        EnsureOwnerThread();
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(character);
        DisposeAssets();
        motion = new DesktopPetMotionModel();
        lastPose = DesktopPetRigPose.Rest;
        lastVersion = long.MinValue;
        currentFrame = -1;
        playingTap = false;

        if (string.Equals(character.Id, AnimationCatalog.DefaultCharacterId, StringComparison.Ordinal))
        {
            rigRenderer = WhiteBearRigRenderer.Load();
            PresentOwnedFrame(rigRenderer.Render(DesktopPetRigPose.Rest));
        }
        else if (character.HasPetAssets)
        {
            idleSequence = AnimationSequence.Load(character.PetIdleSequenceName, character.PetIdleDuration, loop: true);
            tapSequence = AnimationSequence.Load(character.PetTapSequenceName, character.PetTapDuration, loop: true);
            idleTimeline = new AnimationTimeline(idleSequence.Frames.Count, character.PetIdleDuration, loop: true);
            tapTimeline = new AnimationTimeline(tapSequence.Frames.Count, character.PetTapDuration, loop: true);
            sink.SetFrame(idleSequence.Frames[0]);
            currentFrame = 0;
            playbackClock.Restart();
        }
        else
        {
            using var reminder = AnimationSequence.Load(character.SequenceName, character.Duration, character.Loop);
            PresentOwnedFrame(new Bitmap(reminder.Frames.MaxBy(VisibleSampleCount)!));
        }
        if (started) WakeTimer();
    }

    public void Start()
    {
        EnsureOwnerThread();
        if (disposed || started) return;
        started = true;
        WakeTimer();
    }

    /// <summary>Hook callbacks only wake the timer; sampling and drawing happen on a later UI tick.</summary>
    public void NotifyInputAvailable()
    {
        if (disposed) return;
        if (Environment.CurrentManagedThreadId == ownerThreadId)
        {
            WakeTimer();
            return;
        }
        // Coalesce non-hook callers, including a notification queued just before teardown.
        if (Interlocked.Exchange(ref pendingNotification, 1) != 0) return;
        try
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref pendingNotification, 0);
                if (!disposed) WakeTimer();
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref pendingNotification, 0);
        }
    }

    private void WakeTimer()
    {
        if (disposed || !started) return;
        if (inFrameTick)
        {
            wakeDuringFrame = true;
            return;
        }
        if (frameTimer.Enabled) return;
        elapsedClock.Restart();
        frameTimer.Start();
    }

    private void OnFrameTick(object? sender, EventArgs eventArgs)
    {
        if (disposed || !started || inFrameTick) return;
        inFrameTick = true;
        wakeDuringFrame = false;
        // A slow frame must not leave WM_TIMER continuously ready and starve UI messages.
        frameTimer.Stop();
        try
        {
            var elapsed = elapsedClock.Elapsed;
            elapsedClock.Restart();
            var snapshot = inputState.ReadSnapshot();
            var area = sink is Control control
                ? Screen.FromControl(control).WorkingArea
                : Screen.PrimaryScreen?.WorkingArea ?? Rectangle.Empty;
            var pose = motion.Step(snapshot, area, elapsed);
            if (rigRenderer is not null)
            {
                if (snapshot.Version != lastVersion || !lastPose.IsAtRest || !pose.IsAtRest)
                    PresentOwnedFrame(rigRenderer.Render(ToRenderPose(pose)));
            }
            else if (idleSequence is not null && tapSequence is not null)
            {
                PlaySequence(snapshot.ActiveVirtualKey != 0 || snapshot.LeftButtonDown || snapshot.RightButtonDown);
            }
            lastVersion = snapshot.Version;
            lastPose = pose;
            if (disposed) return;
            if (pose.IsAtRest && idleSequence is null && !wakeDuringFrame)
            {
                elapsedClock.Reset();
            }
            else frameTimer.Start();
        }
        finally
        {
            inFrameTick = false;
        }
    }

    private static DesktopPetRigPose ToRenderPose(DesktopPetRigPose pose)
    {
        // The model's neutral target differs from the source artwork's resting paw.
        // Blend by press so key release returns to the original idle artwork.
        var rest = DesktopPetRigPose.Rest;
        var press = pose.IsAtRest ? 0f : pose.KeyboardPress;
        var target = new PointF(
            rest.KeyboardTarget.X + (pose.KeyboardTarget.X - rest.KeyboardTarget.X) * press,
            rest.KeyboardTarget.Y + (pose.KeyboardTarget.Y - rest.KeyboardTarget.Y) * press);
        var offset = pose.MouseOffset;
        if (pose.IsAtRest && Math.Abs(offset.X) < 0.002f && Math.Abs(offset.Y) < 0.002f)
            offset = PointF.Empty;
        return pose with
        {
            MouseOffset = offset,
            MouseRotationDegrees = pose.IsAtRest ? 0f : pose.MouseRotationDegrees,
            MousePress = pose.IsAtRest ? 0f : pose.MousePress,
            KeyboardTarget = target,
            KeyboardPress = press
        };
    }

    private void PlaySequence(bool tap)
    {
        if (playingTap != tap)
        {
            playingTap = tap;
            playbackClock.Restart();
            currentFrame = -1;
        }
        var sequence = tap ? tapSequence! : idleSequence!;
        var timeline = tap ? tapTimeline! : idleTimeline!;
        var frame = timeline.GetFrameIndex(playbackClock.Elapsed);
        if (currentFrame == frame) return;
        sink.SetFrame(sequence.Frames[frame]);
        currentFrame = frame;
    }

    private void PresentOwnedFrame(Bitmap next)
    {
        try
        {
            sink.SetFrame(next);
        }
        catch
        {
            next.Dispose();
            throw;
        }
        if (disposed)
        {
            sink.ClearFrame();
            next.Dispose();
            return;
        }
        var previous = ownedFrame;
        ownedFrame = next;
        previous?.Dispose();
    }

    private static int VisibleSampleCount(Bitmap frame)
    {
        var score = 0;
        for (var y = 0; y < frame.Height; y += 4)
        for (var x = 0; x < frame.Width; x += 4)
            if (frame.GetPixel(x, y).A >= 32) score++;
        return score;
    }

    private void DisposeAssets()
    {
        frameTimer.Stop();
        elapsedClock.Reset();
        playbackClock.Reset();
        sink.ClearFrame();
        ownedFrame?.Dispose();
        ownedFrame = null;
        rigRenderer?.Dispose();
        rigRenderer = null;
        idleSequence?.Dispose();
        tapSequence?.Dispose();
        idleSequence = null;
        tapSequence = null;
        idleTimeline = null;
        tapTimeline = null;
    }

    private void EnsureOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("桌宠生命周期操作必须在创建控制器的 UI 线程执行。");
    }

    public void Dispose()
    {
        if (disposed) return;
        EnsureOwnerThread();
        disposed = true;
        started = false;
        frameTimer.Tick -= OnFrameTick;
        DisposeAssets();
        frameTimer.Dispose();
        dispatcher.Dispose();
    }
}
