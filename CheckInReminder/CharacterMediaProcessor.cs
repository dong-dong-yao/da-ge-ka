using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CheckInReminder;

public enum BackgroundRemoval { Auto, Preserve, Green, Blue, White }

internal static class CharacterMediaProcessor
{
    private static readonly string[] InputOptions = ["-v", "error", "-max_alloc", "268435456", "-protocol_whitelist", "file,pipe", "-format_whitelist", "mov,matroska,webm,image2,png_pipe,jpeg_pipe,webp_pipe"];
    internal static async Task<CustomAnimationInfo> VideoAsync(string source, string target, BackgroundRemoval background, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CheckSource(source, [".mp4", ".mov", ".webm"]);
        var json = await BundledMediaTools.RunAsync("ffprobe", [.. InputOptions, "-show_entries", "stream=codec_type,codec_name,width,height,duration:format=duration", "-of", "json", source], token);
        using var doc = JsonDocument.Parse(json);
        var stream = doc.RootElement.GetProperty("streams").EnumerateArray().FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");
        if (stream.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("素材中没有视频画面。");
        var width = stream.GetProperty("width").GetInt32();
        var height = stream.GetProperty("height").GetInt32();
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new InvalidDataException("视频最长边不能超过 4096 像素。");
        var durationText = stream.TryGetProperty("duration", out var time) ? time.GetString()
            : doc.RootElement.GetProperty("format").TryGetProperty("duration", out var formatTime) ? formatTime.GetString() : null;
        if (!double.TryParse(durationText, CultureInfo.InvariantCulture, out var duration) || !double.IsFinite(duration) || duration is < 3 or > 15)
            throw new InvalidDataException("请选择 3–15 秒的提醒视频。");
        Directory.CreateDirectory(target);
        var codec = stream.GetProperty("codec_name").GetString();
        var decoder = codec == "vp9" ? new[] { "-c:v", "libvpx-vp9" } : codec == "vp8" ? new[] { "-c:v", "libvpx" } : Array.Empty<string>();
        await BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", .. InputOptions, .. decoder, "-i", source,
            "-map", "0:v:0", "-an", "-sn", "-dn", "-t", "15", "-vf", "fps=12,scale=520:520:force_original_aspect_ratio=decrease",
            "-frames:v", "180", "-threads", "2", "-pix_fmt", "rgba", "-start_number", "0", Path.Combine(target, "frame_%04d.png")], token);
        await Task.Run(() => ProcessFrames(target, background, token), token);
        return new CustomAnimationInfo { DurationSeconds = duration, FrameCount = Directory.GetFiles(target, "frame_*.png").Length };
    }

    internal static void CheckSource(string path, string[] extensions)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path) || !extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            throw new InvalidDataException("请选择本机支持的素材文件。");
        if (new FileInfo(path).Length is <= 0 or > 150 * 1024 * 1024) throw new InvalidDataException("单个素材必须小于 150 MB。");
    }

    internal static void ProcessFrames(string folder, BackgroundRemoval removal, CancellationToken token)
    {
        var files = Directory.GetFiles(folder, "frame_*.png").Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0) throw new InvalidDataException("没有读取到动画画面。");
        Rectangle union = Rectangle.Empty;
        Size size = Size.Empty;
        // One background decision and one crop for the whole clip prevent flickering and jumping.
        using (var first = new Bitmap(files[0])) if (removal == BackgroundRemoval.Auto) removal = DetectBackground(first);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            using var bitmap = ReadBitmap(file);
            if (size != Size.Empty && bitmap.Size != size) throw new InvalidDataException("视频帧尺寸发生变化。");
            size = bitmap.Size;
            var bounds = RemoveBackground(bitmap, removal);
            if (!bounds.IsEmpty) union = union.IsEmpty ? bounds : Rectangle.Union(union, bounds);
            bitmap.Save(file, ImageFormat.Png);
        }
        if (union.IsEmpty) throw new InvalidDataException("处理后没有可见角色，请检查视频；如果角色与背景同色，请换成另一种纯色或透明背景重新制作。");
        union.Inflate(8, 8);
        union.Intersect(new Rectangle(Point.Empty, size));
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            using var bitmap = ReadBitmap(file);
            using var cropped = bitmap.Clone(union, PixelFormat.Format32bppArgb);
            cropped.Save(file, ImageFormat.Png);
        }
    }

    internal static Bitmap ReadBitmap(string file)
    {
        using var image = Image.FromFile(file);
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
            0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
        return bitmap;
    }

    internal static BackgroundRemoval DetectBackground(Bitmap bitmap)
    {
        var green = 0; var blue = 0; var transparent = 0; var count = 0;
        for (var x = 0; x < bitmap.Width; x += Math.Max(1, bitmap.Width / 40))
        foreach (var y in new[] { 0, bitmap.Height - 1 })
        {
            var c = bitmap.GetPixel(x, y); count++;
            if (c.A < 240) transparent++;
            if (c.G > 80 && c.G - Math.Max(c.R, c.B) > 60) green++;
            if (c.B > 80 && c.B - Math.Max(c.R, c.G) > 60) blue++;
        }
        if (transparent > 0) return BackgroundRemoval.Preserve;
        return green > count * .7 ? BackgroundRemoval.Green : blue > count * .7 ? BackgroundRemoval.Blue : BackgroundRemoval.Preserve;
    }

    internal static Rectangle RemoveBackground(Bitmap bitmap, BackgroundRemoval removal)
    {
        if (removal == BackgroundRemoval.Auto) removal = DetectBackground(bitmap);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var left = bitmap.Width; var top = bitmap.Height; var right = -1; var bottom = -1;
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var p = y * data.Stride + x * 4;
                var b = bytes[p]; var g = bytes[p + 1]; var r = bytes[p + 2];
                var strength = removal switch
                {
                    BackgroundRemoval.Green => Math.Clamp((g - Math.Max(r, b) - 20) / 65f, 0, 1),
                    BackgroundRemoval.Blue => Math.Clamp((b - Math.Max(r, g) - 20) / 65f, 0, 1),
                    BackgroundRemoval.White => Math.Clamp((Math.Min(r, Math.Min(g, b)) - 225) / 30f, 0, 1),
                    _ => 0
                };
                bytes[p + 3] = (byte)Math.Round(bytes[p + 3] * (1 - strength));
                if (strength > 0 && removal == BackgroundRemoval.Green) bytes[p + 1] = (byte)Math.Min(g, Math.Max(r, b) + 8);
                if (strength > 0 && removal == BackgroundRemoval.Blue) bytes[p] = (byte)Math.Min(b, Math.Max(r, g) + 8);
                if (bytes[p + 3] < 16) continue;
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally { bitmap.UnlockBits(data); }
    }

    internal static async Task<Bitmap> ImageAsync(string file, BackgroundRemoval removal, CancellationToken token)
    {
        CheckSource(file, [".png", ".jpg", ".jpeg", ".webp"]);
        await ImageSizeAsync(file, token);
        var temp = Path.Combine(Path.GetTempPath(), "dagaka-image-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", .. InputOptions, "-i", file, "-vf", "format=rgba,scale=600:448:force_original_aspect_ratio=decrease,pad=600:448:(ow-iw)/2:(oh-ih)/2:color=0x00000000", "-frames:v", "1", "-pix_fmt", "rgba", temp], token);
            var bitmap = ReadBitmap(temp);
            try { RemoveBackground(bitmap, removal); return bitmap; }
            catch { bitmap.Dispose(); throw; }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static async Task<Size> ImageSizeAsync(string file, CancellationToken token)
    {
        CheckSource(file, [".png", ".jpg", ".jpeg", ".webp"]);
        var json = await BundledMediaTools.RunAsync("ffprobe", [.. InputOptions, "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "json", file], token);
        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new InvalidDataException("图片没有可读画面。");
        var stream = streams[0];
        var width = stream.GetProperty("width").GetInt32(); var height = stream.GetProperty("height").GetInt32();
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new InvalidDataException("图片最长边不能超过 4096 像素。");
        return new Size(width, height);
    }

    internal static void ValidateTransparentPng(string path)
    {
        CheckSource(path, [".png"]);
        // Read dimensions from the PNG header before allocating decoded pixels.
        Span<byte> header = stackalloc byte[24];
        using (var stream = File.OpenRead(path)) stream.ReadExactly(header);
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("请上传真正的透明 PNG 图片，不要只修改文件扩展名。");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new InvalidDataException("图片最长边不能超过 4096 像素。");
        using var image = ReadBitmap(path);
        var transparent = false; var visible = false;
        for (var y = 0; y < image.Height; y += Math.Max(1, image.Height / 200))
        for (var x = 0; x < image.Width; x += Math.Max(1, image.Width / 200))
        {
            var alpha = image.GetPixel(x, y).A;
            transparent |= alpha == 0; visible |= alpha > 128;
        }
        if (!transparent || !visible) throw new InvalidDataException($"“{Path.GetFileName(path)}”没有有效的透明背景。请让图片工具导出真正透明的 PNG，白底或画出来的棋盘格不算透明。");
    }
}
