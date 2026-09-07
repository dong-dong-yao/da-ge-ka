using CheckInReminder;
using System.Reflection;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopPetControllerTests
{
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

    private sealed class FakeFrameSink : IPetFrameSink
    {
        public Bitmap? LastFrame { get; private set; }
        public int FrameCount { get; private set; }
        public int LastPresentationThread { get; private set; }
        public bool CheckedPreviousFrame { get; private set; }

        public void SetFrame(Bitmap frame)
        {
            if (LastFrame is not null)
            {
                _ = LastFrame.GetPixel(0, 0);
                CheckedPreviousFrame = true;
            }
            _ = frame.GetPixel(0, 0);
            LastFrame = frame;
            FrameCount++;
            LastPresentationThread = Environment.CurrentManagedThreadId;
        }

        public void ClearFrame() => LastFrame = null;
    }

    private static bool IsRendering(DesktopPetController controller) =>
        (bool)typeof(DesktopPetController).GetProperty("IsRendering", BindingFlags.Instance | BindingFlags.NonPublic)!
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
