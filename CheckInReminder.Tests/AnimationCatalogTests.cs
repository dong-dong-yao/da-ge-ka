using System.Text.RegularExpressions;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class AnimationCatalogTests
{
    [TestMethod]
    public void CharacterIds_AreUniqueAndKebabCase()
    {
        var ids = AnimationCatalog.Characters.Select(character => character.Id).ToArray();

        Assert.AreEqual(ids.Length, ids.Distinct(StringComparer.Ordinal).Count(), "角色 Id 必须唯一");
        foreach (var id in ids)
        {
            Assert.IsTrue(
                Regex.IsMatch(id, "^[a-z0-9-]+$"),
                $"角色 Id 必须是 kebab-case（小写字母/数字/连字符）：{id}");
        }
    }

    [TestMethod]
    public void EveryCharacter_HasEmbeddedReminderFrames()
    {
        var resources = typeof(AnimationCatalog).Assembly.GetManifestResourceNames();

        foreach (var character in AnimationCatalog.Characters)
        {
            var frameCount = resources.Count(name => name.Contains(
                $".Animations.{character.SequenceName}.frame_",
                StringComparison.Ordinal));
            Assert.IsGreaterThan(
                0,
                frameCount,
                $"角色 {character.Id} 的提醒动画序列 {character.SequenceName} 缺少嵌入帧资源");
        }
    }

    [TestMethod]
    public void PetAssetsFlag_MatchesEmbeddedResources()
    {
        foreach (var character in AnimationCatalog.Characters)
        {
            Assert.AreEqual(
                AnimationCatalog.HasSequenceResources(character.PetIdleSequenceName),
                character.HasPetAssets,
                $"角色 {character.Id} 的 HasPetAssets 与实际嵌入资源不一致");
        }
    }

    [TestMethod]
    public void PetSequenceNames_DeriveFromCharacterId()
    {
        var character = AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId);

        Assert.IsNotNull(character);
        Assert.AreEqual("white-bear-pet-idle", character.PetIdleSequenceName);
        Assert.AreEqual("white-bear-pet-tap", character.PetTapSequenceName);
    }
}
