using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal sealed class ReminderBubbleForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private readonly bool interactive;

    public ReminderBubbleForm(ReminderKind kind, Action clicked)
    {
        interactive = kind != ReminderKind.Break;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = UiTheme.WarmBackgroundColor;
        ForeColor = UiTheme.TextColor;
        ClientSize = interactive ? new Size(370, 160) : new Size(430, 112);
        DoubleBuffered = true;

        var message = new Label
        {
            Text = interactive ? ReminderCopy.CheckInQuestion : ReminderCopy.BreakMessage,
            Dock = DockStyle.Top,
            Height = interactive ? 82 : ClientSize.Height,
            Padding = new Padding(18, 12, 18, 6),
            Font = new Font(
                (SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily,
                interactive ? 17 : 14,
                FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(message);

        if (interactive)
        {
            var button = new Button
            {
                Text = ReminderCopy.CheckInButton,
                Size = new Size(210, 50),
                Location = new Point((ClientSize.Width - 210) / 2, 94),
            };
            UiTheme.StylePrimaryButton(button);
            button.Click += (_, _) => clicked();
            Controls.Add(button);
        }

        using var path = CreateRoundedRectangle(ClientRectangle, 26);
        Region = new Region(path);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            if (!interactive)
            {
                parameters.ExStyle |= WsExTransparent;
            }
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

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedRectangle(
            Rectangle.Inflate(ClientRectangle, -2, -2),
            24);
        using var pen = new Pen(UiTheme.BorderColor, 4);
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
