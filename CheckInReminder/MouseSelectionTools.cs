using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CheckInReminder;

/// <summary>Local, bounded color selection. No model or network dependency.</summary>
internal static class MouseSelectionTools
{
    private const int Width = 600, Height = 448;
    internal static bool TrySelectPad(Bitmap source, MouseCalibration spec, Point? seed)
    {
        if (source.Size != new Size(Width, Height)) throw new InvalidDataException("图片需要使用 600×448 画布。");
        var pixels = Read(source); var visited = new bool[Width * Height];
        bool Gray(Color c) => c.A > 240 && Spread(c) < 24 && Min(c) > 65 && Max(c) < 232;
        List<int> Flood(Point start)
        {
            if (start.X < 0 || start.Y < 0 || start.X >= Width || start.Y >= Height) return [];
            var index = start.Y * Width + start.X; var color = Get(pixels, index);
            if (visited[index] || !Gray(color)) return [];
            var queue = new Queue<int>(); var component = new List<int>(); queue.Enqueue(index); visited[index] = true;
            while (queue.TryDequeue(out var i))
            {
                component.Add(i);
                foreach (var n in Neighbors(i))
                {
                    if (visited[n]) continue;
                    var c = Get(pixels, n);
                    if (!Gray(c) || Math.Abs(c.R - color.R) > 26 || Math.Abs(c.G - color.G) > 26 || Math.Abs(c.B - color.B) > 26) continue;
                    visited[n] = true; queue.Enqueue(n);
                }
            }
            return component;
        }
        List<int> selected = [];
        if (seed is { } point) selected = Flood(point);
        else
        {
            // Search each image, not a shared template rectangle. Isolated gray key
            // caps are too small; large body/background regions are not a pad.
            double best = 0;
            for (var y = 200; y < Height; y += 6) for (var x = 0; x < 440; x += 6)
            {
                var component = Flood(new Point(x, y)); if (component.Count < 350 || component.Count > 60000) continue;
                var minY = component.Min(i => i / Width); var maxY = component.Max(i => i / Width);
                if (maxY - minY > 220) continue;
                var cx = component.Average(i => i % Width); var cy = component.Average(i => i / Width);
                var distance = Math.Pow(cx - spec.Rig.MouseX * Width, 2) + Math.Pow(cy - spec.Rig.MouseY * Height, 2);
                var score = component.Count / (1 + distance / 20000);
                if (score > best) { selected = component; best = score; }
            }
        }
        if (selected.Count < 350) return false;
        // A convex mouse pad can have hand/mouse holes. Its interior color's hull
        // restores the covered area while leaving the dark exterior border out.
        var points = selected.Select(i => new Point(i % Width, i / Width)).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        static long Cross(Point a, Point b, Point c) => (long)(b.X - a.X) * (c.Y - a.Y) - (long)(b.Y - a.Y) * (c.X - a.X);
        var hull = new List<Point>();
        foreach (var p in points) { while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        var lower = hull.Count;
        for (var i = points.Length - 2; i >= 0; i--) { var p = points[i]; while (hull.Count > lower && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
        if (hull.Count is < 3 or > 128) return false;
        spec.Pad = hull.Select(p => new CalibrationPoint(p.X, p.Y)).ToArray();
        return true;
    }
    internal static bool SelectConnected(Bitmap source, Bitmap mask, MouseCalibration spec, Point seed)
    {
        Validate(source, mask, spec);
        if (seed.X < 0 || seed.Y < 0 || seed.X >= Width || seed.Y >= Height) return false;
        var pixels = Read(source); var selected = Read(mask); var start = seed.Y * Width + seed.X;
        var region = new RegionRules(spec, pixels);
        var color = Get(pixels, start);
        if (color.A < 40 || !region.Allowed(seed.X, seed.Y) || region.IsPad(seed.X, seed.Y, color)) return false;
        var chromatic = Spread(color) > 35;
        bool Matches(int i)
        {
            var x = i % Width; var y = i / Width; var c = Get(pixels, i);
            if (c.A < 40 || !region.Allowed(x, y) || region.IsPad(x, y, c)) return false;
            if (chromatic)
            {
                var hue = Math.Abs(c.GetHue() - color.GetHue()); hue = Math.Min(hue, 360 - hue);
                return Spread(c) > 28 && hue < 30 && c.GetSaturation() > .23f;
            }
            if (Min(color) > 195) return Spread(c) < 45 && Min(c) > Math.Max(175, region.PadGray + 25);
            return Math.Abs(c.R - color.R) < 42 && Math.Abs(c.G - color.G) < 42 && Math.Abs(c.B - color.B) < 42;
        }
        var visited = new bool[Width * Height]; var queue = new Queue<int>(); var component = new List<int>();
        queue.Enqueue(start); visited[start] = true;
        while (queue.TryDequeue(out var i))
        {
            if (!Matches(i)) continue;
            component.Add(i);
            foreach (var n in Neighbors(i)) if (!visited[n]) { visited[n] = true; queue.Enqueue(n); }
        }
        if (component.Count < 3) return false;
        var changed = false;
        foreach (var i in component)
        {
            if (selected[i * 4 + 3] < 255) changed = true;
            Set(selected, i);
        }
        Write(mask, selected);
        changed |= CompleteDarkEdges(source, mask, spec) > 0;
        return changed;
    }

    internal static int CompleteDarkEdges(Bitmap source, Bitmap mask, MouseCalibration spec)
    {
        Validate(source, mask, spec);
        var pixels = Read(source); var selection = Read(mask); var region = new RegionRules(spec, pixels);
        // Fixed distance from selected light/color artwork, never from previously
        // added dark pixels. Repeating the command therefore cannot creep outward.
        var distance = Enumerable.Repeat(-1, Width * Height).ToArray(); var queue = new Queue<int>();
        for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
        {
            var i = y * Width + x; var c = Get(pixels, i);
            if (selection[i * 4 + 3] > 127 && c.A > 80 && region.Allowed(x, y)
                && !region.IsPad(x, y, c) && (Min(c) > 195 || Spread(c) >= 40))
            { distance[i] = 0; queue.Enqueue(i); }
        }
        while (queue.TryDequeue(out var i))
        {
            if (distance[i] >= 6) continue;
            foreach (var n in Neighbors(i)) if (distance[n] < 0) { distance[n] = distance[i] + 1; queue.Enqueue(n); }
        }
        var added = 0;
        for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
        {
            var i = y * Width + x; var c = Get(pixels, i);
            if (selection[i * 4 + 3] > 127 || distance[i] is < 1 or > 6 || c.A < 16 || !region.Allowed(x, y) || region.NearPadBorder(x, y)) continue;
            // Preserve the mouse's dark outline and its dark antialiasing fringe,
            // not the surrounding gray pad or arbitrary colored nearby artwork.
            if (Spread(c) < 40 && Max(c) < Math.Min(195, Math.Max(85, region.PadGray - 10))) { Set(selection, i); added++; }
        }
        if (added > 0) Write(mask, selection);
        return added;
    }

    private sealed class RegionRules
    {
        private readonly MouseCalibration spec;
        private readonly float sx, sy, mx, my, ax, ay, lengthSquared;
        public int PadGray { get; }
        public RegionRules(MouseCalibration spec, byte[] pixels)
        {
            this.spec = spec;
            sx = spec.Rig.ShoulderX * Width; sy = spec.Rig.ShoulderY * Height;
            mx = spec.Rig.MouseX * Width; my = spec.Rig.MouseY * Height;
            ax = mx - sx; ay = my - sy; lengthSquared = ax * ax + ay * ay;
            var tones = new List<int>();
            using var pad = new GraphicsPath(); pad.AddPolygon(spec.Pad.Select(p => new PointF(p.X, p.Y)).ToArray());
            for (var y = 0; y < Height; y += 3) for (var x = 0; x < Width; x += 3)
            {
                var c = Get(pixels, y * Width + x);
                if (pad.IsVisible(x, y) && c.A > 240 && Spread(c) < 20 && Min(c) > 70 && Max(c) < 225) tones.Add((c.R + c.G + c.B) / 3);
            }
            tones.Sort(); PadGray = tones.Count > 0 ? tones[tones.Count / 2] : 165;
        }
        public bool Allowed(int x, int y)
        {
            var projection = (x - sx) * ax + (y - sy) * ay;
            var side = Math.Abs((x - sx) * ay - (y - sy) * ax) / MathF.Sqrt(lengthSquared);
            return projection >= -.08f * lengthSquared && projection <= 1.7f * lengthSquared && side < Math.Max(58, MathF.Sqrt(lengthSquared) * .5f);
        }
        public bool IsPad(int x, int y, Color c) => spec.RemoveConnectedPadColor && Spread(c) < 22
            && Math.Abs((c.R + c.G + c.B) / 3 - PadGray) < 25;
        public bool NearPadBorder(int x, int y)
        {
            for (var i = 0; i < spec.Pad.Length; i++)
            {
                var a = spec.Pad[i]; var b = spec.Pad[(i + 1) % spec.Pad.Length]; var dx = b.X - a.X; var dy = b.Y - a.Y;
                var length = dx * dx + dy * dy; var t = length == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / length, 0, 1);
                var px = x - a.X - t * dx; var py = y - a.Y - t * dy;
                if (px * px + py * py < 16) return true;
            }
            return false;
        }
    }
    private static IEnumerable<int> Neighbors(int i)
    {
        if (i % Width > 0) yield return i - 1;
        if (i % Width < Width - 1) yield return i + 1;
        if (i >= Width) yield return i - Width;
        if (i < Width * (Height - 1)) yield return i + Width;
    }
    private static int Min(Color c) => Math.Min(c.R, Math.Min(c.G, c.B));
    private static int Max(Color c) => Math.Max(c.R, Math.Max(c.G, c.B));
    private static int Spread(Color c) => Max(c) - Min(c);
    private static Color Get(byte[] p, int i) { var o = i * 4; return Color.FromArgb(p[o + 3], p[o + 2], p[o + 1], p[o]); }
    private static void Set(byte[] p, int i) { var o = i * 4; p[o] = p[o + 1] = p[o + 2] = p[o + 3] = 255; }
    private static void Validate(Bitmap source, Bitmap mask, MouseCalibration spec)
    {
        spec.Validate();
        if (source.Size != new Size(Width, Height) || mask.Size != source.Size) throw new InvalidDataException("图片与选区大小不一致。");
    }
    private static byte[] Read(Bitmap source)
    {
        using var image = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(source, 0, 0); }
        var data = image.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { var bytes = new byte[Width * Height * 4]; Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); return bytes; }
        finally { image.UnlockBits(data); }
    }
    private static void Write(Bitmap target, byte[] bytes)
    {
        var data = target.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(bytes, 0, data.Scan0, bytes.Length); } finally { target.UnlockBits(data); }
    }
}
