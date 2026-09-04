using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckInReminder;

internal static class DesktopPetArtwork
{
    private const string WhiteBearResource =
        "CheckInReminder.Assets.DesktopPet.white-bear-typing.jpg";
    private const int RenderWidth = 600;
    private const int RenderHeight = 448;

    public static (Bitmap Idle, Bitmap LeftTap, Bitmap RightTap) LoadWhiteBearTypingFrames()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(WhiteBearResource)
            ?? throw new InvalidOperationException($"缺少桌宠原图资源：{WhiteBearResource}");
        using var source = Image.FromStream(stream);
        var idle = ResizeToArgb(source, RenderWidth, RenderHeight);
        RemoveConnectedWhiteBackground(idle);

        try
        {
            return (
                idle,
                CreateTypingFrame(idle, pressLeft: true),
                CreateTypingFrame(idle, pressLeft: false));
        }
        catch
        {
            idle.Dispose();
            throw;
        }
    }

    private static Bitmap ResizeToArgb(Image source, int width, int height)
    {
        var result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.White);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        return result;
    }

    private static Bitmap CreateTypingFrame(Bitmap idle, bool pressLeft)
    {
        var result = new Bitmap(idle.Width, idle.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var horizontalNudge = pressLeft ? -9 : 9;
        graphics.DrawImageUnscaled(idle, horizontalNudge, 10);

        // 单张原图没有独立手臂图层：用交替的机身下压和键盘受力阴影表现左右敲击。
        // 所有像素仍来自用户原图，阴影仅作为按键反馈，不替换角色画面。
        var pressArea = pressLeft
            ? new Rectangle(208, 314, 138, 54)
            : new Rectangle(355, 314, 138, 54);
        using var pressBrush = new SolidBrush(Color.FromArgb(82, 48, 48, 48));
        using var pressPen = new Pen(Color.FromArgb(115, 20, 20, 20), 3);
        graphics.FillEllipse(pressBrush, pressArea);
        graphics.DrawArc(pressPen, pressArea, 15, 150);

        return result;
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
                if ((uint)x >= bitmap.Width || (uint)y >= bitmap.Height)
                {
                    return;
                }

                var pixelIndex = (y * bitmap.Width) + x;
                if (visited[pixelIndex])
                {
                    return;
                }

                visited[pixelIndex] = true;
                var offset = (y * data.Stride) + (x * 4);
                var alpha = bytes[offset + 3];
                if (alpha == 0 ||
                    (bytes[offset] * 255) < (alpha * 240) ||
                    (bytes[offset + 1] * 255) < (alpha * 240) ||
                    (bytes[offset + 2] * 255) < (alpha * 240))
                {
                    return;
                }

                bytes[offset + 3] = 0;
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
}
