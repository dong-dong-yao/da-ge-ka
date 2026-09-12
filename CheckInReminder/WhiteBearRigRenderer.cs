using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckInReminder;

/// <summary>Deterministic, source-pixel-only rig for the user's white-bear artwork.</summary>
public sealed class WhiteBearRigRenderer : IDisposable
{
    private const string WhiteBearResource = "CheckInReminder.Assets.DesktopPet.white-bear-typing.jpg";
    private const int RenderWidth = 600;
    private const int RenderHeight = 448;
    private readonly Bitmap baseLayer;
    private readonly Bitmap mouseArm;
    private readonly Bitmap keyboardArm;
    private readonly KeyboardPressSprites keyboardPressSprites;
    private bool disposed;

    private WhiteBearRigRenderer(Bitmap baseLayer, Bitmap mouseArm, Bitmap keyboardArm, KeyboardPressSprites keyboardPressSprites)
    {
        this.baseLayer = baseLayer;
        this.mouseArm = mouseArm;
        this.keyboardArm = keyboardArm;
        this.keyboardPressSprites = keyboardPressSprites;
    }

    public Size FrameSize => new(RenderWidth, RenderHeight);

    public static WhiteBearRigRenderer Load()
    {
        using var artwork = LoadArtwork();
        return Create(artwork);
    }

