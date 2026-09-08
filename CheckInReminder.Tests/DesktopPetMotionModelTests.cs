using System.Drawing;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class DesktopPetMotionModelTests
{
    private static readonly TimeSpan OneFrame = TimeSpan.FromMilliseconds(1000d / 60d);

    [TestMethod]
    public void MouseMotion_ApproachesTargetMonotonicallyAndStaysBounded()
    {
        var model = new DesktopPetMotionModel();
        var input = new DesktopInputSnapshot(new Point(1920, 1080), false, false, 0, 1);
        var area = new Rectangle(0, 0, 1920, 1080);

        var first = model.Step(input, area, OneFrame);
        var second = model.Step(input, area, OneFrame);

        Assert.AreEqual(0.25f, first.MouseOffset.X, 0.0001f);
        Assert.AreEqual(0.4375f, second.MouseOffset.X, 0.0001f);
        Assert.IsGreaterThan(first.MouseOffset.X, second.MouseOffset.X);
        Assert.IsLessThanOrEqualTo(1f, second.MouseOffset.X);
        Assert.IsLessThanOrEqualTo(1f, second.MouseOffset.Y);
    }

    [TestMethod]
    public void CursorNormalization_UsesWorkingAreaOriginAndClampsOutsideEdges()
    {
        var model = new DesktopPetMotionModel();
        var area = new Rectangle(100, 200, 800, 600);

        var pose = model.Step(
            new DesktopInputSnapshot(new Point(1_000, 100), false, false, 0, 1),
            area,
            TimeSpan.FromSeconds(5));

        Assert.AreEqual(1f, pose.MouseOffset.X, 0.002f);
        Assert.AreEqual(-1f, pose.MouseOffset.Y, 0.002f);
    }

    [TestMethod]
    public void EmptyWorkingArea_UsesNeutralBoundedMouseTarget()
    {
        var model = new DesktopPetMotionModel();

        var pose = model.Step(
            new DesktopInputSnapshot(new Point(int.MaxValue, int.MinValue), false, false, 0, 1),
            Rectangle.Empty,
            OneFrame);

        Assert.AreEqual(PointF.Empty, pose.MouseOffset);
        Assert.IsTrue(pose.IsAtRest);
    }

    [TestMethod]
    public void MouseButtons_DrivePressAndOpposingTiltTargets()
    {
        var leftModel = new DesktopPetMotionModel();
        var rightModel = new DesktopPetMotionModel();
        var bothModel = new DesktopPetMotionModel();
        var area = new Rectangle(0, 0, 100, 100);

        var left = leftModel.Step(Snapshot(left: true), area, OneFrame);
        var right = rightModel.Step(Snapshot(right: true), area, OneFrame);
        var both = bothModel.Step(Snapshot(left: true, right: true), area, OneFrame);

        Assert.IsGreaterThan(0f, left.MousePress);
        Assert.AreEqual(left.MousePress, right.MousePress, 0.0001f);
        Assert.AreEqual(left.MousePress, both.MousePress, 0.0001f);
        Assert.IsLessThan(0f, left.MouseRotationDegrees);
        Assert.IsGreaterThan(0f, right.MouseRotationDegrees);
        Assert.AreEqual(0f, both.MouseRotationDegrees, 0.0001f);
        Assert.IsFalse(left.IsAtRest);
    }

    [TestMethod]
    public void KeyboardPress_MapsActiveKeyAndReturnsToRestAfterRelease()
    {
        var model = new DesktopPetMotionModel();
        var area = new Rectangle(0, 0, 1920, 1080);
        var pressed = model.Step(
            new DesktopInputSnapshot(new Point(960, 540), false, false, 0x47, 1),
            area,
            OneFrame);

        Assert.IsGreaterThan(0f, pressed.KeyboardPress);
        Assert.AreEqual(0.50f, pressed.KeyboardTarget.X, 0.001f);
        Assert.AreEqual(0.5125f, pressed.KeyboardTarget.Y, 0.001f);
        Assert.IsFalse(pressed.IsAtRest);

        DesktopPetRigPose released = default;
        for (var frame = 0; frame < 120; frame++)
        {
            released = model.Step(
                new DesktopInputSnapshot(new Point(960, 540), false, false, 0, 2),
                area,
                OneFrame);
        }

        Assert.AreEqual(0.5f, released.KeyboardTarget.X, 0.002f);
        Assert.AreEqual(0.5f, released.KeyboardTarget.Y, 0.002f);
        Assert.IsTrue(released.IsAtRest);
    }

    [TestMethod]
    public void UnknownActiveKey_ReboundsAtTheLastValidKeyboardTarget()
    {
        var model = new DesktopPetMotionModel();
        var area = new Rectangle(0, 0, 100, 100);
        var valid = model.Step(Snapshot(key: 0x51), area, OneFrame);

        var unknown = model.Step(Snapshot(key: 0x2E), area, OneFrame);

        Assert.AreEqual(valid.KeyboardTarget.X, unknown.KeyboardTarget.X,
            "Keep the same pressed sprite while a released or unsupported key rebounds.");
        Assert.IsLessThan(valid.KeyboardPress, unknown.KeyboardPress);
    }

    [TestMethod]
    [DataRow(0x51, 0.10f)]
    [DataRow(0x47, 0.50f)]
    [DataRow(0x50, 0.90f)]
    public void FirstKeySample_SelectsItsOwnZoneAndHoldsItThroughoutRelease(int key, float targetX)
    {
        var model = new DesktopPetMotionModel();
        var area = new Rectangle(0, 0, 100, 100);
        var pressed = model.Step(Snapshot(key: key), area, OneFrame);
        Assert.AreEqual(targetX, pressed.KeyboardTarget.X, 0.001f,
            "The first strike must select the actual key zone, without crossing intermediate sprites.");
        for (var frame = 0; frame < 30; frame++) pressed = model.Step(Snapshot(key: key), area, OneFrame);
        Assert.IsGreaterThan(0.99f, pressed.KeyboardPress);
        Assert.IsFalse(pressed.IsAtRest);
        var released = model.Step(Snapshot(), area, OneFrame);
        Assert.AreEqual(targetX, released.KeyboardTarget.X, 0.001f);
        Assert.IsLessThan(pressed.KeyboardPress, released.KeyboardPress);
        for (var frame = 0; frame < 120; frame++) released = model.Step(Snapshot(), area, OneFrame);
        Assert.AreEqual(0.5f, released.KeyboardTarget.X, 0.002f);
        Assert.IsTrue(released.IsAtRest);
    }

    [TestMethod]
    public void ZeroOrNegativeElapsed_DoesNotMoveState()
    {
        var model = new DesktopPetMotionModel();
        var area = new Rectangle(0, 0, 100, 100);

        var zero = model.Step(Snapshot(cursor: new Point(100, 100), key: 0x4D), area, TimeSpan.Zero);
        var negative = model.Step(
            Snapshot(cursor: new Point(100, 100), key: 0x4D),
            area,
            TimeSpan.FromMilliseconds(-20));

        Assert.AreEqual(PointF.Empty, zero.MouseOffset);
        Assert.AreEqual(new PointF(0.5f, 0.5f), zero.KeyboardTarget);
        Assert.AreEqual(zero, negative);
        Assert.IsFalse(zero.IsAtRest, "Input remains active even when elapsed time is zero.");
    }

    private static DesktopInputSnapshot Snapshot(
        Point? cursor = null,
        bool left = false,
        bool right = false,
        int key = 0) =>
        new(cursor ?? new Point(50, 50), left, right, key, 1);
}
