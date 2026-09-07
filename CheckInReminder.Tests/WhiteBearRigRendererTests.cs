using System.Drawing;
using System.Security.Cryptography;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WhiteBearRigRendererTests
{
    [TestMethod]
    public void EmbeddedArtwork_IsExactlyTheUserProvidedJpeg()
    {
        using var stream = typeof(DesktopPetMotionModel).Assembly.GetManifestResourceStream(
            "CheckInReminder.Assets.DesktopPet.white-bear-typing.jpg");
        Assert.IsNotNull(stream);
        Assert.AreEqual(
            "A20BB054864E32BD60F356F1931171D97B3CC79C6CCF9DE25C3B8A2E6C429782",
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    [TestMethod]
    public void Renderer_PreservesSourceAspectAndTransparentExterior()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var frame = renderer.Render(DesktopPetRigPose.Rest);
        Assert.AreEqual(new Size(600, 448), renderer.FrameSize);
        Assert.AreEqual(renderer.FrameSize, frame.Size);
        Assert.AreEqual(2400d / 1792d, (double)frame.Width / frame.Height, 0.01);
        Assert.AreEqual(0, frame.GetPixel(0, 0).A);
    }

    [TestMethod]
    public void MouseAndKeyboardPosesMoveIndependentPixelRegions()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var idle = renderer.Render(DesktopPetRigPose.Rest);
        using var mouse = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(1, -1), MousePress = 1
        });
        using var keyboard = renderer.Render(DesktopPetRigPose.Rest with
        {
            KeyboardTarget = new PointF(0.8f, 0.7f), KeyboardPress = 1
        });

        var mouseBounds = DifferenceBounds(idle, mouse);
        var keyboardBounds = DifferenceBounds(idle, keyboard);
#pragma warning disable MSTEST0037 // Express the left-to-right geometry without threshold-first ambiguity.
        Assert.IsTrue(mouseBounds.Right < keyboardBounds.Left,
            $"Mouse {mouseBounds} must remain independent of keyboard {keyboardBounds}.");
