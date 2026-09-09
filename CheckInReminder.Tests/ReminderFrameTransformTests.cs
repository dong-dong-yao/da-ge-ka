using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class ReminderFrameTransformTests
{
    [TestMethod]
    public void Catalog_DeclaresTheActualSourceEdgeOfEachAnimation()
    {
        var expected = new Dictionary<string, ScreenEdge>
        {
            ["white-bear"] = ScreenEdge.Right,
            ["yellow-hippo"] = ScreenEdge.Right,
            ["blue-hat-cat"] = ScreenEdge.Bottom,
            ["stick-dog"] = ScreenEdge.Left,
            ["scooter-dinosaur"] = ScreenEdge.Left,
        };

        foreach (var character in AnimationCatalog.Characters)
            Assert.AreEqual(expected[character.Id], character.SourceEdge, character.Id);
    }

    [TestMethod]
    public void BottomAuthoredCat_UsesTheCorrectTransformForEveryScreenEdge()
    {
        Assert.AreEqual(ReminderFrameTransform.None,
            ReminderFrameTransformResolver.Resolve(ScreenEdge.Bottom, ScreenEdge.Bottom));
        Assert.AreEqual(ReminderFrameTransform.FlipVertical,
            ReminderFrameTransformResolver.Resolve(ScreenEdge.Bottom, ScreenEdge.Top));
        Assert.AreEqual(ReminderFrameTransform.RotateClockwise,
            ReminderFrameTransformResolver.Resolve(ScreenEdge.Bottom, ScreenEdge.Left));
        Assert.AreEqual(ReminderFrameTransform.RotateCounterClockwise,
            ReminderFrameTransformResolver.Resolve(ScreenEdge.Bottom, ScreenEdge.Right));
    }

    [TestMethod]
    public void LeftAuthoredSideCharacters_AreUnchangedOnLeftAndMirroredOnRight()
    {
        Assert.AreEqual(ReminderFrameTransform.None,
            ReminderFrameTransformResolver.Resolve(ScreenEdge.Left, ScreenEdge.Left));
        Assert.AreEqual(ReminderFrameTransform.FlipHorizontal,
            ReminderFrameTransformResolver.Resolve(ScreenEdge.Left, ScreenEdge.Right));
    }
}
