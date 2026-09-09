using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class PetKeyboardPoseMapperTests
{
    [TestMethod]
    public void ThreePoseArtwork_MapsQGPAcrossLeftCenterRight()
    {
        Assert.AreEqual(PetKeyboardPose.Left, Map(0x51, hasCenterPose: true));
        Assert.AreEqual(PetKeyboardPose.Center, Map(0x47, hasCenterPose: true));
        Assert.AreEqual(PetKeyboardPose.Right, Map(0x50, hasCenterPose: true));
    }

    [TestMethod]
    public void TwoPoseArtwork_MapsCenterToNearestAvailableSide()
    {
        Assert.AreEqual(PetKeyboardPose.Left, Map(0x51, hasCenterPose: false));
        Assert.AreEqual(PetKeyboardPose.Right, Map(0x47, hasCenterPose: false));
        Assert.AreEqual(PetKeyboardPose.Right, Map(0x50, hasCenterPose: false));
    }

    private static PetKeyboardPose Map(int virtualKey, bool hasCenterPose)
    {
        Assert.IsTrue(KeyboardTargetMapper.TryMap(virtualKey, out var target));
        return PetKeyboardPoseMapper.Map(target.X, hasCenterPose);
    }
}
