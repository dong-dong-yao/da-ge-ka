using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class AnimationTimelineTests
{
    [TestMethod]
    public void OneShotTimeline_MapsElapsedTimeAcrossAllFrames()
    {
        var timeline = new AnimationTimeline(86, TimeSpan.FromSeconds(7.104), loop: false);

        Assert.AreEqual(0, timeline.GetFrameIndex(TimeSpan.Zero));
        Assert.AreEqual(43, timeline.GetFrameIndex(TimeSpan.FromSeconds(3.552)));
        Assert.AreEqual(85, timeline.GetFrameIndex(TimeSpan.FromSeconds(7.104)));
        Assert.IsTrue(timeline.HasFinished(TimeSpan.FromSeconds(7.104)));
    }

    [TestMethod]
    public void LoopTimeline_RestartsAtFrameZeroAfterFullDuration()
    {
        var duration = TimeSpan.FromSeconds(4.086009);
        var timeline = new AnimationTimeline(50, duration, loop: true);

        Assert.AreEqual(0, timeline.GetFrameIndex(duration));
        Assert.AreEqual(0, timeline.GetFrameIndex(duration + TimeSpan.FromMilliseconds(1)));
        Assert.IsFalse(timeline.HasFinished(duration + duration));
    }

    [TestMethod]
    public void Timeline_RejectsEmptySequences()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new AnimationTimeline(0, TimeSpan.FromSeconds(1), loop: false));
    }
}
