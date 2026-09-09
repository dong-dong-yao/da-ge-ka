using CheckInReminder;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopPetControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TapStateMachine_AlternatesPawsAndReturnsToIdleOnSilence()
    {
        var machine = new PetTapStateMachine();

        Assert.AreEqual(PetTapStateMachine.Pose.Idle, machine.Current);
        Assert.AreEqual(PetTapStateMachine.Pose.Left, machine.OnTap());
        Assert.AreEqual(PetTapStateMachine.Pose.Right, machine.OnTap());
        Assert.AreEqual(PetTapStateMachine.Pose.Left, machine.OnTap());
        Assert.AreEqual(PetTapStateMachine.Pose.Idle, machine.OnSilence());
        Assert.AreEqual(PetTapStateMachine.Pose.Right, machine.OnTap(), "回待机后延续左右交替节奏，打字更自然。");
    }

    [TestMethod]
    public void KeyTapThrottler_MergesBurstsWithinWindow()
    {
        var throttler = new KeyTapThrottler(TimeSpan.FromMilliseconds(40));

        Assert.IsTrue(throttler.ShouldSignal(TimeSpan.FromMilliseconds(1000)));
        Assert.IsFalse(throttler.ShouldSignal(TimeSpan.FromMilliseconds(1020)), "窗口内的连按应被合并。");
        Assert.IsFalse(throttler.ShouldSignal(TimeSpan.FromMilliseconds(1039)));
        Assert.IsTrue(throttler.ShouldSignal(TimeSpan.FromMilliseconds(1041)), "超过窗口后应再次放行。");

        throttler.Reset();
        Assert.IsTrue(throttler.ShouldSignal(TimeSpan.FromMilliseconds(1042)), "重置后应立即放行。");
    }

    [TestMethod]
    public void LiveInput_ChangesBothArmRegionsAndReleaseReturnsToIdle()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            var character = AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId)!;
            controller.SetCharacter(character);
            using var idle = new Bitmap(sink.LastFrame!);
            controller.Start();
            var area = Screen.PrimaryScreen!.WorkingArea;
            state.UpdatePointer(area.Right, area.Top);
            state.UpdateMouseButton(DesktopMouseButton.Left, true);
            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            PumpEvents(100);

            Assert.IsGreaterThan(1d, MeanPixelDifference(idle, sink.LastFrame!, new Rectangle(100, 220, 175, 140)),
                "鼠标输入必须改变鼠标手臂区域。");
            Assert.IsGreaterThan(1d, MeanPixelDifference(idle, sink.LastFrame!, new Rectangle(330, 180, 130, 185)),
                "键盘输入必须独立改变键盘手臂区域。");

            CenterPointer(state);
            state.UpdateMouseButton(DesktopMouseButton.Left, false);
            state.UpdateKey(0x41, false);
            controller.NotifyInputAvailable();
            PumpUntil(() => !IsRendering(controller), 1000);
            Assert.IsLessThan(0.5d, MeanPixelDifference(idle, sink.LastFrame!), "松开输入并将鼠标回中后应恢复待机姿态。");
        });
    }

    [TestMethod]
    public void InitialFrameHasMeaningfulVisibleContent()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            using var controller = new DesktopPetController(sink, CenteredInput());
            var character = AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId)!;

            controller.SetCharacter(character);

            var frame = sink.LastFrame;
            Assert.IsNotNull(frame);
            var visiblePixels = CountVisiblePixels(frame);
            Assert.IsGreaterThan(
                (frame.Width * frame.Height) / 5,
                visiblePixels,
                "桌宠待机占位帧至少应有 20% 的有效可见像素，不能使用只剩边缘的动画首帧。");
        });
    }

    [TestMethod]
    public void WhiteBearPet_PreservesProvidedArtworkAndTransparentExterior()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            using var controller = new DesktopPetController(sink, CenteredInput());
            var character = AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId)!;

            controller.SetCharacter(character);
            var idle = sink.LastFrame!;

            Assert.AreEqual(2400d / 1792d, (double)idle.Width / idle.Height, 0.01,
                "白熊桌宠必须沿用用户提供图片的原始构图比例。");
            Assert.AreEqual(0, idle.GetPixel(0, 0).A,
                "与画面边缘相连的白色背景必须透明，不能显示成白色矩形窗口。");
            Assert.IsGreaterThan((idle.Width * idle.Height) / 10, CountVisiblePixels(idle),
                "透明化背景后必须保留白熊、鼠标和键盘主体。");

        });
    }

    [TestMethod]
    public void SettledInput_StopsRenderingAndNotificationWakesWithoutPresentingInTheCallback()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            Assert.IsFalse(IsRendering(controller), "展示初帧不能提前启动输入循环。");
            controller.Start();
            PumpUntil(() => !IsRendering(controller), 1000);
            Assert.IsFalse(IsRendering(controller));
            var settledCount = sink.FrameCount;
            PumpEvents(100);
            Assert.AreEqual(settledCount, sink.FrameCount, "静止时不应继续分配渲染帧。");

            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable();
            Assert.IsTrue(IsRendering(controller));
            Assert.AreEqual(settledCount, sink.FrameCount, "Hook 通知只能唤醒，不得同步绘图。");
            PumpEvents(100);
            Assert.IsGreaterThan(settledCount, sink.FrameCount);

            state.UpdateKey(0x47, false);
            controller.NotifyInputAvailable();
            PumpUntil(() => !IsRendering(controller), 1000);
            Assert.IsFalse(IsRendering(controller), "release 后应再次停表。");
        });
    }

    [TestMethod]
    public void OwnedFrames_StayAliveDuringReplacementAndAreReleasedAfterPresentationAndDisposal()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var initial = sink.LastFrame!;
            state.UpdateKey(0x41, true);
            controller.Start();
            PumpEvents(100);
            Assert.IsTrue(sink.CheckedPreviousFrame, "每次替换时旧帧仍应可读。");
            Assert.Throws<ArgumentException>(() => initial.GetPixel(0, 0));

            var last = sink.LastFrame!;
            controller.Dispose();
            Assert.IsNull(sink.LastFrame, "释放最后一帧前必须让窗体丢弃引用。");
            Assert.Throws<ArgumentException>(() => last.GetPixel(0, 0));
            var count = sink.FrameCount;
            controller.NotifyInputAvailable();
            PumpEvents(100);
            Assert.AreEqual(count, sink.FrameCount);
        });
    }

    [TestMethod]
    public void BackgroundNotification_IsMarshaledAndQueuedNotificationCannotRestartDisposedController()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            PumpUntil(() => !IsRendering(controller), 1000);
            var count = sink.FrameCount;
            state.UpdateKey(0x41, true);
            Task.Run(controller.NotifyInputAvailable).GetAwaiter().GetResult();
            Assert.AreEqual(count, sink.FrameCount);
            PumpEvents(100);
            Assert.IsGreaterThan(count, sink.FrameCount);
            Assert.AreEqual(Environment.CurrentManagedThreadId, sink.LastPresentationThread);

            Task.Run(controller.NotifyInputAvailable).GetAwaiter().GetResult();
            controller.Dispose();
            count = sink.FrameCount;
            PumpEvents(100);
            Assert.AreEqual(count, sink.FrameCount);
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void KeyboardTapBeforeFirstTick_PresentsOneVisiblePressThenSettles(bool backgroundNotification)
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            using var idle = new Bitmap(sink.LastFrame!);
            controller.Start();

            void Tap()
            {
                state.UpdateKey(0x41, true);
                controller.NotifyInputAvailable();
                state.UpdateKey(0x41, false);
                controller.NotifyInputAvailable();
            }
            if (backgroundNotification) Task.Run(Tap).GetAwaiter().GetResult();
            else Tap();

            Assert.AreEqual(0, state.ReadSnapshot().ActiveVirtualKey);
            Assert.AreEqual(1, sink.FrameCount, "两次输入通知都不能同步绘图。");
            Tick(controller);
            Assert.IsGreaterThan(1d, MeanPixelDifference(idle, sink.LastFrame!, new Rectangle(330, 180, 130, 185)),
                "两个 UI tick 之间完整发生的键盘短按必须可见。");
            Assert.IsGreaterThan(3d, MeanPixelDifference(idle, sink.LastFrame!, new Rectangle(355, 316, 60, 30)),
                "短按首帧必须进入用户提供的左区按压素材并触键，不能只把旧抬手平移。");
            PumpUntil(() => !IsRendering(controller), 1000);
            Assert.IsFalse(IsRendering(controller), "一次视觉反馈消费后必须能停表。");
            Assert.IsLessThan(0.5d, MeanPixelDifference(idle, sink.LastFrame!));
        });
    }

    [TestMethod]
    public void HeldKeyboardRelease_PresentsExactIdleAndStopsWithinFiftyMilliseconds()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            PumpUntil(() => !IsRendering(controller), 1000);

            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable();
            var pressFrame = sink.FrameCount;
            PumpUntil(() => sink.FrameCount > pressFrame, 1000);
            Assert.AreNotEqual(idleHash, RawPixelHash(sink.LastFrame!), "Held G must visibly contact the keyboard.");

            var releaseFrame = sink.FrameCount;
            var presentedExactIdle = false;
            sink.FramePresented = frame => presentedExactIdle = RawPixelHash(frame) == idleHash;
            var releaseClock = Stopwatch.StartNew();
            state.UpdateKey(0x47, false);
            controller.NotifyInputAvailable();
            PumpUntil(() => presentedExactIdle && !IsRendering(controller), 1000);

            var latency = releaseClock.Elapsed;
            var releaseFrames = sink.FrameCount - releaseFrame;
            TestContext.WriteLine($"Held release latency: {latency.TotalMilliseconds:F1} ms / {releaseFrames} presented frame(s).");
            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!),
                $"Release must restore the exact raw idle frame; observed {latency.TotalMilliseconds:F1} ms / {releaseFrames} frames.");
            Assert.IsFalse(IsRendering(controller), "The keyboard-only release frame must stop the timer.");
            Assert.IsLessThanOrEqualTo(50d, latency.TotalMilliseconds,
                $"Held-key release took {latency.TotalMilliseconds:F1} ms / {releaseFrames} presented frames.");
        });
    }

    [TestMethod]
    public void KeyboardTapBeforeFirstTick_ShowsContactThenExactIdleWithinFiftyMilliseconds()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            PumpUntil(() => !IsRendering(controller), 1000);

            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            state.UpdateKey(0x41, false);
            controller.NotifyInputAvailable();

            var beforeTapTick = sink.FrameCount;
            Tick(controller);
            Assert.AreEqual(beforeTapTick + 1, sink.FrameCount,
                "The first UI tick after a complete tap must present exactly one contact frame.");
            Assert.AreNotEqual(idleHash, RawPixelHash(sink.LastFrame!),
                "A complete tap must present at least one contact frame.");

            var releaseClock = Stopwatch.StartNew();
            Tick(controller);
            var latency = releaseClock.Elapsed;
            TestContext.WriteLine($"Short-tap release latency: {latency.TotalMilliseconds:F1} ms / 1 explicit UI frame.");
            Assert.AreEqual(beforeTapTick + 2, sink.FrameCount,
                "The UI render immediately after contact must present the raised idle arm.");
            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!));
            Assert.IsLessThanOrEqualTo(50d, latency.TotalMilliseconds,
                $"Short tap stayed in contact for {latency.TotalMilliseconds:F1} ms after its visible frame.");
            Assert.IsFalse(IsRendering(controller), "The idle frame after a short tap must stop the timer.");
        });
    }

    [TestMethod]
    public void HeldKeyboard_RemainsInContactAcrossConsecutiveTimerFrames()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            PumpUntil(() => !IsRendering(controller), 1000);

            var frames = new List<bool>();
            sink.FramePresented = frame => frames.Add(RawPixelHash(frame) == idleHash);
            state.UpdateKey(0x50, true);
            controller.NotifyInputAvailable();
            PumpUntil(() => frames.Count >= 4, 1000);

            Assert.IsGreaterThanOrEqualTo(4, frames.Count);
            Assert.IsTrue(frames.Take(4).All(idle => !idle),
                "A held key must not flicker to the raised idle arm between timer frames.");
        });
    }

    [TestMethod]
    public void HeldKeyboardRelease_NextExplicitUiTickIsExactIdleAndStopsTimer()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            Tick(controller);

            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            Assert.AreNotEqual(idleHash, RawPixelHash(sink.LastFrame!));

            state.UpdateKey(0x47, false);
            controller.NotifyInputAvailable();
            var beforeReleaseTick = sink.FrameCount;
            Tick(controller);

            Assert.AreEqual(beforeReleaseTick + 1, sink.FrameCount,
                "Release must present the raised arm on the very next UI render.");
            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!));
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void RepeatKeyDownAfterPresentedHold_DoesNotReplayContactAfterRelease()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            Tick(controller);

            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            Assert.AreNotEqual(idleHash, RawPixelHash(sink.LastFrame!));

            state.UpdateKey(0x47, true); // Auto-repeat: not a new physical press.
            controller.NotifyInputAvailable();
            state.UpdateKey(0x47, false);
            controller.NotifyInputAvailable();
            Tick(controller);

            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!),
                "Key repeat must not queue a one-shot contact after the real key is released.");
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void ReleasingTwoPresentedKeysWithoutIntermediateTick_DoesNotFlashFallbackZone()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            Tick(controller);

            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            state.UpdateKey(0x44, true);
            controller.NotifyInputAvailable();
            Tick(controller);

            state.UpdateKey(0x44, false);
            controller.NotifyInputAvailable(); // Snapshot falls back to the older held A.
            state.UpdateKey(0x41, false);
            controller.NotifyInputAvailable();
            Tick(controller);

            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!),
                "Releasing D then A without a tick must not replay A's already-presented zone.");
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void MouseMoveAndRepeatDownDuringHold_DoNotReplayClickPulseAfterRelease()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            Tick(controller);

            state.UpdateMouseButton(DesktopMouseButton.Left, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            var heldPress = LastPose(controller).MousePress;

            var area = Screen.PrimaryScreen!.WorkingArea;
            state.UpdatePointer(area.Left + area.Width / 2 + 1, area.Top + area.Height / 2);
            controller.NotifyInputAvailable();
            state.UpdateMouseButton(DesktopMouseButton.Left, true);
            controller.NotifyInputAvailable();
            state.UpdateMouseButton(DesktopMouseButton.Left, false);
            controller.NotifyInputAvailable();
            Tick(controller);
            var releasedPress = LastPose(controller).MousePress;

            Assert.IsLessThanOrEqualTo(heldPress + 0.001f, releasedPress,
                $"Release must decay the existing mouse press instead of replaying a click pulse ({heldPress:F3} -> {releasedPress:F3}).");
        });
    }

    [TestMethod]
    public void NewShortTapQueuedDuringAnotherKeysRelease_StillGetsOneContactFrame()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            Tick(controller);

            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            state.UpdateKey(0x47, false);
            controller.NotifyInputAvailable();
            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            state.UpdateKey(0x41, false);
            controller.NotifyInputAvailable();
            Tick(controller);

            var tapPose = LastPose(controller);
            Assert.IsTrue(tapPose.KeyboardContact,
                "A genuinely new tap during release must not be cleared with stale pulses.");
            Assert.AreEqual(0.14f, tapPose.KeyboardTarget.X, 0.001f);
            Assert.AreNotEqual(idleHash, RawPixelHash(sink.LastFrame!));
            Tick(controller);
            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!));
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void FallbackHeldKeyboard_ResumeTickConsumesSequenceSoRepeatCannotReplayAfterRelease()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            var failRig = true;
            using var controller = CreateController(sink, state, LoadStaticArtwork,
                _ => failRig ? throw new InvalidOperationException("injected fallback") : WhiteBearRigRenderer.Load());
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable(); // Ignored while the static fallback is active.

            failRig = false;
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            Tick(controller); // The live snapshot itself presents the held key.
            Assert.IsTrue(LastPose(controller).KeyboardContact);

            state.UpdateKey(0x47, true);
            controller.NotifyInputAvailable();
            state.UpdateKey(0x47, false);
            controller.NotifyInputAvailable();
            Tick(controller);

            Assert.AreEqual(idleHash, RawPixelHash(sink.LastFrame!),
                "A held key already shown after fallback must not be replayed by repeat notification.");
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void FallbackHeldMouse_ResumeTicksConsumeSequenceSoMoveCannotReplayClickAfterRelease()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            var failRig = true;
            using var controller = CreateController(sink, state, LoadStaticArtwork,
                _ => failRig ? throw new InvalidOperationException("injected fallback") : WhiteBearRigRenderer.Load());
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            state.UpdateMouseButton(DesktopMouseButton.Left, true);
            controller.NotifyInputAvailable(); // Ignored while the static fallback is active.

            failRig = false;
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            PumpUntil(() => LastPose(controller).MousePress >= 0.2f, 1000);
            var heldPress = LastPose(controller).MousePress;

            var area = Screen.PrimaryScreen!.WorkingArea;
            state.UpdatePointer(area.Left + area.Width / 2 + 1, area.Top + area.Height / 2);
            controller.NotifyInputAvailable();
            state.UpdateMouseButton(DesktopMouseButton.Left, true);
            controller.NotifyInputAvailable();
            CenterPointer(state);
            controller.NotifyInputAvailable();
            state.UpdateMouseButton(DesktopMouseButton.Left, false);
            controller.NotifyInputAvailable();
            Tick(controller);
            var releasedPress = LastPose(controller).MousePress;

            Assert.IsLessThanOrEqualTo(heldPress, releasedPress,
                $"A held mouse already shown after fallback must decay, not replay ({heldPress:F3} -> {releasedPress:F3}).");
        });
    }

    [TestMethod]
    public void ConcurrentMouseReleaseCannotAdvanceSequenceBeforeDelayedDownQueuesItsTap()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            Tick(controller);
            var gate = typeof(DesktopPetController).GetField("pulseGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            Thread? delayedDown = null;
            Exception? notificationFailure = null;
            try
            {
                lock (gate)
                {
                    state.UpdateMouseButton(DesktopMouseButton.Left, true);
                    delayedDown = new Thread(() =>
                    {
                        try { controller.NotifyInputAvailable(); }
                        catch (Exception exception) { notificationFailure = exception; }
                    }) { IsBackground = true };
                    delayedDown.Start();
                    Assert.IsTrue(SpinWait.SpinUntil(
                        () => (delayedDown.ThreadState & ThreadState.WaitSleepJoin) != 0,
                        TimeSpan.FromSeconds(2)), "Down notification must be waiting after its down snapshot.");

                    state.UpdateMouseButton(DesktopMouseButton.Left, false);
                    controller.NotifyInputAvailable(); // Re-enters gate first with the release snapshot.
                }
                Assert.IsTrue(delayedDown.Join(TimeSpan.FromSeconds(2)));
                Assert.IsNull(notificationFailure);
                Tick(controller);

                Assert.IsGreaterThanOrEqualTo(0.59f, LastPose(controller).MousePress,
                    "The complete concurrent click must retain its one-shot visual pulse.");
            }
            finally
            {
                if (delayedDown is not null) Assert.IsTrue(delayedDown.Join(TimeSpan.FromSeconds(2)));
            }
        });
    }

    [TestMethod]
    public void ConcurrentKeyboardReleaseDoesNotSwallowDelayedDownTap()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var idleHash = RawPixelHash(sink.LastFrame!);
            controller.Start();
            Tick(controller);
            var gate = typeof(DesktopPetController).GetField("pulseGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            Thread? delayedDown = null;
            Exception? notificationFailure = null;
            try
            {
                lock (gate)
                {
                    state.UpdateKey(0x41, true);
                    delayedDown = new Thread(() =>
                    {
                        try { controller.NotifyInputAvailable(); }
                        catch (Exception exception) { notificationFailure = exception; }
                    }) { IsBackground = true };
                    delayedDown.Start();
                    Assert.IsTrue(SpinWait.SpinUntil(
                        () => (delayedDown.ThreadState & ThreadState.WaitSleepJoin) != 0,
                        TimeSpan.FromSeconds(2)), "Down notification must be waiting after its down snapshot.");

                    state.UpdateKey(0x41, false);
                    controller.NotifyInputAvailable();
                }
                Assert.IsTrue(delayedDown.Join(TimeSpan.FromSeconds(2)));
                Assert.IsNull(notificationFailure);
                Tick(controller);

                Assert.IsTrue(LastPose(controller).KeyboardContact);
                Assert.AreNotEqual(idleHash, RawPixelHash(sink.LastFrame!),
                    "Keyboard release snapshots carry no sequence and must not swallow a delayed down tap.");
            }
            finally
            {
                if (delayedDown is not null) Assert.IsTrue(delayedDown.Join(TimeSpan.FromSeconds(2)));
            }
        });
    }

    [TestMethod]
    [DataRow(DesktopMouseButton.Left)]
    [DataRow(DesktopMouseButton.Right)]
    public void MouseClickBeforeFirstTick_PresentsOneVisiblePressThenSettles(DesktopMouseButton button)
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            using var idle = new Bitmap(sink.LastFrame!);
            controller.Start();

            state.UpdateMouseButton(button, true);
            controller.NotifyInputAvailable();
            state.UpdateMouseButton(button, false);
            controller.NotifyInputAvailable();

            Assert.AreEqual(1, sink.FrameCount, "两次输入通知都不能同步绘图。");
            Tick(controller);
            Assert.IsGreaterThan(1d, MeanPixelDifference(idle, sink.LastFrame!, new Rectangle(100, 220, 175, 140)),
                "两个 UI tick 之间完整发生的鼠标点击必须可见。");
            PumpUntil(() => !IsRendering(controller), 1000);
            Assert.IsFalse(IsRendering(controller));
            Assert.IsLessThan(0.5d, MeanPixelDifference(idle, sink.LastFrame!));
        });
    }

    private static void Tick(DesktopPetController controller) =>
        typeof(DesktopPetController).GetMethod("OnFrameTick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(controller, [null, EventArgs.Empty]);

    [TestMethod]
    public void RigConstructionFailure_ReplacesOldResourcesWithLiveStaticSourceUntilTeardown()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            Bitmap? loadedSource = null;
            WhiteBearRigRenderer? originalRig = null;
            var failRig = false;
            using var controller = CreateController(sink, state,
                () => loadedSource = LoadStaticArtwork(),
                _ => failRig ? throw new InvalidOperationException("injected layer extraction failure")
                    : originalRig = WhiteBearRigRenderer.Load());
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            var originalFrame = sink.LastFrame!;
            var successfulSource = loadedSource!;
            Assert.Throws<ArgumentException>(() => successfulSource.GetPixel(0, 0));
            controller.Start();

            failRig = true;
            controller.SetCharacter(AnimationCatalog.Characters[0]);

            Assert.Throws<ArgumentException>(() => originalFrame.GetPixel(0, 0));
            Assert.Throws<ObjectDisposedException>(() => originalRig!.Render(DesktopPetRigPose.Rest));
            var fallback = sink.LastFrame!;
            Assert.AreSame(loadedSource, fallback, "回退必须使用已经成功加载的源图。");
            Assert.AreEqual(0, fallback.GetPixel(0, 0).A);
            Assert.IsGreaterThan(fallback.Width * fallback.Height / 5, CountVisiblePixels(fallback));
            controller.Start();
            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            Assert.IsFalse(IsRendering(controller), "切层失败后的静态显示不能启动实时循环。");
            Assert.AreSame(fallback, sink.LastFrame);
            _ = fallback.GetPixel(250, 200);

            controller.Dispose();
            Assert.IsNull(sink.LastFrame, "释放回退帧前必须清除窗口借用。");
            Assert.Throws<ArgumentException>(() => fallback.GetPixel(0, 0));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SourceLoadFailure_PropagatesWithoutAttemptingRigFallback(bool corrupt)
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var rigAttempted = false;
            using var controller = CreateController(sink, CenteredInput(),
                () => throw (corrupt ? new ArgumentException("corrupt source") : new InvalidOperationException("missing source")),
                _ => { rigAttempted = true; return WhiteBearRigRenderer.Load(); });

            if (corrupt)
                Assert.Throws<ArgumentException>(() => controller.SetCharacter(AnimationCatalog.Characters[0]));
            else
                Assert.Throws<InvalidOperationException>(() => controller.SetCharacter(AnimationCatalog.Characters[0]));
            Assert.IsFalse(rigAttempted, "原图不可加载时不能尝试用切层回退掩盖错误。");
            Assert.IsNull(sink.LastFrame);
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void RigOutOfMemory_PropagatesAndReleasesTheLoadedSource()
    {
        RunOnStaThread(() =>
        {
            Bitmap? source = null;
            var sink = new FakeFrameSink();
            using var controller = CreateController(sink, CenteredInput(),
                () => source = LoadStaticArtwork(), _ => throw new OutOfMemoryException("injected process failure"));

            Assert.Throws<OutOfMemoryException>(() => controller.SetCharacter(AnimationCatalog.Characters[0]));

            Assert.IsNotNull(source);
            Assert.Throws<ArgumentException>(() => source.GetPixel(0, 0));
            Assert.IsNull(sink.LastFrame);
        });
    }

    private static DesktopPetController CreateController(
        IPetFrameSink sink, DesktopInputState state,
        Func<Bitmap> sourceLoader, Func<Bitmap, WhiteBearRigRenderer> rigFactory)
    {
        var constructor = typeof(DesktopPetController).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(IPetFrameSink), typeof(DesktopInputState), typeof(Func<Bitmap>), typeof(Func<Bitmap, WhiteBearRigRenderer>)], null);
        Assert.IsNotNull(constructor, "必须能够分别注入源图加载和切层构造，以验证失败边界与所有权。");
        return (DesktopPetController)constructor.Invoke([sink, state, sourceLoader, rigFactory]);
    }

    private static Bitmap LoadStaticArtwork() =>
        (Bitmap)typeof(WhiteBearRigRenderer).GetMethod("LoadArtwork", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null)!;

    [TestMethod]
    public void BurstNotifications_CoalesceToGeometryWithoutPerEventAllocationsOrSynchronousFrames()
    {
        RunOnStaThread(() =>
        {
            var state = CenteredInput();
            var sink = new FakeFrameSink();
            using var controller = new DesktopPetController(sink, state);
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            state.UpdateKey(0x41, false);
            controller.NotifyInputAvailable();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 4096; index++)
            {
                var key = 0x41 + index % 26;
                state.UpdateKey(key, true);
                controller.NotifyInputAvailable();
                state.UpdateKey(key, false);
                controller.NotifyInputAvailable();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.IsLessThan(16_384L, allocated, "通知路径应合并到固定字段，不能随事件次数分配队列或快照。");
            Assert.AreEqual(1, sink.FrameCount, "突发输入也不得在通知回调里绘图。");
            Assert.AreEqual(0, state.ReadSnapshot().ActiveVirtualKey);
            var pulseField = typeof(DesktopPetController).GetField("pendingKeyboardPulse",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.AreEqual(typeof(PointF?), pulseField.FieldType, "短按只允许保留映射后的坐标。");
            var target = (PointF)pulseField.GetValue(controller)!;
            Assert.AreEqual(0.68f, target.X, 0.001f); // The final tap is N.
            Assert.AreEqual(0.80f, target.Y, 0.001f);
            Tick(controller);
            Assert.IsNull(pulseField.GetValue(controller), "下一帧必须消费并清空一次性目标。");
        });
    }

    [TestMethod]
    public void RigFallback_RejectedPresentationReleasesTransferredArtwork()
    {
        RunOnStaThread(() =>
        {
            Bitmap? source = null;
            var sink = new FakeFrameSink { RejectNextFrame = true };
            using var controller = CreateController(sink, CenteredInput(),
                () => source = LoadStaticArtwork(), _ => throw new InvalidOperationException("injected layer failure"));

            Assert.Throws<InvalidOperationException>(() => controller.SetCharacter(AnimationCatalog.Characters[0]));

            Assert.IsNotNull(source);
            Assert.Throws<ArgumentException>(() => source.GetPixel(0, 0));
            Assert.IsNull(sink.LastFrame);
            Assert.IsFalse(IsRendering(controller));
        });
    }

    [TestMethod]
    public void RigFallback_RetryReleasesStaticFrameAndResumesLiveRendering()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            var failRig = true;
            using var controller = CreateController(sink, state, LoadStaticArtwork,
                _ => failRig ? throw new InvalidOperationException("injected layer failure") : WhiteBearRigRenderer.Load());
            controller.SetCharacter(AnimationCatalog.Characters[0]);
            controller.Start();
            var fallback = sink.LastFrame!;

            failRig = false;
            controller.SetCharacter(AnimationCatalog.Characters[0]);

            Assert.Throws<ArgumentException>(() => fallback.GetPixel(0, 0));
            Assert.IsTrue(IsRendering(controller));
            var initial = sink.LastFrame!;
            state.UpdateKey(0x41, true);
            controller.NotifyInputAvailable();
            Tick(controller);
            Assert.AreNotSame(initial, sink.LastFrame);
        });
    }

    [TestMethod]
    public void RigFallback_ClearsPulsesCapturedDuringFactoryFailure()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            using var barrier = new Barrier(2);
            Task? notification = null;
            DesktopPetController? controller = null;
            using var ownedController = controller = CreateController(sink, state, LoadStaticArtwork, _ =>
            {
                notification = Task.Run(() =>
                {
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(2)));
                    TapAllInputs(controller!, state);
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(2)));
                });
                Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(2)));
                Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(2)));
                throw new InvalidOperationException("injected failure after complete background tap");
            });
            controller.Start();

            controller.SetCharacter(AnimationCatalog.Characters[0]);
            notification!.GetAwaiter().GetResult();

            AssertFallbackHasNoPulses(controller, sink);
            Task.Run(() => TapAllInputs(controller, state)).GetAwaiter().GetResult();
            AssertFallbackHasNoPulses(controller, sink);
        });
    }

    [TestMethod]
    public void RigFallback_RejectsNotificationAlreadyWaitingForPulseGate()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            var state = CenteredInput();
            Thread? notification = null;
            Exception? notificationFailure = null;
            DesktopPetController? controller = null;
            using var ownedController = controller = CreateController(sink, state, LoadStaticArtwork, _ =>
            {
                notification = new Thread(() =>
                {
                    try { TapAllInputs(controller!, state); }
                    catch (Exception exception) { notificationFailure = exception; }
                }) { IsBackground = true };
                notification.Start();
                // This worker has no waits or other contended locks before Notify's pulseGate.
                // Holding that gate on the owner thread lets fallback enter it reentrantly
                // while the notification remains blocked after its early fallback check.
                Assert.IsTrue(SpinWait.SpinUntil(
                    () => (notification.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(2)), "通知必须先进入等待 pulseGate 的交错位置。");
                Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey);
                throw new InvalidOperationException("injected failure with notification waiting at pulseGate");
            });
            var gate = typeof(DesktopPetController).GetField("pulseGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            controller.Start();
            try
            {
                lock (gate)
                {
                    controller.SetCharacter(AnimationCatalog.Characters[0]);
                    AssertFallbackHasNoPulses(controller, sink);
                }

                Assert.IsTrue(notification!.Join(TimeSpan.FromSeconds(2)), "回退建立后必须允许已等待的通知及时退出。");
                Assert.IsNull(notificationFailure);
                AssertFallbackHasNoPulses(controller, sink);
                Task.Run(() => TapAllInputs(controller, state)).GetAwaiter().GetResult();
                AssertFallbackHasNoPulses(controller, sink);
            }
            finally
            {
                if (notification is not null)
                    Assert.IsTrue(notification.Join(TimeSpan.FromSeconds(2)));
            }
        });
    }

    private static void TapAllInputs(DesktopPetController controller, DesktopInputState state)
    {
        state.UpdateKey(0x41, true);
        state.UpdateMouseButton(DesktopMouseButton.Left, true);
        state.UpdateMouseButton(DesktopMouseButton.Right, true);
        controller.NotifyInputAvailable();
        state.UpdateKey(0x41, false);
        state.UpdateMouseButton(DesktopMouseButton.Left, false);
        state.UpdateMouseButton(DesktopMouseButton.Right, false);
        controller.NotifyInputAvailable();
    }

    private static void AssertFallbackHasNoPulses(DesktopPetController controller, FakeFrameSink sink)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(DesktopPetController);
        var pulses = (
            (PointF?)type.GetField("pendingKeyboardPulse", flags)!.GetValue(controller),
            (bool)type.GetField("pendingLeftPulse", flags)!.GetValue(controller)!,
            (bool)type.GetField("pendingRightPulse", flags)!.GetValue(controller)!);
        Assert.AreEqual(((PointF?)null, false, false), pulses,
            "静态回退不能保留无机会消费的键盘目标或左右键视觉脉冲。");
        Assert.IsFalse(IsRendering(controller));
        Assert.IsNotNull(sink.LastFrame);
        _ = sink.LastFrame.GetPixel(0, 0);
    }

    private sealed class FakeFrameSink : IPetFrameSink
    {
        public Bitmap? LastFrame { get; private set; }
        public int FrameCount { get; private set; }
        public int LastPresentationThread { get; private set; }
        public bool CheckedPreviousFrame { get; private set; }
        public bool RejectNextFrame { get; set; }
        public Action<Bitmap>? FramePresented { get; set; }

        public void SetFrame(Bitmap frame)
        {
            if (RejectNextFrame)
            {
                RejectNextFrame = false;
                throw new InvalidOperationException("injected sink rejection");
            }
            if (LastFrame is not null)
            {
                _ = LastFrame.GetPixel(0, 0);
                CheckedPreviousFrame = true;
            }
            _ = frame.GetPixel(0, 0);
            FramePresented?.Invoke(frame);
            LastFrame = frame;
            FrameCount++;
            LastPresentationThread = Environment.CurrentManagedThreadId;
        }

        public void ClearFrame()
        {
            if (LastFrame is not null) _ = LastFrame.GetPixel(0, 0);
            LastFrame = null;
        }
    }

    private static bool IsRendering(DesktopPetController controller) =>
        (bool)typeof(DesktopPetController).GetProperty("IsRendering", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;

    private static DesktopPetRigPose LastPose(DesktopPetController controller) =>
        (DesktopPetRigPose)typeof(DesktopPetController).GetField("lastPose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;

    private static DesktopInputState CenteredInput()
    {
        var state = new DesktopInputState();
        CenterPointer(state);
        return state;
    }

    private static void CenterPointer(DesktopInputState state)
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        state.UpdatePointer(area.Left + area.Width / 2, area.Top + area.Height / 2);
    }

    private static void PumpUntil(Func<bool> done, int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (!done() && Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static void PumpEvents(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static int CountVisiblePixels(Bitmap bitmap)
    {
        var visiblePixels = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A >= 32)
                {
                    visiblePixels++;
                }
            }
        }

        return visiblePixels;
    }

    private static double MeanPixelDifference(Bitmap first, Bitmap second, Rectangle? region = null)
    {
        Assert.AreEqual(first.Size, second.Size);
        long difference = 0;
        var bounds = region ?? new Rectangle(Point.Empty, first.Size);
        var samples = 0;
        for (var y = bounds.Top; y < bounds.Bottom; y += 4)
        {
            for (var x = bounds.Left; x < bounds.Right; x += 4)
            {
                samples++;
                var a = first.GetPixel(x, y);
                var b = second.GetPixel(x, y);
                difference += Math.Abs(a.A - b.A);
                difference += Math.Abs(a.R - b.R);
                difference += Math.Abs(a.G - b.G);
                difference += Math.Abs(a.B - b.B);
            }
        }

        return difference / (samples * 4d);
    }

    private static string RawPixelHash(Bitmap frame)
    {
        var data = frame.LockBits(new Rectangle(Point.Empty, frame.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { frame.UnlockBits(data); }
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "消息泵必须在时限内返回，不能被渲染定时器占满。");

        if (failure is not null)
        {
            throw failure;
        }
    }
}
