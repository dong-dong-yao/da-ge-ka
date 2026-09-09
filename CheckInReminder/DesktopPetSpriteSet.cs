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
