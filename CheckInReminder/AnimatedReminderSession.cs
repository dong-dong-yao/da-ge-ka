using System.Diagnostics;

namespace CheckInReminder;

internal sealed class AnimatedReminderSession : IDisposable
{
    private const double CharacterScale = 0.78;
    private static readonly TimeSpan BubbleDelay = TimeSpan.FromSeconds(2.1);
    private readonly AnimationSequence sequence;
    private readonly LayeredAnimationForm character;
    private readonly ReminderBubbleForm bubble;
    private readonly System.Windows.Forms.Timer timer;
    private readonly Stopwatch clock = new();
    private readonly Action<bool> completed;
    private int currentFrame = -1;
    private bool bubbleShown;
    private bool finished;

    public AnimatedReminderSession(ReminderKind kind, string characterId, Action<bool> completed)
    {
        this.completed = completed;
        var reminderCharacter = AnimationCatalog.FindCharacter(characterId) ??
            AnimationCatalog.FindCharacter(AnimationCatalog.DefaultCharacterId)!;
        sequence = AnimationSequence.Load(
            reminderCharacter.SequenceName,
            reminderCharacter.Duration,
            reminderCharacter.Loop);
        var edge = Enum.GetValues<ScreenEdge>()[Random.Shared.Next(4)];
        character = new LayeredAnimationForm(sequence.Frames[0].Size, edge, CharacterScale);
        bubble = new ReminderBubbleForm(kind, () => Finish(clicked: true));
        PositionWindows(edge);

        timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) => UpdateAnimation();
    }

    public void Show()
    {
        character.Show();
        character.SetFrame(sequence.Frames[0]);
        currentFrame = 0;
        clock.Restart();
        timer.Start();
    }

    public void CloseWithoutResult()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        Cleanup();
    }

    public void Dispose()
    {
        if (!finished)
        {
            finished = true;
            Cleanup();
        }
    }

    private void UpdateAnimation()
    {
        var elapsed = clock.Elapsed;
        if (!bubbleShown && elapsed >= BubbleDelay)
        {
            bubbleShown = true;
            bubble.Show();
        }

        var frameIndex = sequence.Timeline.GetFrameIndex(elapsed);
        if (frameIndex != currentFrame)
        {
            currentFrame = frameIndex;
            character.SetFrame(sequence.Frames[frameIndex]);
        }

        if (sequence.Timeline.HasFinished(elapsed))
        {
            Finish(clicked: false);
        }
    }

    private void Finish(bool clicked)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        Cleanup();
        completed(clicked);
    }

    private void Cleanup()
    {
        timer.Stop();
        clock.Stop();
        if (!bubble.IsDisposed)
        {
            bubble.Close();
        }
        if (!character.IsDisposed)
        {
            character.Close();
        }
        timer.Dispose();
        bubble.Dispose();
        character.Dispose();
        sequence.Dispose();
    }

    private void PositionWindows(ScreenEdge edge)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(character);
        const int edgeMargin = 4;
        const int bubbleGap = 12;
        var characterX = area.Left;
        var characterY = area.Top;

        if (edge is ScreenEdge.Top or ScreenEdge.Bottom)
        {
            var range = Math.Max(1, area.Width - character.Width - 80);
            characterX = area.Left + 40 + Random.Shared.Next(range);
            characterY = edge == ScreenEdge.Top
                ? area.Top + edgeMargin
                : area.Bottom - character.Height - edgeMargin;
        }
        else
        {
            var range = Math.Max(1, area.Height - character.Height - 80);
            characterY = area.Top + 40 + Random.Shared.Next(range);
            characterX = edge == ScreenEdge.Left
                ? area.Left + edgeMargin
                : area.Right - character.Width - edgeMargin;
        }

        character.Location = new Point(characterX, characterY);
        var bubbleX = edge switch
        {
            ScreenEdge.Left => character.Right + bubbleGap,
            ScreenEdge.Right => character.Left - bubble.Width - bubbleGap,
            _ => character.Left + ((character.Width - bubble.Width) / 2),
        };
        var bubbleY = edge switch
        {
            ScreenEdge.Top => character.Bottom + bubbleGap,
            ScreenEdge.Bottom => character.Top - bubble.Height - bubbleGap,
            _ => character.Top + ((character.Height - bubble.Height) / 2),
        };
        bubble.Location = new Point(
            Math.Clamp(bubbleX, area.Left + 8, area.Right - bubble.Width - 8),
            Math.Clamp(bubbleY, area.Top + 8, area.Bottom - bubble.Height - 8));
    }
}