    /// <summary>Returns owned transparent source artwork before any rig layer extraction.</summary>
    internal static Bitmap LoadArtwork()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(WhiteBearResource)
            ?? throw new InvalidOperationException($"缺少桌宠原图资源：{WhiteBearResource}");
        using var source = Image.FromStream(stream);
        var artwork = ResizeToArgb(source);
        try
        {
            RemoveConnectedWhiteBackground(artwork);
            return artwork;
        }
        catch
        {
            artwork.Dispose();
            throw;
        }
    }

    /// <summary>Builds owned layers; the caller retains ownership of the source artwork.</summary>
    internal static WhiteBearRigRenderer Create(Bitmap artwork)
    {
        Bitmap? background = null;
        Bitmap? mouse = null;
        Bitmap? keyboard = null;
        KeyboardPressSprites? pressSprites = null;
        try
        {
            background = artwork.Clone(new Rectangle(Point.Empty, artwork.Size), PixelFormat.Format32bppPArgb);
            using var mouseMask = CreateMouseMask();
            using var keyboardMask = CreateKeyboardMask(artwork);
            mouse = Extract(artwork, mouseMask);
            keyboard = Extract(artwork, keyboardMask);
            RepairMouseHole(background, artwork, mouseMask);
            RepairKeyboardHole(background, keyboardMask);
            pressSprites = KeyboardPressSprites.Load();
            return new WhiteBearRigRenderer(background, mouse, keyboard, pressSprites);
        }
        catch
        {
            background?.Dispose();
            mouse?.Dispose();
            keyboard?.Dispose();
            pressSprites?.Dispose();
            throw;
        }
    }

    /// <summary>Returns a newly allocated frame. The caller owns and must dispose it.</summary>
    public Bitmap Render(DesktopPetRigPose pose)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var frame = NewLayer();
        try
        {
            using var graphics = Graphics.FromImage(frame);
            ConfigureGraphics(graphics);
            graphics.Clear(Color.Transparent);
            graphics.DrawImageUnscaled(baseLayer, Point.Empty);

            DrawMouseArm(graphics,
                Math.Clamp(pose.MouseOffset.X, -1f, 1f) * 12f,
                Math.Clamp(pose.MouseOffset.Y, -1f, 1f) * 8f + Math.Clamp(pose.MousePress, 0f, 1f) * 5f,
                Math.Clamp(pose.MouseRotationDegrees, -2.5f, 2.5f));

            if (!pose.KeyboardContact)
                graphics.DrawImageUnscaled(keyboardArm, Point.Empty);
            else
                keyboardPressSprites.Draw(graphics, pose.KeyboardTarget, pose.KeyboardPress);
            return frame;
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    private void DrawMouseArm(Graphics graphics, float dx, float dy, float angle)
        => DrawPinnedMouseArm(graphics, mouseArm, dx, dy, angle,
            new PointF(211, 230), new PointF(232, 273), At(0.31f, 0.48f),
            new Rectangle(112, 224, 160, 128));

    internal static void DrawPinnedMouseArm(Graphics graphics, Bitmap mouseArm,
        float dx, float dy, float angle, PointF seamStart, PointF seamEnd,
        PointF pivot, Rectangle meshBounds)
    {
        if (dx == 0f && dy == 0f && angle == 0f)
        {
            graphics.DrawImageUnscaled(mouseArm, Point.Empty);
            return;
        }

        var radians = angle * Math.PI / 180d;
        var cosine = (float)Math.Cos(radians);
        var sine = (float)Math.Sin(radians);
        PointF Deform(PointF point)
        {
            // Pin the shoulder cut from (211,230) to (232,273). The original
            // paw/mouse reach full motion 63 px from this line; no new pixels.
            var weight = Math.Clamp(((seamStart.X - point.X) * (seamEnd.Y - seamStart.Y)
                + (point.Y - seamStart.Y) * (seamEnd.X - seamStart.X)) / 3000f, 0f, 1f);
            var x = point.X - pivot.X;
            var y = point.Y - pivot.Y;
            var moved = new PointF(pivot.X + x * cosine - y * sine + dx,
                pivot.Y + x * sine + y * cosine + dy);
            return Interpolate(point, moved, weight);
        }

        // A small source-pixel mesh preserves the anchored seam while allowing
        // the original mouse to translate and tilt. Each quad is two affine triangles.
        for (var y = meshBounds.Top; y < meshBounds.Bottom; y += 16)
        for (var x = meshBounds.Left; x < meshBounds.Right; x += 16)
        {
            var source = new RectangleF(x, y, 16, 16);
            var topLeft = Deform(new PointF(x, y));
            var topRight = Deform(new PointF(x + 16, y));
            var bottomLeft = Deform(new PointF(x, y + 16));
            var bottomRight = Deform(new PointF(x + 16, y + 16));
            DrawTriangle(graphics, mouseArm, source, [topLeft, topRight, bottomLeft], [topLeft, topRight, bottomLeft]);
            var opposite = new PointF(topRight.X + bottomLeft.X - bottomRight.X,
                topRight.Y + bottomLeft.Y - bottomRight.Y);
            DrawTriangle(graphics, mouseArm, source, [opposite, topRight, bottomLeft], [topRight, bottomRight, bottomLeft]);
        }
    }

    private static void DrawTriangle(Graphics graphics, Bitmap mouseArm, RectangleF source, PointF[] destination, PointF[] triangle)
    {
        var state = graphics.Save();
        try
        {
            using var clip = new GraphicsPath();
            clip.AddPolygon(triangle);
            graphics.SetClip(clip, CombineMode.Intersect);
            var m11 = (destination[1].X - destination[0].X) / source.Width;
            var m12 = (destination[1].Y - destination[0].Y) / source.Width;
            var m21 = (destination[2].X - destination[0].X) / source.Height;
            var m22 = (destination[2].Y - destination[0].Y) / source.Height;
            using var transform = new Matrix(m11, m12, m21, m22,
                destination[0].X - m11 * source.X - m21 * source.Y,
                destination[0].Y - m12 * source.X - m22 * source.Y);
            graphics.Transform = transform;
            // Keep bicubic filter neighbors beyond all cell edges. The triangle
            // clip owns coverage; limiting only the sampling rectangle avoids
            // resampling the entire 600x448 layer for each of the 160 triangles.
            source.Inflate(4f, 4f);
            graphics.DrawImage(mouseArm, source, source, GraphicsUnit.Pixel);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static PointF Interpolate(PointF first, PointF second, float amount) =>
        new(first.X + (second.X - first.X) * amount, first.Y + (second.Y - first.Y) * amount);

    private static GraphicsPath CreateMouseMask() => Polygon(
        // Source-calibrated outline: the full original paw AND its occluded mouse.
        // The initial brief's y <= .62 example truncated the paw at y ~= .70.
        (0.342f, 0.512f), (0.430f, 0.512f), (0.430f, 0.613f), (0.395f, 0.620f),
        (0.372f, 0.637f), (0.348f, 0.659f), (0.326f, 0.680f), (0.328f, 0.698f),
        (0.321f, 0.724f), (0.303f, 0.748f), (0.279f, 0.769f), (0.251f, 0.779f),
        (0.225f, 0.779f), (0.207f, 0.767f), (0.198f, 0.750f), (0.195f, 0.730f),
        (0.191f, 0.705f), (0.202f, 0.680f), (0.222f, 0.657f), (0.233f, 0.634f),
        (0.246f, 0.609f), (0.267f, 0.585f), (0.295f, 0.550f));

    private static GraphicsPath CreateKeyboardMask(Bitmap source)
    {
        // The original arm is a separate dark connected component. At this exact
        // resolution, 180 separates it from the body; 220 merges their JPEG fringes.
        var component = new HashSet<Point>();
        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(407, 184));
        while (queue.TryDequeue(out var point))
        {
            if (point.X < 375 || point.X >= 470 || point.Y < 175 || point.Y >= 280
                || !visited.Add(point) || source.GetPixel(point.X, point.Y).R >= 180) continue;
            component.Add(point);
            queue.Enqueue(new Point(point.X - 1, point.Y));
            queue.Enqueue(new Point(point.X + 1, point.Y));
            queue.Enqueue(new Point(point.X, point.Y - 1));
            queue.Enqueue(new Point(point.X, point.Y + 1));
        }

        // A one-pixel source fringe includes the original antialiasing. The row
        // envelope closes the existing white arm interior without capturing the body.
        var left = Enumerable.Repeat(RenderWidth, RenderHeight).ToArray();
        var right = Enumerable.Repeat(-1, RenderHeight).ToArray();
        foreach (var point in component)
        for (var y = point.Y - 1; y <= point.Y + 1; y++)
        {
            left[y] = Math.Min(left[y], point.X - 1);
            right[y] = Math.Max(right[y], point.X + 1);
        }
        var top = component.Min(point => point.Y) - 1;
        var bottom = component.Max(point => point.Y) + 1;
        var rightTip = component.Where(point => point.X > 430).OrderByDescending(point => point.Y).First();
        for (var y = rightTip.Y; y <= bottom; y++)
        {
            var amount = (float)(y - rightTip.Y) / (bottom - rightTip.Y);
            right[y] = Math.Max(right[y], (int)Math.Ceiling(rightTip.X + 1
                + (right[bottom] - rightTip.X - 1) * amount));
        }
        var outline = new List<PointF>();
        for (var y = top; y <= bottom; y++)
        {
            outline.Add(new PointF(left[y], y));
            outline.Add(new PointF(left[y], y + 1));
        }
        for (var y = bottom; y >= top; y--)
        {
            outline.Add(new PointF(right[y] + 1, y + 1));
            outline.Add(new PointF(right[y] + 1, y));
        }
        var path = new GraphicsPath();
        path.AddPolygon(outline.ToArray());
        return path;
    }

    private static GraphicsPath Polygon(params (float X, float Y)[] points)
    {
        var path = new GraphicsPath();
        path.AddPolygon(points.Select(point => At(point.X, point.Y)).ToArray());
        return path;
    }

    private static Bitmap Extract(Bitmap source, GraphicsPath mask)
    {
        var result = NewLayer();
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.Clear(Color.Transparent);
            graphics.SetClip(mask);
            graphics.DrawImageUnscaled(source, Point.Empty);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static void RepairMouseHole(Bitmap background, Bitmap source, GraphicsPath mask)
    {
        using var graphics = Graphics.FromImage(background);
        graphics.SetClip(mask);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        using var transparent = new SolidBrush(Color.Transparent);
        graphics.FillRectangle(transparent, new Rectangle(Point.Empty, background.Size));

        // Continue only existing desk/pad surfaces and their two original boundary segments.
        using var desk = new SolidBrush(Color.White);
        using var pad = new SolidBrush(source.GetPixel(108, 336)); // Original (0.18, 0.75).
        using var deskRegion = Polygon((0f, 0.514f), (1f, 0.773f), (1f, 1f), (0f, 1f));
        using var padRegion = Polygon((0.133f, 0.573f), (0.437f, 0.654f),
            (0.444f, 0.674f), (0.295f, 0.861f), (0.270f, 0.870f), (0f, 0.719f), (0f, 0.682f));
        graphics.FillPath(desk, deskRegion);
        graphics.FillPath(pad, padRegion);
        using var deskPen = new Pen(source.GetPixel(120, 253), 6f);
        using var padPen = new Pen(source.GetPixel(160, 265), 3.5f);
        graphics.DrawLine(deskPen, At(0f, 0.514f), At(1f, 0.773f));
        graphics.DrawLine(padPen, At(0.133f, 0.573f), At(0.437f, 0.654f));
        using var innerShoulder = Polygon((0.351f, 0.512f), (0.430f, 0.512f),
            (0.430f, 0.613f), (0.351f, 0.605f));
        graphics.FillPath(desk, innerShoulder);
    }

    private static void RepairKeyboardHole(Bitmap background, GraphicsPath mask)
    {
        using var graphics = Graphics.FromImage(background);
        graphics.SetClip(mask);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        using var white = new SolidBrush(Color.White);
        graphics.FillRectangle(white, new Rectangle(Point.Empty, background.Size));
    }

    private static PointF At(float x, float y) => new(x * RenderWidth, y * RenderHeight);
    private static Bitmap NewLayer() => new(RenderWidth, RenderHeight, PixelFormat.Format32bppPArgb);

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    private static Bitmap ResizeToArgb(Image source)
    {
        var result = NewLayer();
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.Clear(Color.White);
            ConfigureGraphics(graphics);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(source, new Rectangle(0, 0, RenderWidth, RenderHeight));
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static void RemoveConnectedWhiteBackground(Bitmap bitmap)
    {
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
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
                var pixelIndex = (y * bitmap.Width) + x;
                if (visited[pixelIndex]) return;
                visited[pixelIndex] = true;
                var offset = (y * data.Stride) + (x * 4);
                var alpha = bytes[offset + 3];
                if (alpha == 0 || (bytes[offset] * 255) < (alpha * 240)
                    || (bytes[offset + 1] * 255) < (alpha * 240)
                    || (bytes[offset + 2] * 255) < (alpha * 240)) return;

                // Transparent PArgb pixels must have zero color channels as well as alpha.
                bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = bytes[offset + 3] = 0;
                queue[tail++] = pixelIndex;
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
                var pixelIndex = queue[head++];
                var x = pixelIndex % bitmap.Width;
                var y = pixelIndex / bitmap.Width;
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

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        baseLayer.Dispose();
        mouseArm.Dispose();
        keyboardArm.Dispose();
        keyboardPressSprites.Dispose();
    }
}
