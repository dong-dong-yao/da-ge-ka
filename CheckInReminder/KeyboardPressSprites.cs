using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckInReminder;

/// <summary>Owned right-arm cutouts from the three exact user PNGs; never whole frames.</summary>
internal sealed class KeyboardPressSprites : IDisposable
{
    private const int Width = 600;
    private const int Height = 448;
    private const int PinnedThroughY = 304;
    private readonly Bitmap[] arms;

    private KeyboardPressSprites(Bitmap[] arms) => this.arms = arms;

    internal static KeyboardPressSprites Load()
    {
        var loaded = new List<Bitmap>();
        try
        {
            foreach (var definition in Definitions) loaded.Add(LoadArm(definition));
            return new KeyboardPressSprites(loaded.ToArray());
        }
        catch
        {
            foreach (var arm in loaded) arm.Dispose();
            throw;
        }
    }

    internal void Draw(Graphics graphics, PointF target, float press)
    {
        var x = Math.Clamp(target.X, 0f, 1f);
        var index = x < 1f / 3f ? 0 : x < 2f / 3f ? 1 : 2;
        var arm = arms[index];
        var amount = Math.Clamp(press, 0f, 1f);
        var eased = amount * amount * (3f - 2f * amount);
        var travel = (4f + (Math.Clamp(target.Y, 0f, 1f) - 0.5f) * 2f) * eased;
        var scale = 1f + travel / (Definitions[index].Bottom - PinnedThroughY);

        // Switch once, without crossfading black outlines. The whole shoulder cut
        // stays unscaled; only the lower palm moves down and rebounds 3–5px.
        var state = graphics.Save();
        try
        {
            graphics.SetClip(new Rectangle(0, 0, Width, PinnedThroughY), CombineMode.Intersect);
            graphics.DrawImageUnscaled(arm, Point.Empty);
        }
        finally { graphics.Restore(state); }
        state = graphics.Save();
        try
        {
            graphics.SetClip(new Rectangle(0, PinnedThroughY, Width, Height - PinnedThroughY), CombineMode.Intersect);
            using var transform = new Matrix(1f, 0f, 0f, scale, 0f, PinnedThroughY * (1f - scale));
            graphics.Transform = transform;
            // Retain bicubic neighbors across the pinned seam, sampling only the paw.
            var source = new RectangleF(345, PinnedThroughY - 4, 130, Definitions[index].Bottom - PinnedThroughY + 8);
            graphics.DrawImage(arm, source, source, GraphicsUnit.Pixel);
        }
        finally { graphics.Restore(state); }
    }

