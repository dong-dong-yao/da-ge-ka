using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class ScheduleCalculatorTests
{
    private static readonly TimeOnly MorningStart = new(9, 30);
    private static readonly TimeOnly MorningEnd = new(10, 0);
    private static readonly TimeOnly EveningStart = new(19, 0);

    [TestMethod]
    public void MorningWindow_BeforeStart_IsFalse() =>
        Assert.IsFalse(ScheduleCalculator.IsInMorningWindow(At(9, 29), MorningStart, MorningEnd));

    [TestMethod]
    public void MorningWindow_AtStart_IsTrue() =>
        Assert.IsTrue(ScheduleCalculator.IsInMorningWindow(At(9, 30), MorningStart, MorningEnd));

    [TestMethod]
    public void MorningWindow_OneSecondBeforeEnd_IsTrue() =>
        Assert.IsTrue(ScheduleCalculator.IsInMorningWindow(At(9, 59, 59), MorningStart, MorningEnd));

    [TestMethod]
    public void MorningWindow_AtExclusiveEnd_IsFalse() =>
        Assert.IsFalse(ScheduleCalculator.IsInMorningWindow(At(10, 0), MorningStart, MorningEnd));

    [TestMethod]
    public void MorningSchedule_UsesFiveMinuteFixedNodes() =>
        Assert.AreEqual(At(9, 40), ScheduleCalculator.GetNextMorningDue(At(9, 35), MorningStart, MorningEnd, 5));

    [TestMethod]
    public void MorningSchedule_After0947_Returns0950() =>
        Assert.AreEqual(At(9, 50), ScheduleCalculator.GetNextMorningDue(At(9, 47), MorningStart, MorningEnd, 5));

    [TestMethod]
    public void MorningSchedule_AtStart_ReturnsStrictlyFutureNode() =>
        Assert.AreEqual(At(9, 35), ScheduleCalculator.GetNextMorningDue(At(9, 30), MorningStart, MorningEnd, 5));

    [TestMethod]
    public void MorningSchedule_SevenMinuteIntervalKeepsOriginalAnchor() =>
        Assert.AreEqual(At(9, 51), ScheduleCalculator.GetNextMorningDue(At(9, 47), MorningStart, MorningEnd, 7));

    [TestMethod]
    public void MorningSchedule_After0959_HasNoNextDue() =>
        Assert.IsNull(ScheduleCalculator.GetNextMorningDue(At(9, 59), MorningStart, MorningEnd, 5));

    [TestMethod]
    public void EveningSchedule_After1903_Returns1910() =>
        Assert.AreEqual(At(19, 10), ScheduleCalculator.GetNextEveningDue(At(19, 3), EveningStart, 10));

    [TestMethod]
    public void EveningSchedule_After1913_Returns1920() =>
        Assert.AreEqual(At(19, 20), ScheduleCalculator.GetNextEveningDue(At(19, 13), EveningStart, 10));

    [TestMethod]
    public void OneMinuteInterval_ReturnsNextMinute() =>
        Assert.AreEqual(At(9, 31), ScheduleCalculator.GetNextMorningDue(At(9, 30), MorningStart, MorningEnd, 1));

    [TestMethod]
    public void InvalidIntervals_AreRejectedBySettingsValidation()
    {
        var settings = AppSettings.CreateDefault();
        settings.MorningIntervalMinutes = 0;
        settings.EveningIntervalMinutes = 1441;

        Assert.IsFalse(SettingsService.TryValidate(settings, out _));
    }

    private static DateTime At(int hour, int minute, int second = 0) =>
        new(2026, 8, 28, hour, minute, second, DateTimeKind.Local);
}
