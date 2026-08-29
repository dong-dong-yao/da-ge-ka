using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class UiThemeTests
{
    [TestMethod]
    public void Branding_UsesApprovedProductName()
    {
        Assert.AreEqual("打个卡", UiTheme.ProductName);
        Assert.AreEqual("打个卡设置", UiTheme.SettingsTitle);
    }

    [TestMethod]
    public void ReminderCopy_UsesApprovedText()
    {
        Assert.AreEqual("你打卡了吗？", ReminderCopy.CheckInQuestion);
        Assert.AreEqual("已打卡，闭嘴", ReminderCopy.CheckInButton);
        Assert.AreEqual("坐太久啦，该站起来活动一下了", ReminderCopy.BreakMessage);
        Assert.AreEqual("真的吗，那你为什么不走", ReminderCopy.GateQuestion);
        Assert.AreEqual("真的", ReminderCopy.GateTrue);
        Assert.AreEqual("假的", ReminderCopy.GateFalse);
    }

    [TestMethod]
    public void AnimationCatalog_PreservesSourceDurationsAndLoopModes()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(7.104), AnimationCatalog.EdgeDuration);
        Assert.AreEqual(TimeSpan.FromSeconds(4.086009), AnimationCatalog.GateDuration);
        Assert.IsFalse(AnimationCatalog.EdgeLoops);
        Assert.IsTrue(AnimationCatalog.GateLoops);
    }

    [TestMethod]
    public void PublishedAssembly_ContainsBothCompleteAnimationSequences()
    {
        var resources = typeof(AnimationCatalog).Assembly.GetManifestResourceNames();

        Assert.AreEqual(
            86,
            resources.Count(name => name.Contains(".Animations.Edge.frame_", StringComparison.Ordinal)));
        Assert.AreEqual(
            50,
            resources.Count(name => name.Contains(".Animations.Gate.frame_", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Theme_HasHighContrastButtonColors()
    {
        Assert.AreNotEqual(UiTheme.PrimaryButtonBackColor.ToArgb(), UiTheme.PrimaryButtonForeColor.ToArgb());
        Assert.AreNotEqual(UiTheme.SecondaryButtonBackColor.ToArgb(), UiTheme.SecondaryButtonForeColor.ToArgb());
    }
}
