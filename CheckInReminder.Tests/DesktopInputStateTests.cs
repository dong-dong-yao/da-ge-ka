using System.Drawing;
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
    public void ConcurrentKeyUpdates_AreSerializableAndReleaseFallsBack()
    {
        var state = new DesktopInputState();
        using var barrier = new Barrier(2);
        var first = Task.Run(() =>
        {
            barrier.SignalAndWait();
            state.UpdateKey(0x41, true);
        });
        var second = Task.Run(() =>
        {
            barrier.SignalAndWait();
            state.UpdateKey(0x44, true);
        });

        Task.WaitAll(first, second);
        var active = state.ReadSnapshot().ActiveVirtualKey;
        Assert.IsTrue(active is 0x41 or 0x44);

        state.UpdateKey(active, false);
        Assert.AreEqual(active == 0x41 ? 0x44 : 0x41, state.ReadSnapshot().ActiveVirtualKey);
        state.UpdateKey(active == 0x41 ? 0x44 : 0x41, false);
        Assert.AreEqual(0, state.ReadSnapshot().ActiveVirtualKey);
    }
}
