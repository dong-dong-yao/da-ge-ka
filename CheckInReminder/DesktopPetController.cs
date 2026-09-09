using System.Diagnostics;
using System.Runtime.InteropServices;

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
    private readonly Func<Bitmap> loadArtwork;
    private readonly Func<Bitmap, WhiteBearRigRenderer> createRig;
    private readonly System.Windows.Forms.Timer frameTimer;
    private readonly Control dispatcher = new();
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly Stopwatch elapsedClock = new();
    private readonly Stopwatch playbackClock = new();
    private readonly object pulseGate = new();
    private PointF? pendingKeyboardPulse;
    private bool pendingLeftPulse;
    private bool pendingRightPulse;
    // Deduplication watermarks intentionally survive pulse clearing and character reloads.
    private long lastQueuedKeyboardPressSequence;
    private long lastQueuedLeftButtonPressSequence;
    private long lastQueuedRightButtonPressSequence;
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
    private volatile bool staticFallback;
    private volatile bool disposed;

    public DesktopPetController(IPetFrameSink sink, DesktopInputState inputState)
        : this(sink, inputState, WhiteBearRigRenderer.LoadArtwork, WhiteBearRigRenderer.Create)
    {
    }

    internal DesktopPetController(
        IPetFrameSink sink, DesktopInputState inputState,
        Func<Bitmap> loadArtwork, Func<Bitmap, WhiteBearRigRenderer> createRig)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.inputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
        this.loadArtwork = loadArtwork ?? throw new ArgumentNullException(nameof(loadArtwork));
        this.createRig = createRig ?? throw new ArgumentNullException(nameof(createRig));
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
        staticFallback = false;
        _ = TakePulses();

        if (string.Equals(character.Id, AnimationCatalog.DefaultCharacterId, StringComparison.Ordinal))
        {
            LoadWhiteBear();
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

    private void LoadWhiteBear()
    {
        // Source errors remain visible to the caller; fallback only covers layer construction.
        Bitmap? artwork = loadArtwork();
        try
        {
            try
            {
                rigRenderer = createRig(artwork);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ExternalException)
            {
                lock (pulseGate)
                {
                    staticFallback = true;
                    pendingKeyboardPulse = null;
                    pendingLeftPulse = pendingRightPulse = false;
                }
                var fallback = artwork;
                artwork = null; // PresentOwnedFrame takes ownership even when presentation fails.
                PresentOwnedFrame(fallback);
                return;
            }
            PresentOwnedFrame(rigRenderer.Render(DesktopPetRigPose.Rest));
        }
        finally
        {
            artwork?.Dispose();
        }
    }

    public void Start()
    {
        EnsureOwnerThread();
        if (disposed || started) return;
        started = true;
        WakeTimer();
    }

    /// <summary>Maps current input to bounded visual pulses; drawing happens on a later UI tick.</summary>
    public void NotifyInputAvailable()
    {
        if (disposed || staticFallback) return;
        var snapshot = inputState.ReadSnapshot();
        var hasKeyboardTarget = KeyboardTargetMapper.TryMap(snapshot.ActiveVirtualKey, out var target);
        lock (pulseGate)
        {
            if (disposed || staticFallback) return;
            // Never queue a snapshot or virtual key. Only normalized geometry and button bits survive.
            if (snapshot.ActiveKeyPressSequence > lastQueuedKeyboardPressSequence)
            {
                lastQueuedKeyboardPressSequence = snapshot.ActiveKeyPressSequence;
                if (hasKeyboardTarget) pendingKeyboardPulse = target;
            }
            if (snapshot.LeftButtonDown
                && snapshot.LeftButtonPressSequence > lastQueuedLeftButtonPressSequence)
            {
                lastQueuedLeftButtonPressSequence = snapshot.LeftButtonPressSequence;
                pendingLeftPulse = true;
            }
            if (snapshot.RightButtonDown
                && snapshot.RightButtonPressSequence > lastQueuedRightButtonPressSequence)
            {
                lastQueuedRightButtonPressSequence = snapshot.RightButtonPressSequence;
                pendingRightPulse = true;
            }
        }
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

    private (PointF? Keyboard, bool Left, bool Right) TakePulses(DesktopInputSnapshot? sampledInput = null)
    {
        lock (pulseGate)
        {
            if (sampledInput is { } snapshot)
            {
                // A live held snapshot is itself a presented/consumed press. Advance
                // the same watermarks atomically with pulse consumption so later
                // repeat, fallback, or mouse-move notifications cannot replay it.
                if (snapshot.ActiveKeyPressSequence > lastQueuedKeyboardPressSequence)
                    lastQueuedKeyboardPressSequence = snapshot.ActiveKeyPressSequence;
                if (snapshot.LeftButtonDown
                    && snapshot.LeftButtonPressSequence > lastQueuedLeftButtonPressSequence)
                    lastQueuedLeftButtonPressSequence = snapshot.LeftButtonPressSequence;
                if (snapshot.RightButtonDown
                    && snapshot.RightButtonPressSequence > lastQueuedRightButtonPressSequence)
                    lastQueuedRightButtonPressSequence = snapshot.RightButtonPressSequence;
            }
            var result = (pendingKeyboardPulse, pendingLeftPulse, pendingRightPulse);
            pendingKeyboardPulse = null;
            pendingLeftPulse = pendingRightPulse = false;
            return result;
        }
    }

    private void WakeTimer()
    {
        if (disposed || !started || staticFallback) return;
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
        if (disposed || !started || staticFallback || inFrameTick) return;
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
            var pulses = TakePulses(snapshot);
            var pose = motion.Step(snapshot, area, elapsed, pulses.Keyboard, pulses.Left, pulses.Right);
            if (rigRenderer is not null)
            {
                if (snapshot.Version != lastVersion || !lastPose.IsAtRest || !pose.IsAtRest)
                    PresentOwnedFrame(rigRenderer.Render(ToRenderPose(pose)));
            }
            else if (idleSequence is not null && tapSequence is not null)
            {
                PlaySequence(snapshot.ActiveVirtualKey != 0 || pose.KeyboardPress > 0.002f || pose.MousePress > 0.002f);
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
        // The renderer switches back to the original idle arm after rebound.
        // Blending the target here would change the selected sprite during a tap.
        var press = pose.IsAtRest ? 0f : pose.KeyboardPress;
        var offset = pose.MouseOffset;
        if (pose.IsAtRest && Math.Abs(offset.X) < 0.002f && Math.Abs(offset.Y) < 0.002f)
            offset = PointF.Empty;
        return pose with
        {
            MouseOffset = offset,
            MouseRotationDegrees = pose.IsAtRest ? 0f : pose.MouseRotationDegrees,
            MousePress = pose.IsAtRest ? 0f : pose.MousePress,
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
        _ = TakePulses();
        frameTimer.Tick -= OnFrameTick;
        DisposeAssets();
        frameTimer.Dispose();
        dispatcher.Dispose();
    }
}
