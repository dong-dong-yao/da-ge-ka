using System.Drawing;
using System.Drawing.Drawing2D;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class MousePadSelectionTests
{
    [TestMethod]
    public void ClickPad_FindsItsInteriorDespiteMouseHole_ExcludesKeyboardAndBorder()
    {
        using var source = Scene(0); var spec = Spec();
        Assert.IsTrue(MouseSelectionTools.TrySelectPad(source, spec, new Point(80, 320)));
        using var path = new GraphicsPath(); path.AddPolygon(spec.Pad.Select(p => new PointF(p.X, p.Y)).ToArray());
        Assert.IsTrue(path.IsVisible(130, 320));
        Assert.IsFalse(path.IsVisible(30, 320));
        Assert.IsFalse(path.IsVisible(350, 320));
        Assert.IsTrue(spec.Pad.Length is >= 3 and <= 128);
    }
    [TestMethod]
    public void AutomaticPadDetection_FollowsEachImagesShift()
    {
        using var a = Scene(0); using var b = Scene(45); var x = Spec(); var y = Spec();
        Assert.IsTrue(MouseSelectionTools.TrySelectPad(a, x, null));
        Assert.IsTrue(MouseSelectionTools.TrySelectPad(b, y, null));
        Assert.AreEqual(45f, y.Pad.Min(p => p.X) - x.Pad.Min(p => p.X), .1f);
    }
    [TestMethod]
    public void ClickingTransparentOrWhiteMouse_DoesNotReplacePad()
    {
        using var source = Scene(0); var spec = Spec(); var old = spec.Pad.ToArray();
        Assert.IsFalse(MouseSelectionTools.TrySelectPad(source, spec, new Point(1, 1)));
        Assert.IsFalse(MouseSelectionTools.TrySelectPad(source, spec, new Point(130, 320)));
        CollectionAssert.AreEqual(old, spec.Pad);
    }
    private static MouseCalibration Spec() => new() { Pad = [new(1, 1), new(2, 1), new(2, 2)] };
    private static Bitmap Scene(int shift)
    {
        var b = new Bitmap(600, 448); using var g = Graphics.FromImage(b);
        g.FillPolygon(Brushes.Black, [new Point(30+shift,310),new(100+shift,255),new(240+shift,300),new(165+shift,390)]);
        g.FillPolygon(Brushes.DarkGray, [new Point(36+shift,310),new(101+shift,261),new(233+shift,302),new(164+shift,384)]);
        g.FillEllipse(Brushes.Black, 100+shift, 295, 60, 60); g.FillEllipse(Brushes.White,104+shift,299,52,52);
        g.FillRectangle(Brushes.Gray, 320,300,15,15); g.FillRectangle(Brushes.White,350,300,80,50);
        return b;
    }
}
