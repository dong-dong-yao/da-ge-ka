using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CheckInReminder;

internal sealed class CustomPetRenderer : IDisposable
{
    private readonly Dictionary<string, Bitmap> images = [];
    private readonly CustomMouseRig? rig;
    private byte[]? armPixels;
    private readonly DesktopPetSpriteSet? template;
    private readonly Dictionary<string, CalibratedMouseLayers> calibrated = [];
    public CustomPetRenderer(string folder, CustomMouseRig? rig, bool useTemplateMouse = false, IReadOnlyDictionary<string, MouseCalibration>? calibrations = null)
    {
        this.rig = rig;
        if (calibrations is { Count: > 0 })
        {
            try { foreach (var (key, spec) in calibrations) calibrated[key] = CalibratedMouseLayers.Load(folder, key, spec); }
            catch { Dispose(); throw; }
            return;
        }
        if (useTemplateMouse) { template = DesktopPetSpriteSet.LoadCustom(folder); return; }
        rig?.Validate();
        try
        {
            foreach (var name in new[] { "idle", "left", "right", "center", "mouse" })
            {
                var path = Path.Combine(folder, name + ".png");
                if (!File.Exists(path) && (name == "center" || name == "mouse" && rig is null)) continue;
                using var source = Image.FromFile(path);
                images[name] = new Bitmap(source);
            }
            if (rig is not null)
            {
                var arm = images["mouse"];
                var data = arm.LockBits(new Rectangle(Point.Empty, arm.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try { armPixels = new byte[arm.Width * arm.Height * 4]; Marshal.Copy(data.Scan0, armPixels, 0, armPixels.Length); }
                finally { arm.UnlockBits(data); }
            }
        }
        catch { Dispose(); throw; }
    }

    public Bitmap Render(DesktopPetRigPose pose)
    {
        if (template is not null) return template.Render(pose);
        if (calibrated.Count > 0)
        {
            var target = !pose.KeyboardContact ? "idle" : PetKeyboardPoseMapper.Map(pose.KeyboardTarget.X, calibrated.ContainsKey("center")) switch
            { PetKeyboardPose.Left => "left", PetKeyboardPose.Center => "center", _ => "right" };
            return calibrated[target].Render(pose);
        }
        var key = "idle";
        if (pose.KeyboardContact)
            key = PetKeyboardPoseMapper.Map(pose.KeyboardTarget.X, images.ContainsKey("center")) switch
            { PetKeyboardPose.Left => "left", PetKeyboardPose.Center => "center", _ => "right" };
        var result = new Bitmap(images[key]);
        if (rig is null) return result;
        using var g = Graphics.FromImage(result);
        g.CompositingMode = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var arm = images["mouse"];
        var shoulder = new PointF(rig.ShoulderX * arm.Width, rig.ShoulderY * arm.Height);
        var mouse = new PointF(rig.MouseX * arm.Width, rig.MouseY * arm.Height);
        var axis = new PointF(mouse.X - shoulder.X, mouse.Y - shoulder.Y);
        var lengthSquared = axis.X * axis.X + axis.Y * axis.Y;
        var dx = Math.Clamp(pose.MouseOffset.X, -1, 1) * arm.Width * .02f;
        var dy = (Math.Clamp(pose.MouseOffset.Y, -1, 1) * .015f + Math.Clamp(pose.MousePress, 0, 1) * .012f) * arm.Height;
        if (dx == 0 && dy == 0) { g.DrawImageUnscaled(arm, Point.Empty); return result; }
        // Invert p' = p + delta * clamp(project(p), 0, 1) for each output pixel.
        // A continuous inverse map avoids the holes produced by individually clipped triangles.
        var denominator = lengthSquared + dx * axis.X + dy * axis.Y;
        var bytes = new byte[arm.Width * arm.Height * 4];
        for (var y = 0; y < arm.Height; y++)
        for (var x = 0; x < arm.Width; x++)
        {
            var weight = Math.Clamp(((x - shoulder.X) * axis.X + (y - shoulder.Y) * axis.Y) / denominator, 0, 1);
            var sx = x - dx * weight; var sy = y - dy * weight;
            var ix = (int)MathF.Floor(sx); var iy = (int)MathF.Floor(sy);
            var fx = sx - ix; var fy = sy - iy;
            var target = (y * arm.Width + x) * 4;
            for (var c = 0; c < 4; c++)
            {
                float Pixel(int px, int py) => px < 0 || px >= arm.Width || py < 0 || py >= arm.Height ? 0 : armPixels![(py * arm.Width + px) * 4 + c];
                var value = (Pixel(ix, iy) * (1 - fx) + Pixel(ix + 1, iy) * fx) * (1 - fy)
                    + (Pixel(ix, iy + 1) * (1 - fx) + Pixel(ix + 1, iy + 1) * fx) * fy;
                bytes[target + c] = (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
            }
        }
        using var warped = new Bitmap(arm.Width, arm.Height, PixelFormat.Format32bppPArgb);
        var output = warped.LockBits(new Rectangle(Point.Empty, warped.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try { Marshal.Copy(bytes, 0, output.Scan0, bytes.Length); }
        finally { warped.UnlockBits(output); }
        g.DrawImageUnscaled(warped, Point.Empty);
        return result;
    }
    public void Dispose() { template?.Dispose(); foreach (var image in images.Values) image.Dispose(); images.Clear(); foreach (var layer in calibrated.Values) layer.Dispose(); calibrated.Clear(); }
}
