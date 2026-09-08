using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class KeyboardPressPoseTests
{
    [TestMethod]
    [DataRow("left.png", 1448, 1086, "6A7F45A0A28D8D46C8C4748073455FB4D6FE4666AF1F9E72303FD52B5ADB9170")]
    [DataRow("center-accent.png", 1448, 1086, "5A5CAD77BB0A885A33EA57DF6FABEABB63F3F8CEB3ABC04F8245EB9D63A7B545")]
    [DataRow("right.png", 1447, 1087, "B9CD57E8230E70CC8999573BCDD2E24440F666127680D52DA15A70399F038ADE")]
    public void PressAssets_AreTheExactTransparentUserPngs(string name, int width, int height, string hash)
    {
        using var stream = typeof(WhiteBearRigRenderer).Assembly.GetManifestResourceStream(
            "CheckInReminder.Assets.DesktopPet.KeyboardPress." + name);
        Assert.IsNotNull(stream, $"The exact user-provided {name} must be embedded.");
        Assert.AreEqual(hash, Convert.ToHexString(SHA256.HashData(stream)));
        stream.Position = 0;
        using var image = new Bitmap(stream);
        Assert.AreEqual(new Size(width, height), image.Size);
        foreach (var point in new[] { new Point(0, 0), new Point(width - 1, 0),
                     new Point(0, height - 1), new Point(width - 1, height - 1) })
            Assert.AreEqual(0, image.GetPixel(point.X, point.Y).A);
    }

    [TestMethod]
    public void RestAndReleasedTargets_PreserveThePreChangeIdlePixelsExactly()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        foreach (var target in new[] { DesktopPetRigPose.Rest.KeyboardTarget, PointF.Empty, new PointF(1, 1) })
        {
            using var frame = renderer.Render(DesktopPetRigPose.Rest with { KeyboardTarget = target });
            Assert.AreEqual("D913751EAD9D77EA2822B2F281C39F49D793BB067274335ED119F2A61F548E09", PixelHash(frame),
                "A released keyboard must restore the original idle artwork regardless of its previous target.");
        }
    }

    [TestMethod]
    public void RepresentativeKeys_ContactThreeDistinctKeyboardRegions()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var rest = renderer.Render(DesktopPetRigPose.Rest);
        var contacts = new List<PointF>();
        foreach (var key in new[] { 0x51, 0x47, 0x50 }) // Q, G, P: independently chosen physical zones.
        {
            Assert.IsTrue(KeyboardTargetMapper.TryMap(key, out var target));
            using var pressed = renderer.Render(DesktopPetRigPose.Rest with { KeyboardTarget = target, KeyboardPress = 1 });
            var points = ChangedPoints(rest, pressed, new Rectangle(345, 312, 135, 70));
            Assert.IsGreaterThan(350, points.Count, "The provided paw must visibly cover keys below the old floating idle arm.");
            contacts.Add(new PointF((float)points.Average(p => p.X), (float)points.Average(p => p.Y)));
        }
        Assert.IsGreaterThan(8f, contacts[1].X - contacts[0].X, "Left and center need distinct contact silhouettes.");
        Assert.IsGreaterThan(20f, contacts[2].X - contacts[1].X, "Center and right need distinct contact silhouettes.");
    }

    [TestMethod]
    [DataRow(0.1f)]
    [DataRow(0.5f)]
    [DataRow(0.9f)]
    public void Strike_DeformsTheLowerHandWhileTheShoulderRemainsPinned(float x)
    {
        using var renderer = WhiteBearRigRenderer.Load();
        var pose = DesktopPetRigPose.Rest with { KeyboardTarget = new PointF(x, 0.5f), KeyboardPress = 0.2f };
        using var raised = renderer.Render(pose);
        using var struck = renderer.Render(pose with { KeyboardPress = 1 });
        using var rest = renderer.Render(DesktopPetRigPose.Rest);
        AssertRegionEqual(raised, struck, new Rectangle(350, 235, 120, 68));
        var anchoredDark = 0;
        for (var y = 244; y <= 255; y++)
        for (var px = 402; px <= 413; px++)
        {
            var color = struck.GetPixel(px, y);
            Assert.AreEqual(255, color.A, "The fixed shoulder must stay joined to opaque body pixels.");
            if (color.R < 100) anchoredDark++;
        }
        Assert.IsGreaterThan(2, anchoredDark, "Each pose's shoulder contour must meet the same 12px anchor neighborhood.");
        Assert.IsGreaterThan(30, ChangedPoints(raised, struck, new Rectangle(350, 304, 125, 82)).Count,
            "A strike must deform the hand, rather than translate the entire arm or leave it motionless.");
        var contact = new Rectangle(350, 304, 125, 82);
        var downTravel = ChangedPoints(rest, struck, contact).Max(p => p.Y)
            - ChangedPoints(rest, raised, contact).Max(p => p.Y);
        Assert.IsTrue(downTravel >= 2 && downTravel <= 6,
            $"The lower contact edge must travel down 2–6px, not upward or across the whole keyboard; actual {downTravel}px.");
    }

    [TestMethod]
    public void KeyRows_OnlyAdjustTheHandAndNeverTranslateTheShoulder()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var top = renderer.Render(DesktopPetRigPose.Rest with { KeyboardTarget = new PointF(0.5f, 0), KeyboardPress = 1 });
        using var bottom = renderer.Render(DesktopPetRigPose.Rest with { KeyboardTarget = new PointF(0.5f, 1), KeyboardPress = 1 });
        AssertRegionEqual(top, bottom, new Rectangle(350, 235, 120, 68));
        var changes = ChangedPoints(top, bottom, new Rectangle(345, 235, 135, 150));
        Assert.IsGreaterThan(20, changes.Count);
        Assert.IsTrue(changes.All(p => p.Y >= 303 && p.Y < 345), "Row adjustment must remain local to the center paw.");
    }

    [TestMethod]
    public void Pressing_ChangesOnlyTheArmOcclusionAndRemovesTheOldRaisedContour()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var rest = renderer.Render(DesktopPetRigPose.Rest);
        foreach (var x in new[] { 0.1f, 0.5f, 0.9f })
        {
            using var pressed = renderer.Render(DesktopPetRigPose.Rest with { KeyboardTarget = new PointF(x, 0.5f), KeyboardPress = 1 });
            var oldArm = new Rectangle(382, 180, 72, 96);
            var newArm = new Rectangle(350, 241, 120, 143);
            foreach (var point in ChangedPoints(rest, pressed, new Rectangle(0, 0, 600, 448)))
                Assert.IsTrue(oldArm.Contains(point) || newArm.Contains(point),
                    $"Body, mouse, desk or keyboard changed outside arm occlusion at {point}.");
            Assert.IsGreaterThan(240, pressed.GetPixel(408, 186).R, "The old lifted arc must not remain as a double outline.");
        }
    }

    [TestMethod]
    public void CenterPose_ExcludesBothSetsOfSourceMotionMarks()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var rest = renderer.Render(DesktopPetRigPose.Rest);
        using var pressed = renderer.Render(DesktopPetRigPose.Rest with { KeyboardTarget = new PointF(0.5f, 0.5f), KeyboardPress = 1 });
        // Source marks at (815,690) and (990,770), registered to the fixed shoulder.
        AssertRegionEqual(rest, pressed, new Rectangle(359, 267, 14, 22));
        AssertRegionEqual(rest, pressed, new Rectangle(438, 300, 10, 18));
    }

    private static List<Point> ChangedPoints(Bitmap first, Bitmap second, Rectangle region)
    {
        var result = new List<Point>();
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
            if (first.GetPixel(x, y).ToArgb() != second.GetPixel(x, y).ToArgb()) result.Add(new Point(x, y));
        return result;
    }

    private static void AssertRegionEqual(Bitmap first, Bitmap second, Rectangle region) =>
        Assert.HasCount(0, ChangedPoints(first, second, region), $"Pinned/source region {region} must remain pixel-identical.");

    private static string PixelHash(Bitmap frame)
    {
        var data = frame.LockBits(new Rectangle(Point.Empty, frame.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { frame.UnlockBits(data); }
    }
}
