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

    private static Bitmap RenderMouseMotion(Bitmap source, MouseLayers layers, DesktopPetRigPose pose)
    {
        var dx = Math.Clamp(pose.MouseOffset.X, -1f, 1f) * 12f;
        var dy = Math.Clamp(pose.MouseOffset.Y, -1f, 1f) * 8f
            + Math.Clamp(pose.MousePress, 0f, 1f) * 5f;
        var angle = Math.Clamp(pose.MouseRotationDegrees, -2.5f, 2.5f);
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

        PointF Deform(PointF point)
        {
            const float shoulderX = 270f;
            const float shoulderY = 205f;
            const float pawX = 145f;
            const float pawY = 330f;
            var armX = pawX - shoulderX;
            var armY = pawY - shoulderY;
            var along = Math.Clamp(
                ((point.X - shoulderX) * armX + (point.Y - shoulderY) * armY)
                / ((armX * armX) + (armY * armY)),
                0f,
                1f);
            var closestX = shoulderX + armX * along;
            var closestY = shoulderY + armY * along;
            var crossX = (point.X - closestX) / 105f;
            var crossY = (point.Y - closestY) / 105f;
            var armWeight = along * MathF.Exp(-(crossX * crossX + crossY * crossY));
            var weight = Math.Clamp(armWeight, 0f, 1f);

            var radians = angle * Math.PI / 180d;
            var cosine = (float)Math.Cos(radians);
            var sine = (float)Math.Sin(radians);
            var localX = point.X - shoulderX;
            var localY = point.Y - shoulderY;
            var movedX = shoulderX + localX * cosine - localY * sine + dx;
            var movedY = shoulderY + localX * sine + localY * cosine + dy;
            return new PointF(
                point.X + (movedX - point.X) * weight,
                point.Y + (movedY - point.Y) * weight);
        }

        var region = new Rectangle(0, 128, 352, 320);
        const int cell = 32;
        for (var y = region.Top; y < region.Bottom; y += cell)
        for (var x = region.Left; x < region.Right; x += cell)
        {
            var width = Math.Min(cell, region.Right - x);
            var height = Math.Min(cell, region.Bottom - y);
            var sourceCell = new RectangleF(x, y, width, height);
            var topLeft = Deform(new PointF(x, y));
            var topRight = Deform(new PointF(x + width, y));
            var bottomLeft = Deform(new PointF(x, y + height));
            var bottomRight = Deform(new PointF(x + width, y + height));
            DrawTriangle(graphics, layers.MouseArm, sourceCell,
                [topLeft, topRight, bottomLeft], [topLeft, topRight, bottomLeft]);
            var opposite = new PointF(
                topRight.X + bottomLeft.X - bottomRight.X,
                topRight.Y + bottomLeft.Y - bottomRight.Y);
            DrawTriangle(graphics, layers.MouseArm, sourceCell,
                [opposite, topRight, bottomLeft], [topRight, bottomRight, bottomLeft]);
        }
        return result;
    }

    private static void DrawTriangle(
        Graphics graphics,
        Bitmap source,
        RectangleF sourceBounds,
        PointF[] destination,
        PointF[] triangle)
    {
        var state = graphics.Save();
        try
        {
            using var clip = new GraphicsPath();
            clip.AddPolygon(triangle);
            graphics.SetClip(clip, CombineMode.Intersect);
            var m11 = (destination[1].X - destination[0].X) / sourceBounds.Width;
            var m12 = (destination[1].Y - destination[0].Y) / sourceBounds.Width;
            var m21 = (destination[2].X - destination[0].X) / sourceBounds.Height;
            var m22 = (destination[2].Y - destination[0].Y) / sourceBounds.Height;
            using var transform = new Matrix(
                m11, m12, m21, m22,
                destination[0].X - m11 * sourceBounds.X - m21 * sourceBounds.Y,
                destination[0].Y - m12 * sourceBounds.X - m22 * sourceBounds.Y);
            graphics.Transform = transform;
            sourceBounds.Inflate(3f, 3f);
            graphics.DrawImage(source, sourceBounds, sourceBounds, GraphicsUnit.Pixel);
        }
        finally
        {
            graphics.Restore(state);
        }
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
                using var mask = CreateMouseMask();
                background = source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);
                mouseArm = Extract(source, mask);
                RepairMouseHole(background, source, mask);
                return new MouseLayers(background, mouseArm);
            }
            catch
            {
                background?.Dispose();
                mouseArm?.Dispose();
                throw;
            }
        }

        private static GraphicsPath CreateMouseMask() => Polygon(
            (0.342f, 0.512f), (0.430f, 0.512f), (0.430f, 0.613f), (0.395f, 0.620f),
            (0.372f, 0.637f), (0.348f, 0.659f), (0.326f, 0.680f), (0.328f, 0.698f),
            (0.321f, 0.724f), (0.303f, 0.748f), (0.279f, 0.769f), (0.251f, 0.779f),
            (0.225f, 0.779f), (0.207f, 0.767f), (0.198f, 0.750f), (0.195f, 0.730f),
            (0.191f, 0.705f), (0.202f, 0.680f), (0.222f, 0.657f), (0.233f, 0.634f),
            (0.246f, 0.609f), (0.267f, 0.585f), (0.295f, 0.550f));

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

        private static void RepairMouseHole(Bitmap background, Bitmap source, GraphicsPath mask)
        {
            using var graphics = Graphics.FromImage(background);
            graphics.SetClip(mask);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            using var transparent = new SolidBrush(Color.Transparent);
            graphics.FillRectangle(transparent, new Rectangle(Point.Empty, background.Size));

            using var pad = new SolidBrush(source.GetPixel(108, 336));
            using var padRegion = Polygon((0.133f, 0.573f), (0.437f, 0.654f),
                (0.444f, 0.674f), (0.295f, 0.861f), (0.270f, 0.870f),
                (0f, 0.719f), (0f, 0.682f));
            graphics.FillPath(pad, padRegion);

            using var deskPen = new Pen(source.GetPixel(120, 253), 6f);
            using var padPen = new Pen(source.GetPixel(160, 265), 3.5f);
            graphics.DrawLine(deskPen, At(0f, 0.514f), At(1f, 0.773f));
            graphics.DrawLine(padPen, At(0.133f, 0.573f), At(0.437f, 0.654f));
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
