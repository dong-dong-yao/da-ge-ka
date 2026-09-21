using System.Drawing;
using System.Drawing.Imaging;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CharacterMediaTests
{
    private static void RequireMediaTools()
    {
        if (!typeof(AnimationCatalog).Assembly.GetManifestResourceNames().Contains("CheckInReminder.MediaTools.zip"))
            Assert.Inconclusive("本测试需要先运行 tools/pack-media-tools.ps1；普通源码构建不包含视频工具。");
    }
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "dagaka-media-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestMethod]
    public void KeyAndCrop_UsesWholeSequenceBounds_PreservesMotion()
    {
        for (var i = 0; i < 2; i++)
        {
            using var image = new Bitmap(100, 80);
            using (var g = Graphics.FromImage(image)) { g.Clear(Color.Lime); g.FillRectangle(Brushes.Red, 20 + i * 20, 20, 20, 20); }
            image.Save(Path.Combine(root, $"frame_{i:0000}.png"), ImageFormat.Png);
        }
        CharacterMediaProcessor.ProcessFrames(root, BackgroundRemoval.Green, CancellationToken.None);
        using var first = new Bitmap(Path.Combine(root, "frame_0000.png"));
        using var second = new Bitmap(Path.Combine(root, "frame_0001.png"));
        Assert.AreEqual(first.Size, second.Size);
        Assert.IsLessThan(100, first.Width);
        Assert.AreEqual(0, first.GetPixel(0, 0).A);
        Assert.AreEqual(255, first.GetPixel(10, 10).A);
        Assert.AreEqual(0, second.GetPixel(10, 10).A);
    }

    [TestMethod]
    public void PreserveBackground_DoesNotEraseGreenCharacter()
    {
        using (var image = new Bitmap(50, 50))
        {
            using var g = Graphics.FromImage(image); g.FillRectangle(Brushes.Lime, 10, 10, 20, 20);
            image.Save(Path.Combine(root, "frame_0000.png"), ImageFormat.Png);
        }
        CharacterMediaProcessor.ProcessFrames(root, BackgroundRemoval.Preserve, CancellationToken.None);
        using var result = new Bitmap(Path.Combine(root, "frame_0000.png"));
        Assert.AreEqual(Color.Lime.ToArgb(), result.GetPixel(10, 10).ToArgb());
    }

    [TestMethod]
    public async Task Creation_Cancelled_DoesNotPublishPackage()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new CharacterCreationService(new CustomCharacterStore(root)).PrepareAsync(
            new CharacterCreationRequest { Name = "测试", VideoPath = "missing.mp4" }, null, cancelled.Token));
        Assert.IsEmpty(Directory.GetDirectories(root));
    }

    [TestMethod]
    [TestCategory("MediaIntegration")]
    public async Task VideoImport_Offline_ProcessesCommitsReloadsAndRollsBack()
    {
        RequireMediaTools();
        var png = Path.Combine(root, "source.png");
        using (var image = new Bitmap(120, 90))
        {
            using var g = Graphics.FromImage(image); g.Clear(Color.Lime); g.FillEllipse(Brushes.Red, 30, 20, 50, 50);
            image.Save(png, ImageFormat.Png);
        }
        var video = Path.Combine(root, "source.mp4");
        await BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", "-v", "error", "-loop", "1", "-i", png, "-t", "3", "-r", "12", "-pix_fmt", "yuv420p", video], CancellationToken.None);
        var store = new CustomCharacterStore(Path.Combine(root, "characters"));
        var request = new CharacterCreationRequest { Name = "本地/角色", VideoPath = video, Background = BackgroundRemoval.Green };
        var service = new CharacterCreationService(store);
        using (var discarded = await service.PrepareAsync(request, null, CancellationToken.None)) Assert.IsEmpty(store.Load());
        Assert.IsEmpty(Directory.GetDirectories(store.Root));
        using var prepared = await service.PrepareAsync(request, null, CancellationToken.None);
        var character = prepared.Commit();
        File.Delete(video); File.Delete(png);
        var reloaded = store.Load().Single();
        Assert.AreEqual(character.Id, reloaded.Id);
        using var frames = AnimationSequence.Load(reloaded.SequenceName, reloaded.Duration, false);
        Assert.HasCount(36, frames.Frames);
        Assert.AreEqual(0, frames.Frames[0].GetPixel(0, 0).A);
        Assert.IsGreaterThan(200, (int)frames.Frames[0].GetPixel(frames.Frames[0].Width / 2, frames.Frames[0].Height / 2).R);
    }

    [TestMethod]
    [TestCategory("MediaIntegration")]
    public async Task DesktopImages_RejectDifferentCanvasesBeforePublishing()
    {
        RequireMediaTools();
        foreach (var (name, width) in new[] { ("idle", 100), ("left", 200), ("right", 100) })
        {
            using var image = new Bitmap(width, 80);
            image.Save(Path.Combine(root, name + ".png"), ImageFormat.Png);
        }
        var request = new CharacterCreationRequest { Name = "测试", VideoPath = Path.Combine(root, "missing.mp4"),
            PetImages = new[] { "idle", "left", "right" }.ToDictionary(k => k, k => Path.Combine(root, k + ".png")) };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new CharacterCreationService(new CustomCharacterStore(Path.Combine(root, "characters"))).PrepareAsync(request, null, CancellationToken.None));
        StringAssert.Contains(error.Message, "画布");
    }

    [TestMethod]
    [TestCategory("MediaIntegration")]
    public async Task TransparentWebm_KeepsTransparencyAndGreenArtwork()
    {
        RequireMediaTools();
        var png = Path.Combine(root, "transparent.png");
        using (var image = new Bitmap(120, 90))
        {
            using (var g = Graphics.FromImage(image)) g.FillRectangle(Brushes.Lime, 30, 20, 50, 50);
            image.Save(png, ImageFormat.Png);
        }
        var video = Path.Combine(root, "alpha.webm");
        await BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", "-v", "error", "-loop", "1", "-i", png, "-t", "3", "-r", "12", "-c:v", "libvpx-vp9", "-pix_fmt", "yuva420p", "-auto-alt-ref", "0", video], CancellationToken.None);
        var target = Path.Combine(root, "decoded");
        await CharacterMediaProcessor.VideoAsync(video, target, BackgroundRemoval.Auto, CancellationToken.None);
        using var frame = new Bitmap(Path.Combine(target, "frame_0000.png"));
        Assert.AreEqual(0, frame.GetPixel(0, 0).A);
        var center = frame.GetPixel(frame.Width / 2, frame.Height / 2);
        Assert.IsGreaterThan(240, (int)center.A);
        Assert.IsGreaterThan(200, (int)center.G);
    }

    [TestMethod]
    [TestCategory("MediaIntegration")]
    public async Task GuidedImport_RawPosesPersistMouse_AndMismatchCanSaveKeyboardOnly()
    {
        RequireMediaTools();
        // Prepare transparent fixture files from raw embedded artwork, before mouse-layer extraction.
        var assembly = typeof(AnimationCatalog).Assembly;
        var images = new Dictionary<string, string>();
        foreach (var (key, stem) in new[] { ("idle", "idle"), ("left", "press-left"), ("center", "press-center"), ("right", "press-right") })
        {
            var resource = assembly.GetManifestResourceNames().Single(n => n.Contains(".yellow_hippo.") && n.Contains("." + stem + "."));
            var raw = Path.Combine(root, stem + Path.GetExtension(resource));
            using (var input = assembly.GetManifestResourceStream(resource)!)
            using (var output = File.Create(raw)) input.CopyTo(output);
            using var transparent = await CharacterMediaProcessor.ImageAsync(raw, BackgroundRemoval.White, CancellationToken.None);
            var path = Path.Combine(root, "upload-" + key + ".png");
            transparent.Save(path, ImageFormat.Png); images[key] = path;
        }
        var video = Path.Combine(root, "guide.mp4");
        await BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", "-v", "error", "-f", "lavfi", "-i", "color=c=green:s=120x90:r=12,drawbox=x=40:y=30:w=30:h=30:color=red:t=fill", "-t", "3", video], CancellationToken.None);
        var store = new CustomCharacterStore(Path.Combine(root, "guided-store"));
        var request = new CharacterCreationRequest { Name = "图文引导测试", VideoPath = video, PetImages = images, StrictPetPng = true, UseTemplateMouse = true };
        var service = new CharacterCreationService(store);
        using (var prepared = await service.PrepareAsync(request, null, CancellationToken.None)) prepared.Commit();
        var reloaded = store.Load().Single();
        Assert.IsTrue(reloaded.CustomManifest!.UsesTemplateMouse);
        using (var renderer = new CustomPetRenderer(Path.Combine(reloaded.CustomPackagePath!, "pet"), null, reloaded.CustomManifest.UsesTemplateMouse))
        {
            using var rest = renderer.Render(DesktopPetRigPose.Rest);
            using var mouse = renderer.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 1) });
            using var key = renderer.Render(DesktopPetRigPose.Rest with { KeyboardContact = true, KeyboardTarget = new PointF(.1f, .5f) });
            Assert.IsTrue(Different(rest, mouse)); Assert.IsTrue(Different(rest, key));
        }
        store.Delete(reloaded.Id);
        foreach (var (key, path) in images)
        {
            using var b = new Bitmap(600, 448);
            using (var g = Graphics.FromImage(b)) g.FillEllipse(key == "idle" ? Brushes.Red : Brushes.Blue, 280, 80, 80, 80);
            b.Save(path, ImageFormat.Png);
        }
        var mismatch = await Assert.ThrowsAsync<InvalidDataException>(() => service.PrepareAsync(request, null, CancellationToken.None));
        StringAssert.StartsWith(mismatch.Message, "桌面构图");
        Assert.IsEmpty(Directory.GetDirectories(store.Root));
        request.UseTemplateMouse = false;
        using (var prepared = await service.PrepareAsync(request, null, CancellationToken.None)) prepared.Commit();
        reloaded = store.Load().Single();
        Assert.IsFalse(reloaded.CustomManifest!.UsesTemplateMouse);
        using (var renderer = new CustomPetRenderer(Path.Combine(reloaded.CustomPackagePath!, "pet"), null, reloaded.CustomManifest.UsesTemplateMouse))
        {
            using var rest = renderer.Render(DesktopPetRigPose.Rest);
            using var key = renderer.Render(DesktopPetRigPose.Rest with { KeyboardContact = true, KeyboardTarget = new PointF(.1f, .5f) });
            Assert.IsTrue(Different(rest, key));
        }
        request.PetImages.Remove("center");
        var missing = await Assert.ThrowsAsync<InvalidDataException>(() => service.PrepareAsync(request, null, CancellationToken.None));
        StringAssert.Contains(missing.Message, "四张");
    }

    private static bool Different(Bitmap a, Bitmap b)
    {
        for (var y = 0; y < a.Height; y += 4)
        for (var x = 0; x < a.Width; x += 4)
            if (a.GetPixel(x, y) != b.GetPixel(x, y)) return true;
        return false;
    }
}
