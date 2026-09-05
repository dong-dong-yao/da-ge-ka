using System.Drawing;
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
        state.UpdateKey(0, true);
        state.UpdateKey(-1, true);
        state.UpdateKey(256, true);
        state.UpdateKey(0x41, true);
        state.UpdateKey(0x41, false);

        Assert.AreEqual(0, state.ReadSnapshot().ActiveVirtualKey);
    }

    [TestMethod]
    public void UpdateKey_RepeatKeyDownDoesNotChangePressOrder()
    {
        var state = new DesktopInputState();
        state.UpdateKey(0x41, true);
        state.UpdateKey(0x44, true);
        state.UpdateKey(0x41, true);
        state.UpdateKey(0x44, false);

        Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey);
    }
}
