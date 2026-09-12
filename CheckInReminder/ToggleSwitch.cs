using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 暖色滑动开关：切换时滑块平滑移动，轨道颜色渐变过渡。
/// </summary>
internal sealed class ToggleSwitch : Control
{
    private bool isChecked;
    private float position;
    private bool hovered;
    private readonly AnimationHelper slideAnimation;

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
            if (!IsHandleCreated || !Visible)
            {
                slideAnimation.Stop();
                position = value ? 1f : 0f;
                Invalidate();
            }
            else slideAnimation.Start(position, value ? 1f : 0f, 220);
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        Size = new Size(48, 26);
        MinimumSize = new Size(44, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
        AccessibleDefaultActionDescription = "开启";
        SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable, true);
        BackColor = Color.Transparent;

        slideAnimation = new AnimationHelper(v =>
        {
            position = v;
            Invalidate();
        });
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

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new Rectangle(0, 1, Width - 1, Height - 2);
        using var trackPath = RoundedPanel.CreateRoundedPath(track, track.Height / 2);

        var offColor = Color.FromArgb(219, 207, 192);
        var onColor = UiTheme.AccentColor;
        var trackColor = Blend(offColor, onColor, position);
        using var trackBrush = new SolidBrush(trackColor);
        g.FillPath(trackBrush, trackPath);

        var inset = 4;
        var diameter = Height - (inset * 2);
        var travel = Width - diameter - (inset * 2);
        var thumbX = inset + (travel * position);

        // 滑块柔和投影
        using (var shadowBrush = new SolidBrush(Color.FromArgb(36, 60, 40, 20)))
        {
            g.FillEllipse(shadowBrush, thumbX, inset + 2, diameter, diameter);
        }

        using var thumbBrush = new SolidBrush(hovered ? Color.FromArgb(255, 250, 242) : Color.White);
        g.FillEllipse(thumbBrush, thumbX, inset, diameter, diameter);

        if (Focused)
        {
            using var focusPen = new Pen(UiTheme.AccentColor, 1.6f);
            var focusBounds = new Rectangle(-2, -1, Width + 3, Height + 3);
            using var focusPath = RoundedPanel.CreateRoundedPath(focusBounds, (Height + 3) / 2);
            g.DrawPath(focusPen, focusPath);
        }
    }

    private static Color Blend(Color a, Color b, float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            255,
            (int)(a.R + ((b.R - a.R) * clamped)),
            (int)(a.G + ((b.G - a.G) * clamped)),
            (int)(a.B + ((b.B - a.B) * clamped)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            slideAnimation.Dispose();
        }

        base.Dispose(disposing);
    }
}
