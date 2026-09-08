using System.Drawing;
using System.Reflection;
using System.Threading;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class DesktopInputStateTests
{
    [TestMethod]
    public void ActiveKey_FallsBackToPreviouslyHeldKeyWhenLatestIsReleased()
    {
        var state = new DesktopInputState();
        state.UpdateKey(0x41, true);
        state.UpdateKey(0x44, true);
        Assert.AreEqual(0x44, state.ReadSnapshot().ActiveVirtualKey);

        state.UpdateKey(0x44, false);
        Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey);
        state.UpdateKey(0x41, false);
        Assert.AreEqual(0, state.ReadSnapshot().ActiveVirtualKey);
    }

    [TestMethod]
    public void Snapshot_CoalescesPointerAndTracksButtonsWithoutText()
    {
        var state = new DesktopInputState();
        state.UpdatePointer(100, 200);
        state.UpdatePointer(320, 480);
        state.UpdateMouseButton(DesktopMouseButton.Left, true);

        var snapshot = state.ReadSnapshot();
        Assert.AreEqual(new Point(320, 480), snapshot.CursorScreen);
        Assert.IsTrue(snapshot.LeftButtonDown);
        Assert.IsFalse(snapshot.RightButtonDown);
    }

    [TestMethod]
    public void UpdateKey_IgnoresKeysOutsideVirtualKeyRange()
    {
        var state = new DesktopInputState();
        state.UpdatePointer(12, 34);
        state.UpdateMouseButton(DesktopMouseButton.Right, true);
        var before = state.ReadSnapshot();

        state.UpdateKey(0, true);
        state.UpdateKey(-1, true);
        state.UpdateKey(256, true);
        state.UpdateKey(0, false);
        state.UpdateKey(-1, false);
        state.UpdateKey(256, false);
        state.UpdateKey(1024, false);

        Assert.AreEqual(before, state.ReadSnapshot());
    }

    [TestMethod]
    public void UpdateKey_RepeatKeyDownDoesNotChangePressOrder()
    {
        var state = new DesktopInputState();
        state.UpdateKey(0x41, true);
        state.UpdateKey(0x44, true);
        state.UpdateKey(0x41, true);
        Assert.AreEqual(0x44, state.ReadSnapshot().ActiveVirtualKey);
        state.UpdateKey(0x44, false);

        Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey);
    }

    [TestMethod]
    public void KeyRelease_ImmediatelyErasesPressedIdentityAndItsOrderSlot()
    {
        var state = new DesktopInputState();
        state.UpdateKey(0x41, true);
        state.UpdateKey(0x44, true);

        state.UpdateKey(0x44, false);

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pressed = (int[])typeof(DesktopInputState).GetField("pressedKeys", flags)!.GetValue(state)!;
        var orders = (long[])typeof(DesktopInputState).GetField("pressOrders", flags)!.GetValue(state)!;
        Assert.AreEqual(0, pressed[0x44], "松键后不能留下该键的按下标记。");
        Assert.AreEqual(0L, orders[0x44], "松键后不能从次序槽恢复已释放的键身份。");
        Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey, "仍按住的键必须继续生效。");
    }

    [TestMethod]
    public void KeyUpdate_WaitsForGateAndCompletesAfterRelease()
    {
        var state = new DesktopInputState();
        var gate = typeof(DesktopInputState)
            .GetField("keyUpdateGate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(state)!;
        using var started = new ManualResetEventSlim();
        Task update;
        Monitor.Enter(gate);
        try
        {
            update = Task.Run(() =>
            {
                started.Set();
                state.UpdateKey(0x41, true);
            });
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsFalse(update.Wait(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            Monitor.Exit(gate);
        }

        Assert.IsTrue(update.Wait(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey);
    }
}
