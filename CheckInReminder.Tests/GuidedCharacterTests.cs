using System.Drawing;
using System.Drawing.Imaging;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GuidedCharacterTests
{
    [TestMethod]
    public void TransparentPng_OpaqueWhitePngIsRejected_RealTransparencyAccepted()
    {
        var path = Path.Combine(Path.GetTempPath(), "guide-png-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var b = new Bitmap(80, 60)) { using (var g = Graphics.FromImage(b)) g.Clear(Color.White); b.Save(path, ImageFormat.Png); }
            Assert.Throws<InvalidDataException>(() => CharacterMediaProcessor.ValidateTransparentPng(path));
            using (var b = new Bitmap(80, 60)) { using (var g = Graphics.FromImage(b)) g.FillRectangle(Brushes.Red, 10, 10, 40, 40); b.Save(path, ImageFormat.Png); }
            CharacterMediaProcessor.ValidateTransparentPng(path);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void GuidedTemplates_LoadExistingCompletePoses_AndRespondToMouse()
    {
        var root = Path.Combine(Path.GetTempPath(), "guide-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var id in new[] { "yellow-hippo", "blue-hat-cat", "stick-dog", "scooter-dinosaur" })
            {
                Assert.IsTrue(DesktopPetSpriteSet.TryLoad(id, out var source));
                using (source)
                {
                    source.Idle.Save(Path.Combine(root, "idle.png"), ImageFormat.Png);
                    source.GetPressed(PetKeyboardPose.Left).Save(Path.Combine(root, "left.png"), ImageFormat.Png);
                    source.GetPressed(PetKeyboardPose.Center).Save(Path.Combine(root, "center.png"), ImageFormat.Png);
                    source.GetPressed(PetKeyboardPose.Right).Save(Path.Combine(root, "right.png"), ImageFormat.Png);
                }
                using var custom = new CustomPetRenderer(root, null, useTemplateMouse: true);
                using var rest = custom.Render(DesktopPetRigPose.Rest);
                using var moved = custom.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 1), MousePress = 1 });
                Assert.AreEqual(rest.GetPixel(350, 320), moved.GetPixel(350, 320), "Keyboard must remain stationary.");
                var differences = 0;
                for (var y = 220; y < 350; y += 2)
                for (var x = 110; x < 270; x += 2)
                    if (rest.GetPixel(x, y) != moved.GetPixel(x, y)) differences++;
                Assert.IsGreaterThan(20, differences, id);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [STATestMethod]
    public void Wizard_GuidedUploadsDoNotRequestMouseLayerOrBackgroundAlgorithm()
    {
        using var wizard = new CharacterCreationWizard();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var files = (Dictionary<string, System.Windows.Forms.TextBox>)typeof(CharacterCreationWizard).GetField("petFiles", flags)!.GetValue(wizard)!;
        CollectionAssert.AreEquivalent(new[] { "idle", "left", "center", "right" }, files.Keys.ToArray());
        var request = (CharacterCreationRequest)typeof(CharacterCreationWizard).GetMethod("Request", flags)!.Invoke(wizard, null)!;
        Assert.AreEqual(BackgroundRemoval.Auto, request.Background);
        Assert.AreEqual(BackgroundRemoval.Preserve, request.PetBackground);
        Assert.IsTrue(request.StrictPetPng);
    }

    [TestMethod]
    public void TemplateMouse_RejectsUnrelatedComposition_KeyboardOnlyStillWorks()
    {
        var root = Path.Combine(Path.GetTempPath(), "guide-reject-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var name in new[] { "idle", "left", "center", "right" })
            {
                using var b = new Bitmap(600, 448);
                using (var g = Graphics.FromImage(b)) g.FillEllipse(Brushes.Red, 250, 40, 100, 100);
                b.Save(Path.Combine(root, name + ".png"), ImageFormat.Png);
            }
            var error = Assert.Throws<InvalidDataException>(() => new CustomPetRenderer(root, null, true));
            StringAssert.StartsWith(error.Message, "桌面构图");
            using var renderer = new CustomPetRenderer(root, null);
            using var frame = renderer.Render(DesktopPetRigPose.Rest);
            Assert.AreEqual(Color.Red.ToArgb(), frame.GetPixel(300, 90).ToArgb());
        }
        finally { Directory.Delete(root, true); }
    }
}