    private static Bitmap LoadArm(Definition definition)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "CheckInReminder.Assets.DesktopPet.KeyboardPress." + definition.Name)
            ?? throw new InvalidOperationException($"缺少键盘按压素材：{definition.Name}");
        using var source = new Bitmap(stream);
        using var crop = source.Clone(definition.Crop, PixelFormat.Format32bppPArgb);
        var pixels = ReadPixels(crop);
        using var gate = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppPArgb);
        using (var path = new GraphicsPath())
        using (var graphics = Graphics.FromImage(gate))
        {
            path.AddPolygon(definition.Outline.Select(p => new PointF(p.X - definition.Crop.X, p.Y - definition.Crop.Y)).ToArray());
            graphics.FillPath(Brushes.White, path);
        }
        var gatePixels = ReadPixels(gate);
        var interior = new bool[crop.Width * crop.Height];
        var visited = new bool[interior.Length];
        var pending = new Queue<int>();
        pending.Enqueue((definition.Seed.Y - definition.Crop.Y) * crop.Width + definition.Seed.X - definition.Crop.X);
        while (pending.TryDequeue(out var index))
        {
            if (visited[index]) continue;
            visited[index] = true;
            var offset = index * 4;
            if (gatePixels[offset + 3] == 0 || pixels[offset + 3] < 240
                || pixels[offset] < 225 || pixels[offset + 1] < 225 || pixels[offset + 2] < 225) continue;
            interior[index] = true;
            var x = index % crop.Width;
            var y = index / crop.Width;
            if (x > 0) pending.Enqueue(index - 1);
            if (x + 1 < crop.Width) pending.Enqueue(index + 1);
            if (y > 0) pending.Enqueue(index - crop.Width);
            if (y + 1 < crop.Height) pending.Enqueue(index + crop.Width);
        }

        // White palm pixels form one closed component after the shoulder cut.
        // Keep only its nearest 10px of existing dark stroke: the outer few source
        // pixels meet keyboard lines, so a conservative inner matte excludes those
        // lines and the center artwork's separate decorative motion accents.
        var selected = (bool[])interior.Clone();
        for (var index = 0; index < interior.Length; index++)
        {
            if (!interior[index]) continue;
            var x = index % crop.Width;
            var y = index / crop.Width;
            if (x > 0 && y > 0 && x + 1 < crop.Width && y + 1 < crop.Height
                && interior[index - 1] && interior[index + 1]
                && interior[index - crop.Width] && interior[index + crop.Width]) continue;
            for (var dy = -10; dy <= 10; dy++)
            for (var dx = -10; dx <= 10; dx++)
            {
                if (dx * dx + dy * dy > 100 || (uint)(x + dx) >= crop.Width || (uint)(y + dy) >= crop.Height) continue;
                var next = (y + dy) * crop.Width + x + dx;
                var offset = next * 4;
                if (gatePixels[offset + 3] > 0 && pixels[offset + 3] > 128
                    && pixels[offset] < 225 && pixels[offset + 1] < 225 && pixels[offset + 2] < 225)
                    selected[next] = true;
            }
        }
        for (var index = 0; index < selected.Length; index++)
            if (!selected[index]) Array.Clear(pixels, index * 4, 4);
        WritePixels(crop, pixels);

        var arm = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(arm);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            const float scale = 0.42f;
            graphics.DrawImage(crop, new RectangleF(
                408f + (definition.Crop.X - definition.Anchor.X) * scale,
                249f + (definition.Crop.Y - definition.Anchor.Y) * scale,
                crop.Width * scale, crop.Height * scale));
            return arm;
        }
        catch { arm.Dispose(); throw; }
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static void WritePixels(Bitmap bitmap, byte[] pixels)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
        finally { bitmap.UnlockBits(data); }
    }

    public void Dispose()
    {
        foreach (var arm in arms) arm.Dispose();
    }

    private sealed record Definition(string Name, Rectangle Crop, Point Seed, PointF Anchor, int Bottom, PointF[] Outline);

    // Native PNG coordinates, calibrated independently of the original body.
    // These gates close only the open shoulder; color connectivity supplies the matte.
    private static readonly Definition[] Definitions =
    [
        new("left.png", new(760, 590, 260, 270), new(850, 800), new(907, 620), 341,
            [new(905, 612), new(913, 613), new(915, 619), new(914, 625), new(982, 718), new(981, 725),
             new(968, 735), new(953, 750), new(944, 768), new(935, 789), new(927, 806), new(916, 821),
             new(899, 832), new(880, 838), new(855, 840), new(829, 837), new(811, 831), new(797, 822),
             new(786, 809), new(779, 794), new(776, 778), new(777, 763), new(780, 749), new(785, 734),
             new(793, 718), new(802, 702), new(814, 686), new(828, 670), new(844, 653), new(863, 635), new(885, 619)]),
        new("center-accent.png", new(760, 610, 280, 245), new(890, 780), new(917, 635), 330,
            [new(914, 626), new(921, 629), new(922, 635), new(916, 643), new(1021, 684), new(1022, 690),
             new(1014, 705), new(1004, 719), new(993, 736), new(983, 753), new(974, 770), new(963, 790),
             new(952, 807), new(939, 819), new(921, 826), new(899, 829), new(875, 827), new(855, 821),
             new(840, 812), new(830, 799), new(824, 784), new(822, 768), new(824, 751), new(827, 736),
             new(832, 719), new(840, 702), new(851, 684), new(865, 665), new(881, 647), new(899, 631)]),
        new("right.png", new(1020, 560, 250, 340), new(1160, 800), new(1108, 583), 373,
            [new(1105, 575), new(1112, 577), new(1117, 585), new(1134, 601), new(1152, 620), new(1169, 640),
             new(1184, 660), new(1197, 680), new(1212, 706), new(1224, 731), new(1233, 754), new(1240, 778),
             new(1245, 800), new(1246, 817), new(1242, 836), new(1233, 854), new(1219, 867), new(1202, 875),
             new(1181, 880), new(1159, 877), new(1141, 872), new(1124, 860), new(1113, 847), new(1101, 828),
             new(1090, 808), new(1079, 787), new(1069, 767), new(1059, 746), new(1049, 726), new(1040, 710),
             new(1040, 704), new(1045, 699), new(1051, 699), new(1057, 705)])
    ];
}
