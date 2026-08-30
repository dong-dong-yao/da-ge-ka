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

    private static ReminderCharacter Character(string id, string name) =>
        new(id, name, "Edge", TimeSpan.FromSeconds(1), false);
}
