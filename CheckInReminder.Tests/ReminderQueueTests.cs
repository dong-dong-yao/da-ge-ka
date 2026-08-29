using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class ReminderQueueTests
{
    [TestMethod]
    public void FirstRequest_IsShownImmediately()
    {
        var queue = new ReminderQueue();

        Assert.AreEqual(ReminderQueueAction.Show, queue.Request(ReminderKind.Break));
        Assert.AreEqual(ReminderKind.Break, queue.Active);
    }

    [TestMethod]
    public void CheckInRequest_ReplacesActiveBreakReminder()
    {
        var queue = new ReminderQueue();
        queue.Request(ReminderKind.Break);

        Assert.AreEqual(ReminderQueueAction.ReplaceCurrent, queue.Request(ReminderKind.Morning));
        Assert.AreEqual(ReminderKind.Morning, queue.Active);
        Assert.AreEqual(ReminderKind.Break, queue.CompleteCurrent());
        Assert.IsNull(queue.CompleteCurrent());
    }

    [TestMethod]
    public void RepeatedBreakRequests_QueueOnlyOneBreak()
    {
        var queue = new ReminderQueue();
        queue.Request(ReminderKind.Morning);

        Assert.AreEqual(ReminderQueueAction.Queue, queue.Request(ReminderKind.Break));
        Assert.AreEqual(ReminderQueueAction.Queue, queue.Request(ReminderKind.Break));
        Assert.AreEqual(ReminderKind.Break, queue.CompleteCurrent());
        Assert.IsNull(queue.CompleteCurrent());
    }

    [TestMethod]
    public void CheckInRequest_DoesNotDuplicateAnActiveCheckInFlow()
    {
        var queue = new ReminderQueue();
        queue.Request(ReminderKind.Evening);

        Assert.AreEqual(ReminderQueueAction.Ignore, queue.Request(ReminderKind.Evening));
        Assert.AreEqual(ReminderKind.Evening, queue.Active);
    }

    [TestMethod]
    public void CancelPendingBreak_PreventsItFromAppearingAfterCheckIn()
    {
        var queue = new ReminderQueue();
        queue.Request(ReminderKind.Morning);
        queue.Request(ReminderKind.Break);

        queue.CancelPendingBreak();

        Assert.IsNull(queue.CompleteCurrent());
    }
}
