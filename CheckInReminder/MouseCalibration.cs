using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CheckInReminder;

public readonly record struct CalibrationPoint(float X, float Y);

public sealed class MouseCalibration
{
    public CustomMouseRig Rig { get; set; } = new();
    public CalibrationPoint[] Pad { get; set; } = [];
    public bool RemoveConnectedPadColor { get; set; } = true;
    public void Validate()
    {
        if (Rig is null || Pad is null || Pad.Length is < 3 or > 128 || Pad.Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < 0 || p.X > 600 || p.Y < 0 || p.Y > 448))
            throw new InvalidDataException("请圈出鼠标垫范围，再确认手臂连接位置。");
        Rig.Validate();
    }
}

internal sealed class CalibratedMouseLayers : IDisposable
{
    private readonly Bitmap original;
    private readonly Bitmap background;
    private readonly Bitmap arm;
    private readonly byte[] pixels;
    private readonly CustomMouseRig rig;
    private readonly Rectangle bounds;
    private CalibratedMouseLayers(Bitmap original, Bitmap background, Bitmap arm, CustomMouseRig rig)
    {
        this.original = original; this.background = background; this.arm = arm; this.rig = rig;
        pixels = Read(arm);
        var minX = 600; var minY = 448; var maxX = 0; var maxY = 0;
        for (var y = 0; y < 448; y++) for (var x = 0; x < 600; x++)
            if (pixels[(y * 600 + x) * 4 + 3] > 0) { minX = Math.Min(x, minX); maxX = Math.Max(x, maxX); minY = Math.Min(y, minY); maxY = Math.Max(y, maxY); }
        bounds = Rectangle.Intersect(new Rectangle(0, 0, 600, 448), Rectangle.FromLTRB(minX - 10, minY - 10, maxX + 11, maxY + 11));
    }

    public static CalibratedMouseLayers Create(Bitmap source, Bitmap selection, MouseCalibration spec)
    {
        spec.Validate();
        if (source.Size != new Size(600, 448) || selection.Size != source.Size) throw new InvalidDataException("校准图片需要使用相同的 600×448 画布。");
        var input = Read(source); var mask = Read(selection); var chosen = new bool[600 * 448];
        using var pad = new GraphicsPath(); pad.AddPolygon(spec.Pad.Select(p => new PointF(p.X, p.Y)).ToArray());
        var inPad = new bool[chosen.Length]; var samples = new List<int>();
        for (var y = 0; y < 448; y++) for (var x = 0; x < 600; x++)
        {
            var i = y * 600 + x; var o = i * 4; chosen[i] = mask[o + 3] > 127;
            inPad[i] = pad.IsVisible(x + .5f, y + .5f);
            var lo = Math.Min(input[o], Math.Min(input[o + 1], input[o + 2])); var hi = Math.Max(input[o], Math.Max(input[o + 1], input[o + 2]));
            if (inPad[i] && !chosen[i] && input[o + 3] > 240 && hi - lo < 20 && lo > 60 && hi < 230) samples.Add((input[o] + input[o + 1] + input[o + 2]) / 3);
        }
        if (samples.Count < 100) throw new InvalidDataException("鼠标垫内没有足够的纯灰色区域，请重新圈出鼠标垫，并给选区外留出一些空白垫面。");
        samples.Sort(); var gray = samples[samples.Count / 2];
        // Remove only pad-colored pixels connected to the selection boundary.
        // Gray artwork enclosed by a hand/mouse outline is preserved.
        var removed = new bool[chosen.Length]; var queue = new Queue<int>();
        bool IsPadColor(int i)
        {
            var o = i * 4;
            return inPad[i] && input[o + 3] > 240 && Math.Abs(input[o] - gray) < 28 && Math.Abs(input[o + 1] - gray) < 28 && Math.Abs(input[o + 2] - gray) < 28;
        }
        for (var y = 1; y < 447; y++) for (var x = 1; x < 599; x++)
        {
            var i = y * 600 + x;
            if (spec.RemoveConnectedPadColor && chosen[i] && IsPadColor(i) && (!chosen[i - 1] || !chosen[i + 1] || !chosen[i - 600] || !chosen[i + 600])) { removed[i] = true; queue.Enqueue(i); }
        }
        while (queue.TryDequeue(out var i))
            foreach (var n in new[] { i - 1, i + 1, i - 600, i + 600 })
                if (n >= 0 && n < chosen.Length && !removed[n] && chosen[n] && IsPadColor(n)) { removed[n] = true; queue.Enqueue(n); }
        var moving = new byte[input.Length]; var bottom = (byte[])input.Clone();
        var sx = spec.Rig.ShoulderX * 600; var sy = spec.Rig.ShoulderY * 448;
        var ax = spec.Rig.MouseX * 600 - sx; var ay = spec.Rig.MouseY * 448 - sy;
        var count = 0;
        for (var y = 0; y < 448; y++) for (var x = 0; x < 600; x++)
        {
            var i = y * 600 + x; var o = i * 4;
            if (!chosen[i] || removed[i] || input[o + 3] == 0) continue;
            if ((x - sx) * ax + (y - sy) * ay <= 0) continue;
            Array.Copy(input, o, moving, o, 4); count++;
            if (inPad[i]) { bottom[o] = bottom[o + 1] = bottom[o + 2] = (byte)gray; bottom[o + 3] = 255; }
            else Array.Clear(bottom, o, 4);
        }
        if (count < 50) throw new InvalidDataException("还没有选中手和鼠标，请圈出需要移动的部分。");
        return new(new Bitmap(source), FromBytes(bottom), FromBytes(moving), spec.Rig);
    }

