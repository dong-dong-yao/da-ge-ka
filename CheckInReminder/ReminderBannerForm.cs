using System.Diagnostics;

namespace CheckInReminder;

internal sealed class ReminderBannerForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const double MaximumOpacity = 0.72;
    private const double MinimumVisibleOpacity = 0.01;
    private const int FadeInMilliseconds = 300;
    private const int FadeOutStartMilliseconds = 4400;
    private const int TotalDurationMilliseconds = 5000;

    private readonly System.Windows.Forms.Timer animationTimer;
    private readonly Stopwatch animationClock = new();
    private readonly Action<bool> completed;
    private readonly Image bannerImage;
    private bool finished;

    public ReminderBannerForm(Action<bool> completed)
    {
        this.completed = completed;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(UiTheme.BannerWidth, UiTheme.BannerHeight);
        BackColor = Color.FromArgb(24, 24, 24);
        Opacity = MinimumVisibleOpacity;
        DoubleBuffered = true;

        bannerImage = UiAssets.Load("ReminderBanner.jpg");
        var picture = new PictureBox
        {
            Image = bannerImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = Point.Empty,
            Size = new Size(UiTheme.BannerWidth, UiTheme.BannerImageHeight),
            TabStop = false,
        };

        var action = new Label
        {
            Text = "已打卡，闭嘴",
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 13, FontStyle.Bold),
            ForeColor = UiTheme.PrimaryButtonForeColor,
            BackColor = UiTheme.PrimaryButtonBackColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Size = new Size(260, 58),
            Location = new Point(70, UiTheme.BannerButtonTop),
        };
        action.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                Finish(clicked: true);
            }
        };

        Controls.Add(picture);
        Controls.Add(action);

        animationTimer = new System.Windows.Forms.Timer { Interval = 30 };
        animationTimer.Tick += (_, _) => UpdateAnimation();
        Shown += (_, _) =>
        {
            PositionAtTopCenter();
            animationClock.Restart();
            animationTimer.Start();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmMouseActivate)
        {
            message.Result = (IntPtr)MaNoActivate;
            return;
        }

        base.WndProc(ref message);
    }

    public void CloseForExit()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        animationTimer.Stop();
        animationClock.Stop();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer.Dispose();
            bannerImage.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Finish(bool clicked)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        animationTimer.Stop();
        animationClock.Stop();
        Close();
        completed(clicked);
    }

    private void UpdateAnimation()
    {
        var elapsed = animationClock.Elapsed.TotalMilliseconds;
        if (elapsed < FadeInMilliseconds)
        {
            Opacity = Math.Max(MinimumVisibleOpacity, MaximumOpacity * elapsed / FadeInMilliseconds);
            return;
        }

        if (elapsed < FadeOutStartMilliseconds)
        {
            Opacity = MaximumOpacity;
            return;
        }

        if (elapsed < TotalDurationMilliseconds)
        {
            var fadeProgress = (elapsed - FadeOutStartMilliseconds) /
                (TotalDurationMilliseconds - FadeOutStartMilliseconds);
            Opacity = Math.Max(MinimumVisibleOpacity, MaximumOpacity * (1 - fadeProgress));
            return;
        }

        Finish(clicked: false);
    }

    private void PositionAtTopCenter()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Location = new Point(workingArea.Left + ((workingArea.Width - Width) / 2), workingArea.Top + 12);
    }
}
