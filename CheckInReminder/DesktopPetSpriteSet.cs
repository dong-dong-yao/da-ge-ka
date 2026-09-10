using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckInReminder;

internal sealed class DesktopPetSpriteSet : IDisposable
{
    private const int RenderWidth = 600;
    private const int RenderHeight = 448;
    private readonly Bitmap idle;
    private readonly Bitmap left;
    private readonly Bitmap? center;
    private readonly Bitmap right;
    private readonly MouseLayers idleMouseLayers;
    private readonly MouseLayers leftMouseLayers;
    private readonly MouseLayers? centerMouseLayers;
    private readonly MouseLayers rightMouseLayers;
    private bool disposed;

    private DesktopPetSpriteSet(string characterId, Bitmap idle, Bitmap left, Bitmap? center, Bitmap right)
    {
        this.idle = idle;
        this.left = left;
        this.center = center;
        this.right = right;
        try
        {
            idleMouseLayers = MouseLayers.Create(idle, characterId);
            leftMouseLayers = MouseLayers.Create(left, characterId);
            centerMouseLayers = center is null ? null : MouseLayers.Create(center, characterId);
            rightMouseLayers = MouseLayers.Create(right, characterId);
        }
        catch
        {
            idleMouseLayers?.Dispose();
            leftMouseLayers?.Dispose();
            centerMouseLayers?.Dispose();
            rightMouseLayers?.Dispose();
            throw;
        }
    }

    public bool HasCenterPose => center is not null;

    public Bitmap Idle => GetLive(idle);

    public Bitmap GetPressed(PetKeyboardPose pose) => GetLive(pose switch
    {
        PetKeyboardPose.Left => left,
        PetKeyboardPose.Center when center is not null => center,
        PetKeyboardPose.Center => right,
        _ => right,
    });

    public Bitmap Render(DesktopPetRigPose pose)
    {
        var keyboardPose = PetKeyboardPoseMapper.Map(pose.KeyboardTarget.X, HasCenterPose);
        var source = pose.KeyboardContact ? GetPressed(keyboardPose) : Idle;
        var mouseLayers = pose.KeyboardContact ? GetMouseLayers(keyboardPose) : idleMouseLayers;
        return RenderMouseMotion(source, mouseLayers, pose);
    }

    public static bool HasResources(string characterId)
    {
        var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        var prefix = ResourcePrefix(characterId);
        return Find(names, prefix, "idle") is not null
            && Find(names, prefix, "press-left") is not null
            && Find(names, prefix, "press-right") is not null;
    }

    public static bool TryLoad(string characterId, out DesktopPetSpriteSet result)
    {
        if (!HasResources(characterId))
        {
            result = null!;
            return false;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();
        var prefix = ResourcePrefix(characterId);
        Bitmap? idle = null;
        Bitmap? left = null;
        Bitmap? center = null;
        Bitmap? right = null;
        try
        {
            idle = Load(assembly, Find(names, prefix, "idle")!);
            left = Load(assembly, Find(names, prefix, "press-left")!);
            var centerName = Find(names, prefix, "press-center");
            if (centerName is not null) center = Load(assembly, centerName);
            right = Load(assembly, Find(names, prefix, "press-right")!);
            result = new DesktopPetSpriteSet(characterId, idle, left, center, right);
            return true;
        }
        catch
        {
            idle?.Dispose();
            left?.Dispose();
            center?.Dispose();
            right?.Dispose();
            throw;
        }
    }

    private static string ResourcePrefix(string characterId) =>
        $"CheckInReminder.Assets.DesktopPet.Characters.{characterId.Replace('-', '_')}.";

    private static string? Find(IEnumerable<string> names, string prefix, string stem) =>
        names.FirstOrDefault(name => name.StartsWith(prefix + stem + ".", StringComparison.Ordinal)
            && (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)));

