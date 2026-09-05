using System.Drawing;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class KeyboardTargetMapperTests
{
    [TestMethod]
    [DataRow(0x51, 0.10f, 0.28f)] // Q: upper-left letter area
    [DataRow(0x47, 0.50f, 0.55f)] // G: center
    [DataRow(0x4D, 0.78f, 0.80f)] // M: lower-right letter area
    [DataRow(0x20, 0.50f, 0.92f)] // Space: bottom-center
    public void RepresentativeKeys_MapInsideExpectedKeyboardRegion(
        int virtualKey,
        float expectedX,
        float expectedY)
    {
        Assert.IsTrue(KeyboardTargetMapper.TryMap(virtualKey, out var target));
        Assert.AreEqual(expectedX, target.X, 0.12f);
        Assert.AreEqual(expectedY, target.Y, 0.12f);
        Assert.IsTrue(target.X is >= 0 and <= 1);
        Assert.IsTrue(target.Y is >= 0 and <= 1);
    }

    [TestMethod]
    [DataRow(0x1B)] // Escape
    [DataRow(0x70)] // F1
    [DataRow(0x7B)] // F12
    [DataRow(0x30)] // 0
    [DataRow(0x39)] // 9
    [DataRow(0x51)] // Q
    [DataRow(0x50)] // P
    [DataRow(0x41)] // A
    [DataRow(0x4C)] // L
    [DataRow(0x5A)] // Z
    [DataRow(0x4D)] // M
    [DataRow(0x20)] // Space
    [DataRow(0x0D)] // Enter
    [DataRow(0x08)] // Backspace
    [DataRow(0x09)] // Tab
    [DataRow(0x10)] // Shift
    [DataRow(0xA0)] // Left Shift
    [DataRow(0xA1)] // Right Shift
    [DataRow(0x11)] // Control
    [DataRow(0xA2)] // Left Control
    [DataRow(0xA3)] // Right Control
    [DataRow(0x12)] // Alt
    [DataRow(0xA4)] // Left Alt
    [DataRow(0xA5)] // Right Alt
    public void SupportedKeyboardZones_MapToNormalizedCoordinates(int virtualKey)
    {
        Assert.IsTrue(KeyboardTargetMapper.TryMap(virtualKey, out var target));
        Assert.IsTrue(target.X is >= 0 and <= 1, $"0x{virtualKey:X2} X={target.X}");
        Assert.IsTrue(target.Y is >= 0 and <= 1, $"0x{virtualKey:X2} Y={target.Y}");
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(0x2E)] // Delete is outside the requested logical keyboard.
    [DataRow(0xFF)]
    public void UnknownKeys_ReturnFalseAndAnEmptyTarget(int virtualKey)
    {
        Assert.IsFalse(KeyboardTargetMapper.TryMap(virtualKey, out var target));
        Assert.AreEqual(PointF.Empty, target);
    }
}
