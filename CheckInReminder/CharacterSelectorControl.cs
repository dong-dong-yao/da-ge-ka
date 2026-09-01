using System.Diagnostics;

namespace CheckInReminder;

internal sealed class CharacterSelectorControl : UserControl
{
    private readonly CharacterSelection selection;
    private readonly PictureBox previewBox;
    private readonly Label nameLabel;
    private readonly Label positionLabel;
    private readonly Label statusLabel;
    private readonly ChevronButton previousButton;
    private readonly ChevronButton nextButton;
    private readonly BrandButton confirmButton;
    private readonly System.Windows.Forms.Timer animationTimer;
    private readonly System.Windows.Forms.Timer feedbackTimer;
    private readonly Stopwatch animationClock = new();
    private AnimationSequence? previewSequence;
    private AnimationTimeline? previewTimeline;
    private int currentFrame = -1;
    private bool interactionPaused;

    public event EventHandler? SelectionConfirmed;

    public string ConfirmedCharacterId => selection.ConfirmedCharacterId;

    public CharacterSelectorControl(string? initialCharacterId)
    {
        selection = new CharacterSelection(AnimationCatalog.Characters, initialCharacterId);
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Padding = new Padding(4);

        var title = new Label
        {
            Text = "提醒角色",
            AutoSize = true,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 14, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 2),
        };
        var subtitle = new Label
        {
            Text = "陪你完成今天的打卡",
            AutoSize = true,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 14),
        };

        previewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = UiTheme.AccentSoftColor,
            Margin = Padding.Empty,
        };
        var previewShell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.AccentSoftColor,
            Padding = new Padding(12),
            Margin = new Padding(0, 2, 0, 14),
        };
        previewShell.Controls.Add(previewBox);

        previousButton = new ChevronButton { PointsLeft = true, Dock = DockStyle.Fill, AccessibleName = "上一个角色", Margin = new Padding(3) };
        nextButton = new ChevronButton { PointsLeft = false, Dock = DockStyle.Fill, AccessibleName = "下一个角色", Margin = new Padding(3) };
        previousButton.Click += (_, _) => Browse(previous: true);
        nextButton.Click += (_, _) => Browse(previous: false);

        nameLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 14, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
        };
        var navigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        navigation.Controls.Add(previousButton, 0, 0);
        navigation.Controls.Add(nameLabel, 1, 0);
        navigation.Controls.Add(nextButton, 2, 0);

        positionLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
        };
        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.AccentColor,
            BackColor = Color.Transparent,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9.5f, FontStyle.Bold),
        };
        confirmButton = new BrandButton
        {
            Text = "使用这个角色",
            Dock = DockStyle.Fill,
            CornerRadius = 18,
            AccessibleName = "确认使用当前角色",
            Margin = new Padding(0, 8, 0, 0),
        };
        confirmButton.Click += (_, _) => ConfirmSelection();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.Transparent,
            Padding = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(previewShell, 0, 2);
        layout.Controls.Add(navigation, 0, 3);
        layout.Controls.Add(positionLabel, 0, 4);
        layout.Controls.Add(statusLabel, 0, 5);
        layout.Controls.Add(confirmButton, 0, 6);
        Controls.Add(layout);

        animationTimer = new System.Windows.Forms.Timer { Interval = 33 };
        animationTimer.Tick += (_, _) => AdvancePreview();
        feedbackTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        feedbackTimer.Tick += (_, _) =>
        {
            feedbackTimer.Stop();
            confirmButton.Text = "使用这个角色";
        };

        UpdateSelectionView(reloadAnimation: true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer.Stop();
            feedbackTimer.Stop();
            animationTimer.Dispose();
            feedbackTimer.Dispose();
            previewBox.Image = null;
            previewSequence?.Dispose();
            animationClock.Stop();
        }

        base.Dispose(disposing);
    }

    internal void SetInteractionPaused(bool paused)
    {
        if (interactionPaused == paused)
        {
            return;
        }

        interactionPaused = paused;
        if (paused)
        {
            animationTimer.Stop();
            animationClock.Stop();
            return;
        }

        if (previewSequence is not null)
        {
            animationClock.Start();
            animationTimer.Start();
        }
    }

    private void Browse(bool previous)
    {
        var moved = previous ? selection.MovePrevious() : selection.MoveNext();
        if (moved)
        {
            statusLabel.Text = "预览中 · 点击下方确认";
            UpdateSelectionView(reloadAnimation: true);
        }
    }

    private void ConfirmSelection()
    {
        selection.Confirm();
        statusLabel.Text = "已选择 ✓";
        confirmButton.Text = "已选择 ✓";
        feedbackTimer.Stop();
        feedbackTimer.Start();
        SelectionConfirmed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionView(bool reloadAnimation)
    {
        var character = selection.SelectedCharacter;
        nameLabel.Text = character.DisplayName;
        var index = AnimationCatalog.Characters.ToList().IndexOf(character);
        positionLabel.Text = $"{index + 1} / {AnimationCatalog.Characters.Count}";
        previousButton.Enabled = selection.CanBrowse;
        nextButton.Enabled = selection.CanBrowse;
        statusLabel.Text = string.Equals(character.Id, selection.ConfirmedCharacterId, StringComparison.Ordinal)
            ? "当前使用"
            : "预览中 · 点击下方确认";

        if (reloadAnimation)
        {
            LoadPreview(character);
        }
    }

    private void LoadPreview(ReminderCharacter character)
    {
        animationTimer.Stop();
        animationClock.Stop();
        previewBox.Image = null;
        previewSequence?.Dispose();
        previewSequence = AnimationSequence.Load(character.SequenceName, character.Duration, character.Loop);
        previewTimeline = new AnimationTimeline(previewSequence.Frames.Count, character.Duration, loop: true);
        currentFrame = 0;
        previewBox.Image = previewSequence.Frames[0];
        animationClock.Restart();
        if (interactionPaused)
        {
            animationClock.Stop();
        }
        else
        {
            animationTimer.Start();
        }
    }

    private void AdvancePreview()
    {
        if (interactionPaused || previewSequence is null || previewTimeline is null)
        {
            return;
        }

        var frameIndex = previewTimeline.GetFrameIndex(animationClock.Elapsed);
        if (frameIndex == currentFrame)
        {
            return;
        }

        currentFrame = frameIndex;
        previewBox.Image = previewSequence.Frames[frameIndex];
    }
}
