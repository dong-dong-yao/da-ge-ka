using System.Drawing;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass, DoNotParallelize]
public sealed class MouseSelectionToolsTests
{
    [TestMethod]
    public void ReadingPng_PreservesPixelsRegardlessOfStoredDpi()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            using var source = Scene(); source.SetResolution(300, 300);
            source.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            using var loaded = CharacterMediaProcessor.ReadBitmap(file);
            Assert.AreEqual(source.Size, loaded.Size);
            Assert.AreEqual(source.GetPixel(140, 320).ToArgb(), loaded.GetPixel(140, 320).ToArgb());
            Assert.AreEqual(source.GetPixel(340, 320).ToArgb(), loaded.GetPixel(340, 320).ToArgb());
        }
        finally { File.Delete(file); }
    }
    private static MouseCalibration Spec() => new()
    {
        Rig = new() { ShoulderX = 200f / 600, ShoulderY = 220f / 448, MouseX = 140f / 600, MouseY = 320f / 448 },
        Pad = [new(40, 260), new(265, 260), new(265, 390), new(40, 390)]
    };
    [TestMethod]
    public void ClickMouse_SelectsWhiteInteriorAndBlackRim_NotPadOrKeyboard()
    {
        using var source = Scene(); using var mask = new Bitmap(600, 448);
        Assert.IsTrue(MouseSelectionTools.SelectConnected(source, mask, Spec(), new Point(140, 320)));
        Assert.AreEqual(255, mask.GetPixel(140, 320).A);
        Assert.AreEqual(255, mask.GetPixel(111, 320).A, "Include original black rim.");
        Assert.AreEqual(0, mask.GetPixel(60, 320).A, "Do not select gray pad.");
        Assert.AreEqual(0, mask.GetPixel(40, 320).A, "Protect pad border.");
        Assert.AreEqual(0, mask.GetPixel(340, 320).A, "Keyboard remains stationary.");
    }
    [TestMethod]
    public void ClickHand_StopsAtShoulder_EvenWhenSameColorContinuesIntoBody()
    {
        using var source = Scene(); using var mask = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(source))
        {
            g.FillRectangle(Brushes.Orange, 160, 100, 80, 100);
            using var pen = new Pen(Color.Orange, 34); g.DrawLine(pen, 205, 190, 140, 290);
        }
        Assert.IsTrue(MouseSelectionTools.SelectConnected(source, mask, Spec(), new Point(160, 260)));
        Assert.AreEqual(255, mask.GetPixel(160, 260).A);
        Assert.AreEqual(0, mask.GetPixel(190, 130).A);
    }
    [TestMethod]
    public void CompleteRim_DoesNotGrowIntoPadBorder_AndRepeatedClickIsIdempotent()
    {
        using var source = Scene(); using var mask = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(mask)) g.FillEllipse(Brushes.White, 114, 294, 52, 52);
        Assert.IsGreaterThan(0, MouseSelectionTools.CompleteDarkEdges(source, mask, Spec()));
        Assert.AreEqual(255, mask.GetPixel(111, 320).A);
        Assert.AreEqual(0, mask.GetPixel(40, 320).A);
        Assert.AreEqual(0, MouseSelectionTools.CompleteDarkEdges(source, mask, Spec()), "Repeated repair must not keep growing outward.");
    }
    [TestMethod]
    public void TintedDarkFringe_DoesNotBecomeANewExpansionSeed()
    {
        using var source = Scene(); using var mask = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(source))
        {
            using var fringe = new SolidBrush(Color.FromArgb(70, 33, 33));
            g.FillRectangle(fringe, 100, 305, 40, 20);
            g.FillRectangle(Brushes.White, 140, 305, 15, 20);
        }
        using (var g = Graphics.FromImage(mask)) g.FillRectangle(Brushes.White, 140, 305, 15, 20);
        Assert.IsGreaterThan(0, MouseSelectionTools.CompleteDarkEdges(source, mask, Spec()));
        Assert.AreEqual(0, MouseSelectionTools.CompleteDarkEdges(source, mask, Spec()));
    }
    [TestMethod]
    public void BrightPadWithGrayDetails_DoesNotExpandOnRepeatedRepair()
    {
        using var source = new Bitmap(600, 448); using var mask = new Bitmap(600, 448);
        var spec = Spec(); spec.RemoveConnectedPadColor = false;
        using (var g = Graphics.FromImage(source))
        {
            using var pad = new SolidBrush(Color.FromArgb(220, 220, 220));
            using var fringe = new SolidBrush(Color.FromArgb(200, 200, 200));
            g.FillRectangle(pad, 40, 260, 225, 130);
            g.FillRectangle(fringe, 100, 305, 40, 20);
            g.FillRectangle(Brushes.White, 140, 305, 15, 20);
        }
        using (var g = Graphics.FromImage(mask)) g.FillRectangle(Brushes.White, 140, 305, 15, 20);
        MouseSelectionTools.CompleteDarkEdges(source, mask, spec);
        Assert.AreEqual(0, MouseSelectionTools.CompleteDarkEdges(source, mask, spec));
    }
    [TestMethod]
    public void TransparentOrPadClick_DoesNotChangeExistingSelection()
    {
        using var source = Scene(); using var mask = new Bitmap(600, 448);
        mask.SetPixel(140, 320, Color.White);
        Assert.IsFalse(MouseSelectionTools.SelectConnected(source, mask, Spec(), new Point(1, 1)));
        Assert.IsFalse(MouseSelectionTools.SelectConnected(source, mask, Spec(), new Point(60, 320)));
        Assert.AreEqual(255, mask.GetPixel(140, 320).A);
    }
    private static Bitmap Scene()
    {
        var source = new Bitmap(600, 448);
        using var g = Graphics.FromImage(source);
        g.FillRectangle(Brushes.DarkGray, 40, 260, 225, 130);
        g.DrawRectangle(Pens.Black, 40, 260, 225, 130);
        g.FillEllipse(Brushes.Black, 110, 290, 60, 60);
        g.FillEllipse(Brushes.White, 114, 294, 52, 52);
        g.FillRectangle(Brushes.White, 320, 290, 100, 60);
        return source;
    }
}
