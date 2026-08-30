using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal sealed class ToggleSwitch : Control
{
    private bool isChecked;

    public event EventHandler? CheckedChanged;

    [DefaultValue(false)]
    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
            {
                return;
            }

            isChecked = value;
            AccessibleDefaultActionDescription = value ? "关闭" : "开启";
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        Size = new Size(50, 28);
        MinimumSize = new Size(44, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
        AccessibleDefaultActionDescription = "开启";
        SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable, true);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new Rectangle(0, 1, Width - 1, Height - 2);
        using var trackPath = RoundedPanel.CreateRoundedPath(track, track.Height / 2);
        using var trackBrush = new SolidBrush(Checked ? UiTheme.AccentColor : Color.FromArgb(205, 194, 182));
        e.Graphics.FillPath(trackBrush, trackPath);

        var inset = 4;
        var diameter = Height - (inset * 2);
        var thumbX = Checked ? Width - diameter - inset : inset;
        using var thumbBrush = new SolidBrush(Color.White);
        e.Graphics.FillEllipse(thumbBrush, thumbX, inset, diameter, diameter);

        if (Focused)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, UiTheme.AccentColor, BackColor);
        }
    }
}
