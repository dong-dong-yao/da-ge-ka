using System.Diagnostics;

namespace CheckInReminder;

/// <summary>
/// 封装角色动画预览的加载、播放、暂停与释放（PictureBox + Timer + Stopwatch）。
/// 供设置页当前角色预览与角色页网格卡片共用。
/// </summary>
internal sealed class AnimationPreviewPlayer : IDisposable
{
    private readonly PictureBox pictureBox;
    private readonly System.Windows.Forms.Timer timer;
    private readonly Stopwatch clock = new();
    private AnimationSequence? sequence;
    private AnimationTimeline? timeline;
    private int currentFrame = -1;
    private bool paused;
    private int posterFrame;
    private ReminderCharacter? customCharacter;
    private Bitmap? customPoster;

    public AnimationPreviewPlayer(PictureBox pictureBox, int intervalMilliseconds = 33)
    {
        this.pictureBox = pictureBox;
        timer = new System.Windows.Forms.Timer { Interval = intervalMilliseconds };
        timer.Tick += (_, _) => Advance();
    }

    public bool IsLoaded => sequence is not null || customPoster is not null;

    /// <summary>加载角色提醒动画并循环播放（除非处于暂停状态）。</summary>
    public void Load(ReminderCharacter character)
    {
        timer.Stop();
        clock.Stop();
        pictureBox.Image = null;
        sequence?.Dispose();
        sequence = null;
        customPoster?.Dispose(); customPoster = null;
        customCharacter = character.CustomPackagePath is null ? null : character;
        if (customCharacter is not null)
        {
            var posterFile = Path.Combine(character.CustomPackagePath!, "preview.png");
            if (!File.Exists(posterFile)) posterFile = Path.Combine(character.SequenceName, $"frame_{(character.CustomManifest?.FrameCount ?? 1) / 2:0000}.png");
            CustomCharacterStore.ValidateImage(posterFile, 520);
            customPoster = CharacterMediaProcessor.ReadBitmap(posterFile);
            if (pictureBox is CharacterPreviewImage customPreview) customPreview.SourceBounds = new Rectangle(Point.Empty, customPoster.Size);
            pictureBox.Image = customPoster;
            if (!paused) LoadCustomSequence();
            return;
        }
        sequence = AnimationSequence.Load(character.SequenceName, character.Duration, character.Loop);
        timeline = new AnimationTimeline(sequence.Frames.Count, character.Duration, loop: true);
        posterFrame = FindPosterFrame(sequence.Frames, out var subjectBounds);
        if (pictureBox is CharacterPreviewImage preview) preview.SourceBounds = subjectBounds;
        currentFrame = posterFrame;
        pictureBox.Image = sequence.Frames[posterFrame];
        clock.Restart();
        if (paused)
        {
            clock.Stop();
        }
        else
        {
            timer.Start();
        }
    }

    /// <summary>展示角色充分入场后的封面；恢复播放仍从动画起点开始。</summary>
    public void ShowPosterFrame()
    {
        if (customPoster is not null)
        {
            timer.Stop(); clock.Stop(); pictureBox.Image = customPoster;
            sequence?.Dispose(); sequence = null;
            return;
        }
        if (sequence is null)
        {
            return;
        }

        timer.Stop();
        currentFrame = posterFrame;
        pictureBox.Image = sequence.Frames[posterFrame];
        clock.Restart();
        clock.Stop();
    }

    // 入场动画的首帧通常只有门框/桌沿，按相对首帧新增的可见内容选择封面。
    // 只调整预览窗口，不修改提醒动画资源；固定裁切区域避免播放时逐帧缩放跳动。
    private static int FindPosterFrame(IReadOnlyList<Bitmap> frames, out Rectangle bounds)
    {
        var bestIndex = 0;
        var bestCount = -1;
        bounds = new Rectangle(Point.Empty, frames[0].Size);
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            var count = 0;
            var left = frame.Width;
            var top = frame.Height;
            var right = 0;
            var bottom = 0;
            for (var y = 0; y < frame.Height; y += 4)
            for (var x = 0; x < frame.Width; x += 4)
            {
                var pixel = frame.GetPixel(x, y);
                if (pixel.A < 96) continue;
                var initial = frames[0].GetPixel(Math.Min(x, frames[0].Width - 1), Math.Min(y, frames[0].Height - 1));
                if (frames.Count > 1 && Math.Abs(pixel.A - initial.A)
                    + Math.Abs(pixel.R - initial.R) + Math.Abs(pixel.G - initial.G)
                    + Math.Abs(pixel.B - initial.B) < 32) continue;
                count++;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 4);
                bottom = Math.Max(bottom, y + 4);
            }
            if (count <= bestCount) continue;
            bestCount = count;
            bestIndex = index;
            if (count > 0)
                bounds = Rectangle.FromLTRB(Math.Max(0, left - 8), Math.Max(0, top - 8),
                    Math.Min(frame.Width, right + 8), Math.Min(frame.Height, bottom + 8));
        }
        return bestIndex;
    }

    /// <summary>暂停/恢复播放；暂停时停在当前帧，恢复时接着播。</summary>
    public void SetPaused(bool value)
    {
        if (paused == value)
        {
            return;
        }

        paused = value;
        if (paused)
        {
            timer.Stop();
            clock.Stop();
            if (customPoster is not null) ShowPosterFrame();
            return;
        }

        if (customCharacter is not null && sequence is null) LoadCustomSequence();
        if (sequence is not null)
        {
            clock.Start();
            timer.Start();
        }
    }

    private void LoadCustomSequence()
    {
        var character = customCharacter!;
        sequence = AnimationSequence.Load(character.SequenceName, character.Duration, false);
        timeline = new AnimationTimeline(sequence.Frames.Count, character.Duration, true);
        currentFrame = -1; clock.Restart(); timer.Start();
    }

    private void Advance()
    {
        if (paused || sequence is null || timeline is null)
        {
            return;
        }

        var frameIndex = timeline.GetFrameIndex(clock.Elapsed);
        if (frameIndex == currentFrame)
        {
            return;
        }

        currentFrame = frameIndex;
        pictureBox.Image = sequence.Frames[frameIndex];
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        pictureBox.Image = null;
        sequence?.Dispose();
        customPoster?.Dispose();
        clock.Stop();
    }
}
