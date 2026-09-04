using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
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
    public void PlaceholderMode_TapsAlternateDerivedFramesAndSilenceRestoresIdle()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            using var controller = new DesktopPetController(sink);
            var character = AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId)!;
            Assert.IsFalse(character.HasPetAssets, "白熊尚无宠物素材，应走占位模式。");

            controller.SetCharacter(character);
            var idleFrame = sink.LastFrame;
            Assert.IsNotNull(idleFrame, "设置角色后应立即呈现待机帧。");

            controller.OnKeyTapped();
            var leftTap = sink.LastFrame;
            Assert.AreNotSame(idleFrame, leftTap, "第一次敲击应呈现左爪派生帧。");

            controller.OnKeyTapped();
            var rightTap = sink.LastFrame;
            Assert.AreNotSame(leftTap, rightTap, "连续敲击应换另一只爪。");

            controller.OnKeyTapped();
            Assert.AreSame(leftTap, sink.LastFrame, "第三次敲击应回到左爪帧。");

            PumpEvents(700);
            Assert.AreSame(idleFrame, sink.LastFrame, "静默超时后应恢复待机帧。");
        });
    }

    [TestMethod]
    public void PlaceholderMode_InitialFrameHasMeaningfulVisibleContent()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            using var controller = new DesktopPetController(sink);
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
    public void WhiteBearPet_UsesProvidedArtworkAndProducesVisibleAlternatingTypingFrames()
    {
        RunOnStaThread(() =>
        {
            var sink = new FakeFrameSink();
            using var controller = new DesktopPetController(sink);
            var character = AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId)!;

            controller.SetCharacter(character);
            var idle = sink.LastFrame!;

            Assert.AreEqual(2400d / 1792d, (double)idle.Width / idle.Height, 0.01,
                "白熊桌宠必须沿用用户提供图片的原始构图比例。");
            Assert.AreEqual(0, idle.GetPixel(0, 0).A,
                "与画面边缘相连的白色背景必须透明，不能显示成白色矩形窗口。");
            Assert.IsGreaterThan((idle.Width * idle.Height) / 10, CountVisiblePixels(idle),
                "透明化背景后必须保留白熊、鼠标和键盘主体。");

            controller.OnKeyTapped();
            var firstTap = sink.LastFrame!;
            controller.OnKeyTapped();
            var secondTap = sink.LastFrame!;

            Assert.IsGreaterThan(2.0, MeanPixelDifference(idle, firstTap),
                "第一次按键必须产生肉眼可见的敲击帧，不能只是几乎不可察觉的抖动。");
            Assert.IsGreaterThan(2.0, MeanPixelDifference(firstTap, secondTap),
                "连续按键必须交替呈现两种不同的敲击动作。");
        });
    }

    private sealed class FakeFrameSink : IPetFrameSink
    {
        public Bitmap? LastFrame { get; private set; }

        public void SetFrame(Bitmap frame) => LastFrame = frame;
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

    private static double MeanPixelDifference(Bitmap first, Bitmap second)
    {
        Assert.AreEqual(first.Size, second.Size);
        long difference = 0;
        for (var y = 0; y < first.Height; y += 4)
        {
            for (var x = 0; x < first.Width; x += 4)
            {
                var a = first.GetPixel(x, y);
                var b = second.GetPixel(x, y);
                difference += Math.Abs(a.A - b.A);
                difference += Math.Abs(a.R - b.R);
                difference += Math.Abs(a.G - b.G);
                difference += Math.Abs(a.B - b.B);
            }
        }

        var sampledPixels = ((first.Width + 3) / 4) * ((first.Height + 3) / 4);
        return difference / (sampledPixels * 4d);
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
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }
}
