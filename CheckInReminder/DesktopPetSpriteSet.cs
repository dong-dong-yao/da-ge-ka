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
    private bool disposed;

    private DesktopPetSpriteSet(Bitmap idle, Bitmap left, Bitmap? center, Bitmap right)
    {
        this.idle = idle;
        this.left = left;
        this.center = center;
        this.right = right;
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
        var source = pose.KeyboardContact
            ? GetPressed(PetKeyboardPoseMapper.Map(pose.KeyboardTarget.X, HasCenterPose))
            : Idle;
        return RenderMouseMotion(source, pose);
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

    private static Bitmap RenderMouseMotion(Bitmap source, DesktopPetRigPose pose)
    {
        var result = source.Clone(
            new Rectangle(Point.Empty, source.Size),
            PixelFormat.Format32bppPArgb);
        var dx = Math.Clamp(pose.MouseOffset.X, -1f, 1f) * 12f;
        var dy = Math.Clamp(pose.MouseOffset.Y, -1f, 1f) * 8f
            + Math.Clamp(pose.MousePress, 0f, 1f) * 5f;
        var angle = Math.Clamp(pose.MouseRotationDegrees, -2.5f, 2.5f);
        if (Math.Abs(dx) < 0.01f && Math.Abs(dy) < 0.01f && Math.Abs(angle) < 0.01f)
            return result;

        var region = new Rectangle(0, 128, 352, 320);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var transparent = new SolidBrush(Color.Transparent))
            graphics.FillRectangle(transparent, region);

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
            var edgeWeight = Math.Min(1f, Math.Min(
                Math.Min((point.X - region.Left) / 32f, (region.Right - point.X) / 32f),
                Math.Min((point.Y - region.Top) / 32f, (region.Bottom - point.Y) / 32f)));
            var weight = Math.Clamp(armWeight * Math.Max(0f, edgeWeight), 0f, 1f);

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
            DrawTriangle(graphics, source, sourceCell,
                [topLeft, topRight, bottomLeft], [topLeft, topRight, bottomLeft]);
            var opposite = new PointF(
                topRight.X + bottomLeft.X - bottomRight.X,
                topRight.Y + bottomLeft.Y - bottomRight.Y);
            DrawTriangle(graphics, source, sourceCell,
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

    private Bitmap GetLive(Bitmap frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return frame;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        idle.Dispose();
        left.Dispose();
        center?.Dispose();
        right.Dispose();
    }
}