    private static Bitmap Load(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"缺少嵌入桌宠素材：{resourceName}");
        using var source = Image.FromStream(stream);
        var result = new Bitmap(RenderWidth, RenderHeight, PixelFormat.Format32bppPArgb);
        try
        {
            using (var graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, RenderWidth, RenderHeight));
            }
            RemoveConnectedWhiteBackground(result);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private MouseLayers GetMouseLayers(PetKeyboardPose pose) => pose switch
    {
        PetKeyboardPose.Left => leftMouseLayers,
        PetKeyboardPose.Center when centerMouseLayers is not null => centerMouseLayers,
        PetKeyboardPose.Center => rightMouseLayers,
        _ => rightMouseLayers,
    };

    private static Bitmap RenderMouseMotion(Bitmap source, MouseLayers layers, DesktopPetRigPose pose)
    {
        var dx = Math.Clamp(pose.MouseOffset.X, -1f, 1f) * 6f;
        var dy = Math.Clamp(pose.MouseOffset.Y, -1f, 1f) * 4f
            + Math.Clamp(pose.MousePress, 0f, 1f) * 3f;
        var angle = Math.Clamp(pose.MouseRotationDegrees, -1f, 1f);
        if (Math.Abs(dx) < 0.01f && Math.Abs(dy) < 0.01f && Math.Abs(angle) < 0.01f)
            return source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);

        var result = layers.Background.Clone(
            new Rectangle(Point.Empty, layers.Background.Size),
            PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        PointF TransformArm(PointF point)
        {
            const float shoulderX = 270f;
            const float shoulderY = 205f;
            const float pawX = 145f;
            const float pawY = 330f;
            var armX = pawX - shoulderX;
            var armY = pawY - shoulderY;
            var along = ((point.X - shoulderX) * armX + (point.Y - shoulderY) * armY)
                / ((armX * armX) + (armY * armY));

            var radians = angle * Math.PI / 180d;
            var cosine = (float)Math.Cos(radians);
            var sine = (float)Math.Sin(radians);
            var localX = point.X - shoulderX;
            var localY = point.Y - shoulderY;
            return new PointF(
                shoulderX + localX * cosine - localY * sine + (dx * along),
                shoulderY + localX * sine + localY * cosine + (dy * along));
        }

        // One affine draw keeps the supplied arm pixels in a single continuous
        // surface. A tiled mesh can expose cell edges and visually split the arm.
        var destination = new[]
        {
            TransformArm(PointF.Empty),
            TransformArm(new PointF(RenderWidth, 0f)),
            TransformArm(new PointF(0f, RenderHeight)),
        };
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(layers.MouseArm, destination,
            new Rectangle(0, 0, RenderWidth, RenderHeight), GraphicsUnit.Pixel, attributes);
        return result;
    }

    private static void RemoveConnectedWhiteBackground(Bitmap bitmap)
    {
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var visited = new bool[bitmap.Width * bitmap.Height];
            var queue = new int[visited.Length];
            var head = 0;
            var tail = 0;

            void TryEnqueue(int x, int y)
            {
                if ((uint)x >= bitmap.Width || (uint)y >= bitmap.Height) return;
                var pixel = (y * bitmap.Width) + x;
                if (visited[pixel]) return;
                visited[pixel] = true;
                var offset = (y * data.Stride) + (x * 4);
                var alpha = bytes[offset + 3];
                if (alpha == 0 || (bytes[offset] * 255) < (alpha * 240)
                    || (bytes[offset + 1] * 255) < (alpha * 240)
                    || (bytes[offset + 2] * 255) < (alpha * 240)) return;
                bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = bytes[offset + 3] = 0;
                queue[tail++] = pixel;
            }

            for (var x = 0; x < bitmap.Width; x++)
            {
                TryEnqueue(x, 0);
                TryEnqueue(x, bitmap.Height - 1);
            }
            for (var y = 1; y < bitmap.Height - 1; y++)
            {
                TryEnqueue(0, y);
                TryEnqueue(bitmap.Width - 1, y);
            }
            while (head < tail)
            {
                var pixel = queue[head++];
                var x = pixel % bitmap.Width;
                var y = pixel / bitmap.Width;
                TryEnqueue(x - 1, y);
                TryEnqueue(x + 1, y);
                TryEnqueue(x, y - 1);
                TryEnqueue(x, y + 1);
            }
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private sealed class MouseLayers : IDisposable
    {
        private MouseLayers(Bitmap background, Bitmap mouseArm)
        {
            Background = background;
            MouseArm = mouseArm;
        }

        public Bitmap Background { get; }
        public Bitmap MouseArm { get; }

        public static MouseLayers Create(Bitmap source, string characterId)
        {
            Bitmap? background = null;
            Bitmap? mouseArm = null;
            try
            {
                using var mask = CreateMouseArmMask();
                background = source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);
                mouseArm = Extract(source, mask);
                using var repairMask = CreateRepairMask(mask);
                // Flat-color characters tolerate a slightly wider color bed;
                // the hippo's shaded 3D arm would show that wider bed as a patch.
                RepairMouseHole(background, source, mask, repairMask,
                    extendArmBed: characterId != "yellow-hippo");
                return new MouseLayers(background, mouseArm);
            }
            catch
            {
                background?.Dispose();
                mouseArm?.Dispose();
                throw;
            }
        }

        private static GraphicsPath CreateMouseArmMask()
        {
            // These four supplied characters share the same desk template but
            // not the white bear's much narrower forearm. Extract the complete
            // forearm as one piece; the mouse and pad stay in the static layer.
            var path = new GraphicsPath();
            path.AddPolygon(
            [
                new PointF(280, 215), new PointF(250, 214), new PointF(224, 219),
                new PointF(202, 227), new PointF(183, 237), new PointF(166, 248),
                new PointF(154, 262), new PointF(146, 272), new PointF(142, 281),
                new PointF(144, 289), new PointF(150, 296), new PointF(158, 300),
                new PointF(168, 299), new PointF(178, 294), new PointF(186, 289),
                new PointF(190, 289), new PointF(200, 284), new PointF(210, 274),
                new PointF(220, 265), new PointF(230, 257), new PointF(238, 255),
                new PointF(248, 258), new PointF(258, 264), new PointF(270, 265),
                new PointF(280, 258), new PointF(287, 245), new PointF(290, 225),
            ]);
            return path;
        }

        private static GraphicsPath CreateRepairMask(GraphicsPath armMask)
        {
            var result = (GraphicsPath)armMask.Clone();
            result.FillMode = FillMode.Winding;
            result.AddPolygon(
            [
                new PointF(232, 215), new PointF(205, 222), new PointF(180, 233),
                new PointF(155, 248), new PointF(136, 263), new PointF(131, 276),
                new PointF(143, 279), new PointF(149, 267), new PointF(166, 253),
                new PointF(184, 242), new PointF(207, 232), new PointF(234, 224),
            ]);
            return result;
        }

        private static Bitmap Extract(Bitmap source, GraphicsPath mask)
        {
            var result = new Bitmap(RenderWidth, RenderHeight, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.Clear(Color.Transparent);
            graphics.SetClip(mask);
            graphics.DrawImageUnscaled(source, Point.Empty);
            return result;
        }

        private static void RepairMouseHole(
            Bitmap background,
            Bitmap source,
            GraphicsPath armMask,
            GraphicsPath repairMask,
            bool extendArmBed)
        {
            using var graphics = Graphics.FromImage(background);
            graphics.SetClip(repairMask);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            using var transparent = new SolidBrush(Color.Transparent);
            graphics.FillRectangle(transparent, new Rectangle(Point.Empty, background.Size));

            using var pad = new SolidBrush(source.GetPixel(108, 336));
            using var padRegion = Polygon((0.133f, 0.573f), (0.437f, 0.654f),
                (0.444f, 0.674f), (0.295f, 0.861f), (0.270f, 0.870f),
                (0f, 0.719f), (0f, 0.682f));
            graphics.FillPath(pad, padRegion);

            // Preserve a borderless color bed below the original limb. It fills
            // pixels that do not exist in the single supplied composite image,
            // while the transformed source arm still supplies the real contour.
            graphics.ResetClip();
            using var armBed = new LinearGradientBrush(
                new PointF(180f, 270f), new PointF(220f, 240f),
                source.GetPixel(180, 270), source.GetPixel(220, 240));
            graphics.FillPath(armBed, extendArmBed ? repairMask : armMask);

            // The supplied still image contains no pixels for the part of the
            // mouse hidden below the paw. Rebuild the complete stationary mouse
            // before placing the moving arm over it, avoiding clipped fragments.
            graphics.ResetClip();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var mouseFill = new SolidBrush(Color.FromArgb(255, 248, 248, 248));
            using var mouseOutline = new Pen(Color.FromArgb(255, 20, 20, 20), 5f);
            var mouseBounds = new RectangleF(116f, 277f, 80f, 61f);
            graphics.FillEllipse(mouseFill, mouseBounds);
            graphics.DrawEllipse(mouseOutline, mouseBounds);
            using var mouseWheel = new Pen(Color.FromArgb(255, 20, 20, 20), 4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            graphics.DrawLine(mouseWheel, new PointF(153f, 301f), new PointF(148f, 319f));
        }

        private static GraphicsPath Polygon(params (float X, float Y)[] points)
        {
            var path = new GraphicsPath();
            path.AddPolygon(points.Select(point => At(point.X, point.Y)).ToArray());
            return path;
        }

        private static PointF At(float x, float y) => new(x * RenderWidth, y * RenderHeight);

        public void Dispose()
        {
            Background.Dispose();
            MouseArm.Dispose();
        }
    }

    private Bitmap GetLive(Bitmap frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return frame;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        idleMouseLayers.Dispose();
        leftMouseLayers.Dispose();
        centerMouseLayers?.Dispose();
        rightMouseLayers.Dispose();
        idle.Dispose();
        left.Dispose();
        center?.Dispose();
        right.Dispose();
    }
}
