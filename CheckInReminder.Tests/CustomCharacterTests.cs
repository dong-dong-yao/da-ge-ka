using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CustomCharacterTests
{
    private string root = null!;
    [TestInitialize] public void Setup() => root = Path.Combine(Path.GetTempPath(), "dagaka-test-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private string MakePackage(string id = "user-test")
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(Path.Combine(folder, "notify"));
        using var frame = new Bitmap(32, 24);
        using (var g = Graphics.FromImage(frame)) g.Clear(Color.Red);
        frame.Save(Path.Combine(folder, "notify", "frame_0000.png"), ImageFormat.Png);
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(new CustomCharacterManifest
        {
            Id = id, Name = "我的/角色", DurationSeconds = 3, FrameCount = 1,
            AllowedEdges = [ScreenEdge.Right], SourceEdge = ScreenEdge.Right
        }));
        return folder;
    }

    [TestMethod]
    public void Store_ReloadsIndependentOfDisplayName_AndLoadsRealFrames()
    {
        MakePackage();
        var store = new CustomCharacterStore(root);
        var character = store.Load().Single();
        Assert.AreEqual("我的/角色", character.DisplayName);
        Assert.AreEqual("user-test", character.Id);
        Assert.IsFalse(character.HasPetAssets);
        using var sequence = AnimationSequence.Load(character.SequenceName, character.Duration, false);
        Assert.AreEqual(Color.Red.ToArgb(), sequence.Frames[0].GetPixel(10, 10).ToArgb());
    }

    [TestMethod]
    public void Store_SkipsBrokenPackageWithoutLosingGoodOne()
    {
        MakePackage();
        var bad = Path.Combine(root, "user-bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "manifest.json"), "{broken");
        var store = new CustomCharacterStore(root);
        Assert.HasCount(1, store.Load());
        Assert.HasCount(1, store.Errors);
    }

    [TestMethod]
    public void Store_SkipsUnsupportedManifestVersion()
    {
        var folder = MakePackage();
        var file = Path.Combine(folder, "manifest.json");
        var manifest = JsonSerializer.Deserialize<CustomCharacterManifest>(File.ReadAllText(file))!;
        manifest.Version = 999;
        File.WriteAllText(file, JsonSerializer.Serialize(manifest));
        var store = new CustomCharacterStore(root);
        Assert.IsEmpty(store.Load());
        Assert.HasCount(1, store.Errors);
    }

    [TestMethod]
    public void Store_RejectsTraversalId_AndCannotDeleteOutsideRoot()
    {
        var store = new CustomCharacterStore(root);
        Assert.Throws<InvalidDataException>(() => store.Delete("../outside"));
    }

    [TestMethod]
    public void Store_RejectsMissingFramesAndInvalidMotionCoordinates()
    {
        var folder = MakePackage();
        File.Delete(Path.Combine(folder, "notify", "frame_0000.png"));
        Assert.IsEmpty(new CustomCharacterStore(root).Load());
        Assert.Throws<InvalidDataException>(() => new CustomMouseRig { ShoulderX = float.NaN }.Validate());
    }

    [TestMethod]
    public void MissingCharacter_OnlyResetsCharacter_NotReminderSettings()
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, """{"MorningStart":"08:10","MorningEnd":"08:40","MorningIntervalMinutes":7,"AutoStart":false,"CharacterId":"user-deleted"}""");
        var settings = new SettingsService(file).Load();
        Assert.AreEqual(new TimeOnly(8, 10), settings.MorningStart);
        Assert.AreEqual(7, settings.MorningIntervalMinutes);
        Assert.IsFalse(settings.AutoStart);
        Assert.AreEqual(AnimationCatalog.DefaultCharacterId, settings.CharacterId);
    }

    [STATestMethod]
    public void Wizard_EmptyNameShowsValidationMessageInsteadOfThrowing()
    {
        using var form = new CharacterCreationWizard();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var task = (Task)typeof(CharacterCreationWizard).GetMethod("NextAsync", flags)!.Invoke(form, null)!;
        task.GetAwaiter().GetResult();
        var label = (System.Windows.Forms.Label)typeof(CharacterCreationWizard).GetField("status", flags)!.GetValue(form)!;
        StringAssert.Contains(label.Text, "角色名称");
    }

    [STATestMethod]
    public void CustomPreview_PausedGalleryKeepsOnlyPoster_ThenLoadsOnHover()
    {
        MakePackage();
        var character = new CustomCharacterStore(root).Load().Single();
        using var box = new System.Windows.Forms.PictureBox();
        using var player = new AnimationPreviewPlayer(box);
        player.SetPaused(true); player.Load(character);
        var field = typeof(AnimationPreviewPlayer).GetField("sequence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.IsNotNull(box.Image);
        Assert.IsNull(field.GetValue(player), "Paused custom cards must not retain every animation frame.");
        player.SetPaused(false);
        Assert.IsNotNull(field.GetValue(player));
        player.SetPaused(true); player.ShowPosterFrame();
        Assert.IsNull(field.GetValue(player));
        Assert.IsNotNull(box.Image);
    }

    [TestMethod]
    public void PetRenderer_MouseMovesWithoutChangingBackground_KeyboardSelectsPose()
    {
        Directory.CreateDirectory(root);
        void Save(string name, Color color)
        {
            using var bitmap = new Bitmap(120, 90);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(color);
            bitmap.Save(Path.Combine(root, name + ".png"), ImageFormat.Png);
        }
        Save("idle", Color.Blue); Save("left", Color.Red); Save("right", Color.Yellow);
        using (var arm = new Bitmap(120, 90))
        {
            using var g = Graphics.FromImage(arm);
            g.FillRectangle(Brushes.White, 50, 40, 30, 20);
            arm.Save(Path.Combine(root, "mouse.png"), ImageFormat.Png);
        }
        using var renderer = new CustomPetRenderer(root, new CustomMouseRig { ShoulderX = .42f, ShoulderY = .45f, MouseX = .65f, MouseY = .65f });
        using var resting = renderer.Render(DesktopPetRigPose.Rest);
        using var moved = renderer.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 1), MousePress = 1 });
        using var pressed = renderer.Render(DesktopPetRigPose.Rest with { KeyboardContact = true, KeyboardTarget = new PointF(0, 0) });
        Assert.AreEqual(resting.GetPixel(5, 5), moved.GetPixel(5, 5));
        Assert.AreEqual(Color.Red.ToArgb(), pressed.GetPixel(5, 5).ToArgb());
        Assert.IsTrue(Enumerable.Range(40, 30).Any(y => resting.GetPixel(78, y) != moved.GetPixel(78, y)));
        for (var y = 45; y < 55; y++)
        for (var x = 55; x < 75; x++)
            Assert.AreEqual(Color.White.ToArgb(), moved.GetPixel(x, y).ToArgb(), $"Moving arm has a crack at {x},{y}");
    }
}
