using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class SettingsServiceTests
{
    [TestMethod]
    public void Defaults_DisableBreakReminderAndUseWorkingDayWindow()
    {
        var settings = AppSettings.CreateDefault();

        Assert.IsFalse(settings.BreakReminderEnabled);
        Assert.AreEqual(new TimeOnly(9, 0), settings.BreakStart);
        Assert.AreEqual(new TimeOnly(18, 0), settings.BreakEnd);
        Assert.AreEqual(60, settings.BreakIntervalMinutes);
        Assert.AreEqual("white-bear", settings.CharacterId);
        Assert.IsFalse(settings.DesktopPetEnabled, "桌面宠物默认关闭。");
    }

    [TestMethod]
    public void Clone_PreservesDesktopPetEnabled()
    {
        var settings = AppSettings.CreateDefault();
        settings.DesktopPetEnabled = true;

        var clone = settings.Clone();

        Assert.IsTrue(clone.DesktopPetEnabled);
        clone.DesktopPetEnabled = false;
        Assert.IsTrue(settings.DesktopPetEnabled, "Clone 必须是独立副本。");
    }

    [TestMethod]
    public void SaveAndLoad_RoundTripsDesktopPetEnabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dagaka-tests-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "config.json");
        Directory.CreateDirectory(directory);
        try
        {
            var service = new SettingsService(path);
            var settings = AppSettings.CreateDefault();
            settings.DesktopPetEnabled = true;
            service.Save(settings);

            var loaded = service.Load();

            Assert.IsTrue(loaded.DesktopPetEnabled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Clone_PreservesSelectedCharacter()
    {
        var settings = AppSettings.CreateDefault();
        settings.CharacterId = "white-bear";

        var clone = settings.Clone();

        Assert.AreEqual("white-bear", clone.CharacterId);
    }

    [TestMethod]
    public void Load_OldSixFieldConfig_PreservesExistingValuesAndAddsBreakDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dagaka-tests-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "config.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, """
                {
                  "MorningStart": "08:45",
                  "MorningEnd": "09:15",
                  "MorningIntervalMinutes": 10,
                  "EveningStart": "18:30",
                  "EveningIntervalMinutes": 15,
                  "AutoStart": false
                }
                """);

            var settings = new SettingsService(path).Load();

            Assert.AreEqual(new TimeOnly(8, 45), settings.MorningStart);
            Assert.AreEqual(15, settings.EveningIntervalMinutes);
            Assert.IsFalse(settings.AutoStart);
            Assert.IsFalse(settings.BreakReminderEnabled);
            Assert.AreEqual(new TimeOnly(9, 0), settings.BreakStart);
            Assert.AreEqual(new TimeOnly(18, 0), settings.BreakEnd);
            Assert.AreEqual(60, settings.BreakIntervalMinutes);
            Assert.AreEqual("white-bear", settings.CharacterId);
            Assert.IsFalse(settings.DesktopPetEnabled, "旧配置缺少该字段时应回退为默认关闭。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Validation_RejectsUnknownCharacter()
    {
        var settings = AppSettings.CreateDefault();
        settings.CharacterId = "missing-character";

        var valid = SettingsService.TryValidate(settings, out var message);

        Assert.IsFalse(valid);
        StringAssert.Contains(message, "角色");
    }
}
