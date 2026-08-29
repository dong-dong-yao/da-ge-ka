using System.Reflection;

namespace CheckInReminder;

internal sealed class AnimationSequence : IDisposable
{
    private AnimationSequence(List<Bitmap> frames, TimeSpan duration, bool loop)
    {
        Frames = frames;
        Timeline = new AnimationTimeline(frames.Count, duration, loop);
    }

    public IReadOnlyList<Bitmap> Frames { get; }

    public AnimationTimeline Timeline { get; }

    public static AnimationSequence Load(string sequenceName, TimeSpan duration, bool loop)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var prefix = $"CheckInReminder.Assets.Animations.{sequenceName}.frame_";
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) &&
                name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (resources.Length == 0)
        {
            throw new InvalidOperationException($"缺少动画资源：{sequenceName}");
        }

        var frames = new List<Bitmap>(resources.Length);
        try
        {
            foreach (var resource in resources)
            {
                using var stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"缺少嵌入图片资源：{resource}");
                using var source = Image.FromStream(stream);
                frames.Add(new Bitmap(source));
            }

            return new AnimationSequence(frames, duration, loop);
        }
        catch
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }

            throw;
        }
    }

    public void Dispose()
    {
        foreach (var frame in Frames)
        {
            frame.Dispose();
        }
    }
}
