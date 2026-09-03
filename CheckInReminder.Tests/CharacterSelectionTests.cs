using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class CharacterSelectionTests
{
    [TestMethod]
    public void SingleCharacter_CannotBrowseButCanConfirm()
    {
        var characters = new[]
        {
            Character("white-bear", "白熊"),
        };
        var selection = new CharacterSelection(characters, "white-bear");

        Assert.IsFalse(selection.CanBrowse);
        Assert.IsFalse(selection.MoveNext());
        Assert.AreEqual("white-bear", selection.Confirm());
        Assert.AreEqual("white-bear", selection.ConfirmedCharacterId);
    }

    [TestMethod]
    public void MultipleCharacters_BrowseWrapsAndConfirmationIsExplicit()
    {
        var characters = new[]
        {
            Character("white-bear", "白熊"),
            Character("cat", "小猫"),
        };
        var selection = new CharacterSelection(characters, "white-bear");

        Assert.IsTrue(selection.CanBrowse);
        Assert.IsTrue(selection.MovePrevious());
        Assert.AreEqual("cat", selection.SelectedCharacter.Id);
        Assert.AreEqual("white-bear", selection.ConfirmedCharacterId);

        Assert.AreEqual("cat", selection.Confirm());
        Assert.AreEqual("cat", selection.ConfirmedCharacterId);

        Assert.IsTrue(selection.MoveNext());
        Assert.AreEqual("white-bear", selection.SelectedCharacter.Id);
    }

    [TestMethod]
    public void UnknownInitialId_FallsBackToFirstCharacter()
    {
        var selection = new CharacterSelection(
            new[] { Character("white-bear", "白熊") },
            "missing");

        Assert.AreEqual("white-bear", selection.SelectedCharacter.Id);
        Assert.AreEqual("white-bear", selection.ConfirmedCharacterId);
    }

    [TestMethod]
    public void SelectById_SelectsExistingCharacterAndRejectsUnknownOrCurrent()
    {
        var selection = new CharacterSelection(
            new[] { Character("white-bear", "白熊"), Character("cat", "小猫") },
            "white-bear");

        Assert.IsFalse(selection.SelectById("missing"));
        Assert.IsFalse(selection.SelectById("white-bear"), "已选中的角色不需要重复选中");

        Assert.IsTrue(selection.SelectById("cat"));
        Assert.AreEqual("cat", selection.SelectedCharacter.Id);
        Assert.AreEqual("white-bear", selection.ConfirmedCharacterId, "选中不等于确认");

        Assert.AreEqual("cat", selection.Confirm());
        Assert.AreEqual("cat", selection.ConfirmedCharacterId);
    }

    private static ReminderCharacter Character(string id, string name) =>
        new(id, name, "Edge", TimeSpan.FromSeconds(1), false);
}
