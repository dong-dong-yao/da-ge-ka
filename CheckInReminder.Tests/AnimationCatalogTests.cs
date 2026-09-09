using System.Text.RegularExpressions;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class AnimationCatalogTests
{
    [TestMethod]
    public void BuiltInCharacters_ExposeExpectedReminderEdgesAndDesktopPetAssets()
    {
        var expected = new Dictionary<string, ScreenEdge[]>
        {
            ["white-bear"] = [ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Right, ScreenEdge.Bottom],
            ["yellow-hippo"] = [ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Right, ScreenEdge.Bottom],
            ["blue-hat-cat"] = [ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Right, ScreenEdge.Bottom],
            ["stick-dog"] = [ScreenEdge.Left, ScreenEdge.Right],
            ["scooter-dinosaur"] = [ScreenEdge.Left, ScreenEdge.Right],
        };

        CollectionAssert.AreEqual(expected.Keys.ToArray(), AnimationCatalog.Characters.Select(x => x.Id).ToArray());
        foreach (var character in AnimationCatalog.Characters)
        {
            CollectionAssert.AreEqual(expected[character.Id], character.AllowedEdges.ToArray(), character.Id);
            Assert.IsTrue(character.Id == AnimationCatalog.DefaultCharacterId || character.HasPetAssets,
                $"{character.Id} 应有可用桌宠素材或使用白熊专用渲染器");
        }
    }

    [TestMethod]
    public void ReminderEdgeSelector_NeverReturnsVerticalEdgeForSideOnlyCharacters()
    {
        foreach (var id in new[] { "stick-dog", "scooter-dinosaur" })
        {
            var character = AnimationCatalog.FindCharacter(id)!;
            var random = new Random(20260909);

            var actual = Enumerable.Range(0, 100)
                .Select(_ => ReminderEdgeSelector.Select(character, random))
                .Distinct()
                .OrderBy(edge => edge)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { ScreenEdge.Left, ScreenEdge.Right },
                actual,
                id);
        }
    }

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
            var resourceSequenceName = character.SequenceName.Replace('-', '_');
            var frameCount = resources.Count(name => name.Contains(
                $".Animations.{resourceSequenceName}.frame_",
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
                AnimationCatalog.HasDesktopPetResources(character.Id)
                    || AnimationCatalog.HasSequenceResources(character.PetIdleSequenceName),
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