    public void Save(string folder, string key)
    {
        original.Save(Path.Combine(folder, "original-" + key + ".png"), ImageFormat.Png);
        background.Save(Path.Combine(folder, key + ".png"), ImageFormat.Png);
        arm.Save(Path.Combine(folder, "arm-" + key + ".png"), ImageFormat.Png);
    }
    public static CalibratedMouseLayers Load(string folder, string key, MouseCalibration spec)
    {
        spec.Validate();
        Bitmap? original = null, background = null, arm = null;
        try
        {
            original = CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, "original-" + key + ".png"));
            background = CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, key + ".png"));
            arm = CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, "arm-" + key + ".png"));
            if (new[] { original, background, arm }.Any(b => b.Size != new Size(600, 448))) throw new InvalidDataException("鼠标图层画布不一致。");
            return new(original, background, arm, spec.Rig);
        }
        catch { original?.Dispose(); background?.Dispose(); arm?.Dispose(); throw; }
    }
    public Bitmap Render(DesktopPetRigPose pose)
    {
        var dx = Math.Clamp(pose.MouseOffset.X, -1, 1) * 6;
        var dy = Math.Clamp(pose.MouseOffset.Y, -1, 1) * 4 + Math.Clamp(pose.MousePress, 0, 1) * 3;
        if (dx == 0 && dy == 0) return new Bitmap(original);
        var sx = rig.ShoulderX * 600; var sy = rig.ShoulderY * 448;
        var ax = rig.MouseX * 600 - sx; var ay = rig.MouseY * 448 - sy;
        var denominator = ax * ax + ay * ay + dx * ax + dy * ay;
        var bytes = new byte[pixels.Length];
        for (var y = bounds.Top; y < bounds.Bottom; y++) for (var x = bounds.Left; x < bounds.Right; x++)
        {
            var weight = Math.Clamp(((x - sx) * ax + (y - sy) * ay) / denominator, 0, 1);
            var px = x - dx * weight; var py = y - dy * weight;
            var ix = (int)MathF.Floor(px); var iy = (int)MathF.Floor(py); var fx = px - ix; var fy = py - iy;
            for (var c = 0; c < 4; c++)
            {
                float Get(int xx, int yy) => xx < 0 || xx >= 600 || yy < 0 || yy >= 448 ? 0 : pixels[(yy * 600 + xx) * 4 + c];
                bytes[(y * 600 + x) * 4 + c] = (byte)Math.Clamp((int)MathF.Round((Get(ix, iy) * (1 - fx) + Get(ix + 1, iy) * fx) * (1 - fy) + (Get(ix, iy + 1) * (1 - fx) + Get(ix + 1, iy + 1) * fx) * fy), 0, 255);
            }
        }
        var result = new Bitmap(background);
        using var warped = FromBytes(bytes); using var g = Graphics.FromImage(result);
        g.SetClip(bounds); g.DrawImageUnscaled(warped, 0, 0); return result;
    }
    private static byte[] Read(Bitmap image)
    {
        using var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(copy)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(image, 0, 0); }
        var data = copy.LockBits(new Rectangle(Point.Empty, copy.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try { var result = new byte[copy.Width * copy.Height * 4]; Marshal.Copy(data.Scan0, result, 0, result.Length); return result; }
        finally { copy.UnlockBits(data); }
    }
    private static Bitmap FromBytes(byte[] bytes)
    {
        var b = new Bitmap(600, 448, PixelFormat.Format32bppPArgb);
        var data = b.LockBits(new Rectangle(0, 0, 600, 448), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try { Marshal.Copy(bytes, 0, data.Scan0, bytes.Length); } finally { b.UnlockBits(data); }
        return b;
    }
    public void Dispose() { original.Dispose(); background.Dispose(); arm.Dispose(); }
}
