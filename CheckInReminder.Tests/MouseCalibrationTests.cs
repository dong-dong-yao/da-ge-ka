using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass, DoNotParallelize]
public sealed class MouseCalibrationTests
{
    [TestMethod]
    public void GrayArm_CanKeepExplicitSelectionInsteadOfRemovingItAsPad()
    {
        using var source = new Bitmap(600, 448); using var mask = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(source))
        {
            g.FillRectangle(Brushes.DarkGray, 40, 200, 220, 200);
            using var gray = new SolidBrush(Color.FromArgb(150, 150, 150)); g.FillRectangle(gray, 120, 200, 40, 130);
        }
        using (var g = Graphics.FromImage(mask)) g.FillRectangle(Brushes.White, 120, 200, 40, 130);
        var spec = new MouseCalibration { Rig = new() { ShoulderX = 140f / 600, ShoulderY = 200f / 448, MouseX = 140f / 600, MouseY = 320f / 448 }, Pad = [new(40, 200), new(260, 200), new(260, 400), new(40, 400)] };
        Assert.Throws<InvalidDataException>(() => CalibratedMouseLayers.Create(source, mask, spec));
        spec.RemoveConnectedPadColor = false;
        using var layers = CalibratedMouseLayers.Create(source, mask, spec);
        using var frame = layers.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 0) });
        Assert.AreEqual(Color.FromArgb(150, 150, 150).ToArgb(), frame.GetPixel(162, 300).ToArgb());
    }
    [TestMethod]
    public void Calibration_PersistsAllPoses_Reopens_AndFailedEditRollsBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "calibration-" + Guid.NewGuid().ToString("N"));
        var stage = Path.Combine(root, ".creating-test"); var pet = Path.Combine(stage, "pet");
        Directory.CreateDirectory(pet); Directory.CreateDirectory(Path.Combine(stage, "notify"));
        var edits = new Dictionary<string, MouseCalibrationEdit>();
        try
        {
            using (var frame = new Bitmap(20, 20)) frame.Save(Path.Combine(stage, "notify", "frame_0000.png"), ImageFormat.Png);
            File.WriteAllText(Path.Combine(stage, "manifest.json"), JsonSerializer.Serialize(new CustomCharacterManifest { Id = "user-calibration", Name = "测试", HasPet = true, DurationSeconds = 3, FrameCount = 1 }));
            var keys = new[] { "idle", "left", "center", "right" };
            for (var j = 0; j < keys.Length; j++)
            {
                using var source = new Bitmap(600, 448);
                using (var g = Graphics.FromImage(source))
                {
                    g.FillRectangle(Brushes.DarkGray, 40, 200, 220, 200);
                    using var marker = new SolidBrush(Color.FromArgb(255, 60 + j * 40, 20, 120));
                    g.FillRectangle(marker, 350, 300, 80, 60);
                    g.FillRectangle(Brushes.Orange, 120, 200, 40, 125);
                    g.FillEllipse(Brushes.White, 110, 300, 60, 60);
                }
                source.Save(Path.Combine(pet, keys[j] + ".png"), ImageFormat.Png);
                var mask = new Bitmap(600, 448); using (var g = Graphics.FromImage(mask)) g.FillRectangle(Brushes.White, 105, 195, 70, 170);
                edits[keys[j]] = new(new MouseCalibration { Rig = new() { ShoulderX = 140f / 600, ShoulderY = 200f / 448, MouseX = 140f / 600, MouseY = 330f / 448 }, Pad = [new(40, 200), new(260, 200), new(260, 400), new(40, 400)] }, mask);
            }
            var store = new CustomCharacterStore(root);
            using (var prepared = new PreparedCharacter(stage, store))
            {
                // Simulate a backup that cannot be fully removed after commit.
                File.SetAttributes(Path.Combine(pet, "idle.png"), FileAttributes.ReadOnly);
                prepared.ApplyMouseCalibration(edits);
                foreach (var backupFile in Directory.EnumerateFiles(stage, "*.png", SearchOption.AllDirectories)) File.SetAttributes(backupFile, FileAttributes.Normal);
                Assert.IsNotNull(prepared.Character.CustomManifest!.MouseCalibrations);
                var manifest = File.ReadAllText(Path.Combine(stage, "manifest.json"));
                var poseHash = File.ReadAllBytes(Path.Combine(pet, "idle.png"));
                edits["right"].Spec.Pad = [];
                Assert.Throws<InvalidDataException>(() => prepared.ApplyMouseCalibration(edits));
                Assert.AreEqual(manifest, File.ReadAllText(Path.Combine(stage, "manifest.json")));
                CollectionAssert.AreEqual(poseHash, File.ReadAllBytes(Path.Combine(pet, "idle.png")));
                Assert.IsEmpty(Directory.GetDirectories(stage, "pet-calibrating-*"));
                prepared.Commit();
            }
            var character = store.Load().Single(); Assert.IsNotNull(character.CustomManifest!.MouseCalibrations);
            using var renderer = new CustomPetRenderer(Path.Combine(character.CustomPackagePath!, "pet"), null, false, character.CustomManifest.MouseCalibrations);
            foreach (var (contact, target, index) in new[] { (false, .5f, 0), (true, .1f, 1), (true, .5f, 2), (true, .9f, 3) })
            {
                using var image = renderer.Render(DesktopPetRigPose.Rest with { KeyboardContact = contact, KeyboardTarget = new PointF(target, .5f), MouseOffset = new PointF(1, 1) });
                Assert.AreEqual(Color.FromArgb(255, 60 + index * 40, 20, 120).ToArgb(), image.GetPixel(370, 320).ToArgb());
                Assert.AreEqual(Color.DarkGray.ToArgb(), image.GetPixel(111, 325).ToArgb());
            }
            File.Delete(Path.Combine(character.CustomPackagePath!, "pet", "arm-center.png"));
            Assert.IsEmpty(store.Load()); Assert.HasCount(1, store.Errors);
        }
        finally
        {
            foreach (var edit in edits.Values) edit.Dispose();
            if (Directory.Exists(root)) { foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal); Directory.Delete(root, true); }
        }
    }
    [TestMethod]
    public void CalibratedLayers_MovingMouseRevealsPad_AndLeavesKeyboardFixed()
    {
        using var source = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(source))
        {
            g.FillRectangle(Brushes.DarkGray, 40, 200, 220, 200);
            g.FillRectangle(Brushes.Blue, 320, 280, 200, 120);
            g.FillRectangle(Brushes.Orange, 120, 200, 40, 125);
            g.FillEllipse(Brushes.Black, 110, 300, 60, 60);
            g.FillEllipse(Brushes.White, 114, 304, 52, 52);
        }
        using var mask = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(mask)) g.FillRectangle(Brushes.White, 105, 195, 70, 170);
        var spec = new MouseCalibration
        {
            Rig = new() { ShoulderX = 140f / 600, ShoulderY = 200f / 448, MouseX = 140f / 600, MouseY = 330f / 448 },
            Pad = [new(40, 200), new(260, 200), new(260, 400), new(40, 400)]
        };
        using var layers = CalibratedMouseLayers.Create(source, mask, spec);
        using var rest = layers.Render(DesktopPetRigPose.Rest);
        using var moved = layers.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 0) });
        Assert.AreEqual(source.GetPixel(120, 320), rest.GetPixel(120, 320));
        Assert.AreEqual(Color.DarkGray.ToArgb(), moved.GetPixel(111, 325).ToArgb(), "Old mouse location must reveal pad.");
        Assert.AreEqual(Color.Blue.ToArgb(), moved.GetPixel(350, 320).ToArgb());
        Assert.AreEqual(rest.GetPixel(140, 200), moved.GetPixel(140, 200), "Shoulder must stay pinned.");
        Assert.AreNotEqual(rest.GetPixel(172, 330), moved.GetPixel(172, 330));
    }
}
