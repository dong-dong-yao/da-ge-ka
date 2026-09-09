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
    public void KeyPressSequence_OnlyAdvancesOnANewDownTransition()
    {
        var state = new DesktopInputState();
        state.UpdateKey(0x41, true);
        var first = state.ReadSnapshot().ActiveKeyPressSequence;
        Assert.IsGreaterThan(0L, first);

        state.UpdateKey(0x41, true);
        Assert.AreEqual(first, state.ReadSnapshot().ActiveKeyPressSequence,
            "A repeat key-down must keep the original physical press sequence.");
        state.UpdateKey(0x41, false);
        state.UpdateKey(0x41, true);

        Assert.IsGreaterThan(first, state.ReadSnapshot().ActiveKeyPressSequence,
            "Only a new up-to-down transition may advance the key press sequence.");
    }

    [TestMethod]
    public void ReleasingLatestKey_FallbackExposesEarlierHeldKeysOriginalSequence()
    {
        var state = new DesktopInputState();
        state.UpdateKey(0x41, true);
        var first = state.ReadSnapshot().ActiveKeyPressSequence;
        state.UpdateKey(0x44, true);
        var latest = state.ReadSnapshot().ActiveKeyPressSequence;
        Assert.IsGreaterThan(first, latest);

        state.UpdateKey(0x44, false);
        var fallback = state.ReadSnapshot();

        Assert.AreEqual(0x41, fallback.ActiveVirtualKey);
        Assert.AreEqual(first, fallback.ActiveKeyPressSequence,
            "Fallback must expose the earlier still-held key's old sequence, not invent a new press.");
    }

    [TestMethod]
    public void MousePressSequences_AdvanceOnlyOnFalseToTruePerButton()
    {
        var state = new DesktopInputState();
        Assert.AreEqual(0L, state.ReadSnapshot().LeftButtonPressSequence);
        Assert.AreEqual(0L, state.ReadSnapshot().RightButtonPressSequence);

        state.UpdateMouseButton(DesktopMouseButton.Left, true);
        var leftFirst = state.ReadSnapshot().LeftButtonPressSequence;
        Assert.IsGreaterThan(0L, leftFirst);
        state.UpdatePointer(10, 20);
        state.UpdateMouseButton(DesktopMouseButton.Left, true);
        Assert.AreEqual(leftFirst, state.ReadSnapshot().LeftButtonPressSequence,
            "Mouse movement and repeated down notifications must not create another click.");
        state.UpdateMouseButton(DesktopMouseButton.Left, false);
        Assert.AreEqual(leftFirst, state.ReadSnapshot().LeftButtonPressSequence,
            "Release keeps the last monotonic sequence available for deduplication.");
        state.UpdateMouseButton(DesktopMouseButton.Left, true);
        Assert.IsGreaterThan(leftFirst, state.ReadSnapshot().LeftButtonPressSequence);

        state.UpdateMouseButton(DesktopMouseButton.Right, true);
        var rightFirst = state.ReadSnapshot().RightButtonPressSequence;
        Assert.IsGreaterThan(0L, rightFirst);
        state.UpdateMouseButton(DesktopMouseButton.Right, true);
        Assert.AreEqual(rightFirst, state.ReadSnapshot().RightButtonPressSequence);
        state.UpdateMouseButton(DesktopMouseButton.Right, false);
        state.UpdateMouseButton(DesktopMouseButton.Right, true);
        Assert.IsGreaterThan(rightFirst, state.ReadSnapshot().RightButtonPressSequence);
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
