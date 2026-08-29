using System.Diagnostics;

namespace CheckInReminder;

internal sealed class EveningConfirmForm : Form
{
    private readonly Action<bool> completed;
    private readonly AnimationSequence sequence;
    private readonly PictureBox picture;
    private readonly System.Windows.Forms.Timer animationTimer;
    private readonly Stopwatch animationClock = new();
    private int currentFrame = -1;
    private bool allowClose;
    private bool finished;

    public EveningConfirmForm(Action<bool> completed)
    {
        this.completed = completed;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 680);
        BackColor = Color.FromArgb(18, 18, 18);

        sequence = AnimationSequence.Load(
            "Gate",
            AnimationCatalog.GateDuration,
            AnimationCatalog.GateLoops);
        picture = new PictureBox
        {
            Image = sequence.Frames[0],
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 18),
            Size = new Size(580, 470),
            TabStop = false,
        };

        var message = new Label
        {
            Text = ReminderCopy.GateQuestion,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Location = new Point(30, 500),
            Size = new Size(560, 58),
        };
        var trueButton = new Button
        {
            Text = ReminderCopy.GateTrue,
            Size = new Size(150, 52),
            Location = new Point(135, 598),
        };
        var falseButton = new Button
        {
            Text = ReminderCopy.GateFalse,
            Size = new Size(150, 52),
            Location = new Point(335, 598),
        };
        UiTheme.StylePrimaryButton(trueButton);
        UiTheme.StyleSecondaryButton(falseButton);
        trueButton.Click += (_, _) => Finish(confirmed: true);
        falseButton.Click += (_, _) => Finish(confirmed: false);

        animationTimer = new System.Windows.Forms.Timer { Interval = 30 };
        animationTimer.Tick += (_, _) => UpdateAnimation();

        Controls.Add(picture);
        Controls.Add(message);
        Controls.Add(trueButton);
        Controls.Add(falseButton);

        FormClosing += (_, eventArgs) =>
        {
            if (!allowClose)
            {
                eventArgs.Cancel = true;
            }
        };
        Shown += (_, _) =>
        {
            animationClock.Restart();
            animationTimer.Start();
            BringForward();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer.Dispose();
            sequence.Dispose();
        }

        base.Dispose(disposing);
    }

    public void BringForward()
    {
        Show();
        BringToFront();
        Activate();
    }

    public void CloseForExit()
    {
        allowClose = true;
        finished = true;
        Close();
    }

    private void Finish(bool confirmed)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        allowClose = true;
        Close();
        completed(confirmed);
    }

    private void UpdateAnimation()
    {
        var frameIndex = sequence.Timeline.GetFrameIndex(animationClock.Elapsed);
        if (frameIndex == currentFrame)
        {
            return;
        }

        currentFrame = frameIndex;
        picture.Image = sequence.Frames[frameIndex];
    }
}
