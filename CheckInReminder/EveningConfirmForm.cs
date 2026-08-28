namespace CheckInReminder;

internal sealed class EveningConfirmForm : Form
{
    private readonly Action<bool> completed;
    private readonly Image confirmImage;
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
        ClientSize = new Size(620, 560);
        BackColor = Color.FromArgb(18, 18, 18);

        confirmImage = UiAssets.Load("EveningConfirm.jpg");
        var picture = new PictureBox
        {
            Image = confirmImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 18),
            Size = new Size(580, 360),
            TabStop = false,
        };

        var message = new Label
        {
            Text = "真的吗，那你为什么不走",
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Location = new Point(30, 390),
            Size = new Size(560, 58),
        };
        var trueButton = new Button { Text = "真的", Size = new Size(150, 52), Location = new Point(135, 478) };
        var falseButton = new Button { Text = "假的", Size = new Size(150, 52), Location = new Point(335, 478) };
        UiTheme.StylePrimaryButton(trueButton);
        UiTheme.StyleSecondaryButton(falseButton);
        trueButton.Click += (_, _) => Finish(confirmed: true);
        falseButton.Click += (_, _) => Finish(confirmed: false);

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
        Shown += (_, _) => BringForward();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            confirmImage.Dispose();
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
}
