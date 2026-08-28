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
    public void BannerLayout_PlacesButtonBelowSquareImage()
    {
        Assert.AreEqual(400, UiTheme.BannerWidth);
        Assert.AreEqual(400, UiTheme.BannerImageHeight);
        Assert.IsGreaterThan(UiTheme.BannerImageHeight, UiTheme.BannerHeight);
        Assert.IsGreaterThanOrEqualTo(UiTheme.BannerImageHeight, UiTheme.BannerButtonTop);
    }

    [TestMethod]
    public void Theme_HasHighContrastButtonColors()
    {
        Assert.AreNotEqual(UiTheme.PrimaryButtonBackColor.ToArgb(), UiTheme.PrimaryButtonForeColor.ToArgb());
        Assert.AreNotEqual(UiTheme.SecondaryButtonBackColor.ToArgb(), UiTheme.SecondaryButtonForeColor.ToArgb());
    }
}
