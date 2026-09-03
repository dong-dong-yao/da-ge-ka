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
