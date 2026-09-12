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

    private DesktopPetSpriteSet(Bitmap idle, Bitmap left, Bitmap? center, Bitmap right)
    {
        this.idle = idle;
        this.left = left;
        this.center = center;
        this.right = right;
        try
        {
            idleMouseLayers = MouseLayers.Create(idle);
            leftMouseLayers = MouseLayers.Create(left);
            centerMouseLayers = center is null ? null : MouseLayers.Create(center);
            rightMouseLayers = MouseLayers.Create(right);
            NormalizeRestFrame(idle, idleMouseLayers);
            NormalizeRestFrame(left, leftMouseLayers);
            if (center is not null) NormalizeRestFrame(center, centerMouseLayers!);
            NormalizeRestFrame(right, rightMouseLayers);
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
        ObjectDisposedException.ThrowIf(disposed, this);
        var keyboardPose = PetKeyboardPoseMapper.Map(pose.KeyboardTarget.X, HasCenterPose);
        var mouseLayers = pose.KeyboardContact ? GetMouseLayers(keyboardPose) : idleMouseLayers;
        return RenderMouseMotion(mouseLayers, pose);
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
            result = new DesktopPetSpriteSet(idle, left, center, right);
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

    private static Bitmap RenderMouseMotion(MouseLayers layers, DesktopPetRigPose pose)
    {
        var dx = Math.Clamp(pose.MouseOffset.X, -1f, 1f) * 6f;
        var dy = Math.Clamp(pose.MouseOffset.Y, -1f, 1f) * 4f
            + Math.Clamp(pose.MousePress, 0f, 1f) * 3f;
        var angle = Math.Clamp(pose.MouseRotationDegrees, -1f, 1f);
        var result = layers.Background.Clone(
            new Rectangle(Point.Empty, layers.Background.Size), PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // Even an unscaled transparent SourceOver draw can round destination
        // alpha at unrelated edges. Limit both rest and motion to this rig's
        // complete swept bounds, leaving the desk/keyboard byte-for-byte intact.
        graphics.SetClip(new Rectangle(96, 184, 188, 184));
        WhiteBearRigRenderer.DrawPinnedMouseArm(graphics, layers.MouseArm, dx, dy, angle,
            new PointF(205, 216), new PointF(240, 260), new PointF(205, 216),
            new Rectangle(96, 192, 176, 160));
        return result;
    }

    private static void NormalizeRestFrame(Bitmap frame, MouseLayers layers)
    {
        using var composed = RenderMouseMotion(layers, DesktopPetRigPose.Rest);
        using var graphics = Graphics.FromImage(frame);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(composed, Point.Empty);
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

        public static MouseLayers Create(Bitmap source)
        {
            Bitmap? background = null;
            Bitmap? mouseArm = null;
            try
            {
                using var mask = CreateMouseArmMask(source);
                var deskEdge = DeskEdge.Find(source);
                background = source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);
                mouseArm = Extract(source, mask);
                RepairMouseHole(background, source, mask, deskEdge);
                if (deskEdge is not null)
                    SeparateDeskEdge(source, background, mouseArm, deskEdge);
                return new MouseLayers(background, mouseArm);
            }
            catch
            {
                background?.Dispose();
                mouseArm?.Dispose();
                throw;
            }
        }

        private static GraphicsPath CreateMouseArmMask(Bitmap source)
        {
            // Calibrate to each supplied pose's actual skin/mouse silhouette,
            // rather than copying another character's arm coordinates. The desk
            // is gray, whereas the paw/mouse is white or chromatic.
            var spans = new List<Rectangle>();
            var coverage = new bool[RenderWidth * RenderHeight];
            using var envelope = new GraphicsPath();
            envelope.AddPolygon([
                new PointF(200, 205), new PointF(268, 265), new PointF(235, 270),
                new PointF(205, 289), new PointF(198, 313), new PointF(182, 335),
                new PointF(137, 340), new PointF(110, 327), new PointF(107, 304),
                new PointF(120, 280), new PointF(133, 265), new PointF(150, 243),
                new PointF(175, 221)]);
            for (var y = 196; y < 342; y++)
            {
                var first = 272;
                var last = -1;
                for (var x = 108; x < 268; x++)
                {
                    if (!envelope.IsVisible(x, y)) continue;
                    var color = source.GetPixel(x, y);
                    var high = Math.Max(color.R, Math.Max(color.G, color.B));
                    var low = Math.Min(color.R, Math.Min(color.G, color.B));
                    if (color.A < 180 || !(low > 215 || high - low > 30)) continue;
                    first = Math.Min(first, x);
                    last = x;
                }
                if (last < first) continue;
                // Include the original dark outline and its antialiased fringe.
                for (var yy = Math.Max(192, y - 8); yy <= Math.Min(347, y + 8); yy++)
                for (var xx = Math.Max(104, first - 8); xx <= Math.Min(271, last + 8); xx++)
                    coverage[yy * RenderWidth + xx] = true;
            }
            for (var y = 192; y < 348; y++)
            {
                var first = 272;
                var last = -1;
                for (var x = 104; x < 272; x++)
                    if (coverage[y * RenderWidth + x]) { first = Math.Min(first, x); last = x; }
                if (last >= first) spans.Add(new Rectangle(first, y, last - first + 1, 1));
            }
            var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangles(spans.ToArray());
            // The mouse outline is much thicker than the skin antialiasing.
            // Include the whole original rim (also the part below the paw).
            path.AddEllipse(108, 271, 96, 74);
            return path;
        }

        private static Bitmap Extract(Bitmap source, GraphicsPath mask)
        {
            var result = new Bitmap(RenderWidth, RenderHeight, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(result))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.Clear(Color.Transparent);
                graphics.SetClip(mask);
                graphics.DrawImageUnscaled(source, Point.Empty);
            }
            // A generous contour mask prevents leftover black outlines, but its
            // safety margin also contains gray pad pixels. Those belong only to
            // the static layer; retain the original skin, mouse and dark rim.
            var pad = source.GetPixel(108, 336).R;
            for (var y = 192; y < 348; y++)
            for (var x = 104; x < 272; x++)
            {
                var color = result.GetPixel(x, y);
                if (color.A == 0) continue;
                var high = Math.Max(color.R, Math.Max(color.G, color.B));
                var low = Math.Min(color.R, Math.Min(color.G, color.B));
                if (high - low > 15 || low < 85 || high > 215) continue;
                if (Math.Abs(color.R - pad) < 20)
                    result.SetPixel(x, y, Color.Transparent);
                else
                {
                    var shade = color.R < pad ? 0 : 255;
                    var opacity = Math.Clamp(Math.Abs(color.R - pad) / (float)Math.Abs(shade - pad), 0, 1);
                    result.SetPixel(x, y, Color.FromArgb((int)(color.A * opacity), shade, shade, shade));
                }
            }
            return result;
        }

        private static void RepairMouseHole(Bitmap background, Bitmap source, GraphicsPath mask, DeskEdge? deskEdge)
        {
            using var graphics = Graphics.FromImage(background);
            graphics.SetClip(mask);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            // Keep every pinned shoulder pixel in the static source as well;
            // rasterizing a cut twice must not expose a one-pixel seam.
            using var movingHalfPlane = new GraphicsPath();
            movingHalfPlane.AddPolygon([new PointF(205, 216), new PointF(240, 260),
                new PointF(390, 448), new PointF(0, 448), new PointF(0, 0), new PointF(33, 0)]);
            graphics.SetClip(movingHalfPlane, CombineMode.Intersect);
            using var transparent = new SolidBrush(Color.Transparent);
            graphics.FillRectangle(transparent, new Rectangle(Point.Empty, source.Size));
            // Only the desk/pad is underneath the extracted limb. Never paint
            // skin or a second mouse here: that would remain behind during motion.
            using var pad = new SolidBrush(source.GetPixel(108, 336));
            using var padRegion = new GraphicsPath();
            padRegion.AddPolygon(
            [
                new PointF(0, 220), new PointF(145, 249), new PointF(265, 274),
                new PointF(265, 300), new PointF(170, 375), new PointF(0, 325),
            ]);
            graphics.FillPath(pad, padRegion);
            if (deskEdge is not null)
            {
                // Continue the stationary desk behind the original occluding
                // arm. The existing clip limits this to the repaired hole.
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(deskEdge.Color, deskEdge.Width);
                graphics.DrawLine(pen, 0, deskEdge.Y(0), RenderWidth, deskEdge.Y(RenderWidth));
            }
        }

        private static void SeparateDeskEdge(Bitmap source, Bitmap background, Bitmap arm, DeskEdge desk)
        {
            bool IsSkin(int x, int y)
            {
                var c = source.GetPixel(x, y);
                var hi = Math.Max(c.R, Math.Max(c.G, c.B));
                var lo = Math.Min(c.R, Math.Min(c.G, c.B));
                return c.A >= 240 && (lo > 235 || hi - lo > 30);
            }

            for (var x = 104; x < 272; x++)
            {
                var center = desk.Y(x);
                var radius = (int)Math.Ceiling(desk.Width / 2 + 2);
                for (var y = (int)Math.Floor(center) - radius; y <= (int)Math.Ceiling(center) + radius; y++)
                {
                    // Keep the arm's actual contour at its intersection with
                    // the desk, including the original black outline. Pixels
                    // away from skin belong to the stationary desk, not the rig.
                    var nearSkin = false;
                    for (var dy = -4; dy <= 4 && !nearSkin; dy++)
                    for (var dx = -4; dx <= 4 && !nearSkin; dx++)
                        if (dx * dx + dy * dy <= 16 && IsSkin(x + dx, y + dy)) nearSkin = true;
                    if (nearSkin) continue;
                    arm.SetPixel(x, y, Color.Transparent);
                    background.SetPixel(x, y, source.GetPixel(x, y));
                }
            }
        }

        private sealed record DeskEdge(float Slope, float Intercept, float Width, Color Color)
        {
            public float Y(float x) => Intercept + Slope * x;

            public static DeskEdge? Find(Bitmap source)
            {
                // Fit the unoccluded left segment separately for every supplied
                // pose. The PNG and JPEG variants do not share exact placement.
                var samples = new List<(double X, double Y, int Thickness)>();
                var ink = Color.Black;
                var darkest = 256;
                for (var x = 8; x <= 96; x += 2)
                {
                    var rows = new List<int>();
                    for (var y = 190; y < 265; y++)
                    {
                        var c = source.GetPixel(x, y);
                        var value = Math.Max(c.R, Math.Max(c.G, c.B));
                        if (c.A < 180 || value >= 90) continue;
                        rows.Add(y);
                        if (value < darkest) { darkest = value; ink = Color.FromArgb(255, c.R, c.G, c.B); }
                    }
                    if (rows.Count != 0) samples.Add((x, rows.Average(), rows.Count));
                }
                if (samples.Count < 20) return null;
                var mx = samples.Average(p => p.X);
                var my = samples.Average(p => p.Y);
                var slope = samples.Sum(p => (p.X - mx) * (p.Y - my)) / samples.Sum(p => (p.X - mx) * (p.X - mx));
                return new DeskEdge((float)slope, (float)(my - slope * mx),
                    (float)samples.Average(p => p.Thickness) + 0.5f, ink);
            }
        }

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