#pragma warning restore MSTEST0037
        Assert.IsGreaterThan(20, mouseBounds.Width);
        Assert.IsGreaterThan(20, keyboardBounds.Width);
    }

    [TestMethod]
    public void RenderingBothArms_PreservesTheFaceAndKeyboardArtwork()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var idle = renderer.Render(DesktopPetRigPose.Rest);
        using var active = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(-1, 1), MouseRotationDegrees = 2.5f,
            MousePress = 1, KeyboardTarget = new PointF(0.05f, 0.98f), KeyboardPress = 1
        });

        AssertRegionEqual(idle, active, new Rectangle(240, 150, 115, 68));
        AssertRegionEqual(idle, active, new Rectangle(220, 370, 300, 50));
    }

    [TestMethod]
    public void MouseMotion_MovesTheOriginalPawTipAndMouseBelowTheExampleMask()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var idle = renderer.Render(DesktopPetRigPose.Rest);
        using var moved = renderer.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 0) });

        // These source contours are below the brief's original 0.62-high mask.
        // Both the paw and the original, partly occluded mouse must travel together.
        foreach (var point in new[] { new Point(168, 311), new Point(150, 344) })
        {
            Assert.IsLessThan(80, idle.GetPixel(point.X, point.Y).R, $"Missing source contour at {point}.");
            Assert.AreEqual(idle.GetPixel(point.X, point.Y).ToArgb(),
                moved.GetPixel(point.X + 12, point.Y).ToArgb(), $"Source contour {point} did not move.");
        }
    }

    [TestMethod]
    public void MouseMotion_DoesNotLeaveTheOriginalMouseOutlineOnThePad()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var moved = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(1, -1), MouseRotationDegrees = 2.5f, MousePress = 1
        });

        foreach (var point in new[]
        {
            new Point(134, 296), new Point(131, 298), new Point(129, 300), new Point(128, 302)
        })
            Assert.IsGreaterThan(150, moved.GetPixel(point.X, point.Y).R,
                $"The original mouse outline must be reconstructed as the gray pad at {point}.");
    }

    [TestMethod]
    public void KeyboardMotion_LeavesTheOriginalOuterBodyContourInPlace()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var idle = renderer.Render(DesktopPetRigPose.Rest);
        using var moved = renderer.Render(DesktopPetRigPose.Rest with
        {
            KeyboardTarget = new PointF(0.05f, 0.98f), KeyboardPress = 1
        });
        AssertRegionEqual(idle, moved, new Rectangle(457, 186, 12, 127));
    }

    [TestMethod]
    public void KeyboardMotion_RemovesTheEntireOriginalArmContour()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var moved = renderer.Render(DesktopPetRigPose.Rest with
        {
            KeyboardTarget = PointF.Empty, KeyboardPress = 1
        });
        Assert.IsGreaterThan(240, moved.GetPixel(430, 197).R,
            "The original right arm edge must not survive as a second ghost contour.");
    }

    [TestMethod]
    public void MouseMotion_KeepsTheShoulderAttachedToTheExistingBody()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var moved = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(-1, 1), MouseRotationDegrees = -2.5f, MousePress = 1
        });
        for (var y = 235; y <= 255; y++)
            Assert.AreEqual(255, moved.GetPixel(215, y).A,
                $"The existing inner shoulder must not open a transparent hole at (215, {y}).");
    }

    [TestMethod]
    public void MouseMotion_AnchorsTheShoulderAndDoesNotCoverTheDeskEdge()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var idle = renderer.Render(DesktopPetRigPose.Rest);
        using var moved = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(-1, 1), MouseRotationDegrees = -2.5f, MousePress = 1
        });
        Assert.IsLessThan(80, moved.GetPixel(211, 228).R,
            "The original shoulder anchor must remain dark and attached.");
        AssertRegionEqual(idle, moved, new Rectangle(247, 274, 12, 8));
    }

    [TestMethod]
    public void DeformedMouseArm_HasNoMeshCracksThroughItsWhiteInterior()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var moved = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(-1, 1), MouseRotationDegrees = -2.5f, MousePress = 1
        });
        for (var y = 298; y < 310; y++)
        for (var x = 162; x < 178; x++)
            Assert.IsGreaterThan(235, moved.GetPixel(x, y).R,
                $"The original white paw interior must not expose the gray pad at ({x}, {y}).");
    }

    [TestMethod]
    public void MouseMotion_LeavesOneConnectedOuterShoulderInsteadOfAnOldEdgeAndBlueWedge()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        foreach (var pose in new[]
        {
            DesktopPetRigPose.Rest with
            {
                MouseOffset = new PointF(1, -1), MouseRotationDegrees = 2.5f, MousePress = 1
            },
            DesktopPetRigPose.Rest with
            {
                MouseOffset = new PointF(-1, 1), MouseRotationDegrees = -2.5f, MousePress = 1
            },
            DesktopPetRigPose.Rest with
            {
                MouseOffset = new PointF(1, 1), MouseRotationDegrees = -2.5f, MousePress = 1
            }
        })
        {
            using var frame = renderer.Render(pose);
            for (var y = 231; y <= 250; y++)
            {
                var visibleRuns = CountVisibleRuns(frame, y, 175, 226);
                Assert.AreEqual(1, visibleRuns,
                    $"Outer shoulder row {y} must contain exactly one visible run; actual {visibleRuns} means the shoulder is missing or an old edge and transparent wedge remain.");
            }
        }
    }

    [TestMethod]
    public void MouseRotationAlone_ProducesAVisibleDifferenceLimitedToTheMouseRegion()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var idle = renderer.Render(DesktopPetRigPose.Rest);
        using var rotated = renderer.Render(DesktopPetRigPose.Rest with { MouseRotationDegrees = 2.5f });

        var bounds = DifferenceBounds(idle, rotated);
        Assert.IsTrue(bounds.Width > 20 && bounds.Height > 20, $"Rotation difference was too small: {bounds}.");
        Assert.IsTrue(bounds.Left >= 100 && bounds.Right < 280,
            $"Rotation changed pixels outside the mouse region: {bounds}.");
    }

    [TestMethod]
    public void OutOfRangePose_RendersExactlyLikeItsClampedBoundaryPose()
    {
        using var renderer = WhiteBearRigRenderer.Load();
        using var boundary = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(1, -1), MouseRotationDegrees = 2.5f, MousePress = 1,
            KeyboardTarget = new PointF(0, 1), KeyboardPress = 1
        });
        using var outOfRange = renderer.Render(DesktopPetRigPose.Rest with
        {
            MouseOffset = new PointF(50, -50), MouseRotationDegrees = 90, MousePress = 50,
            KeyboardTarget = new PointF(-50, 50), KeyboardPress = 50
        });

        Assert.AreEqual(Rectangle.Empty, DifferenceBounds(boundary, outOfRange));
    }

    [TestMethod]
    public void FramesAreDeterministicAndCallerOwned()
    {
        var renderer = WhiteBearRigRenderer.Load();
        using var first = renderer.Render(DesktopPetRigPose.Rest);
        using var second = renderer.Render(DesktopPetRigPose.Rest);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(Rectangle.Empty, DifferenceBounds(first, second));
        first.SetPixel(0, 0, Color.Red);
        Assert.AreEqual(0, second.GetPixel(0, 0).A);

        renderer.Dispose();
        renderer.Dispose();
        Assert.AreEqual(Color.Red.ToArgb(), first.GetPixel(0, 0).ToArgb());
        Assert.Throws<ObjectDisposedException>(() => renderer.Render(DesktopPetRigPose.Rest));
    }

    private static void AssertRegionEqual(Bitmap first, Bitmap second, Rectangle region)
    {
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
            Assert.AreEqual(first.GetPixel(x, y).ToArgb(), second.GetPixel(x, y).ToArgb(),
                $"Source detail at ({x}, {y}) must remain unchanged.");
    }

    private static Rectangle DifferenceBounds(Bitmap first, Bitmap second)
    {
        var left = first.Width;
        var top = first.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < first.Height; y++)
        for (var x = 0; x < first.Width; x++)
        {
            if (first.GetPixel(x, y).ToArgb() == second.GetPixel(x, y).ToArgb()) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static int CountVisibleRuns(Bitmap frame, int y, int left, int right)
    {
        var runs = 0;
        var visible = false;
        for (var x = left; x < right; x++)
        {
            var nextVisible = frame.GetPixel(x, y).A >= 32;
            if (nextVisible && !visible) runs++;
            visible = nextVisible;
        }

        return runs;
    }
}
