using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 自绘圆形箭头按钮：箭头是几何绘制的 chevron，保证在圆心绝对居中。
/// </summary>
internal sealed class ChevronButton : Control
{
    private bool pressed;
    private float hoverAmount;
    private readonly AnimationHelper hoverAnimation;

    [DefaultValue(false)]
    public bool PointsLeft { get; set; }

    public ChevronButton()
    {
        SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable, true);
        Size = new Size(40, 40);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        hoverAnimation = new AnimationHelper(v =>
        {
            hoverAmount = v;
            Invalidate();
        });
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hoverAnimation.Start(hoverAmount, 1f, 160);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverAnimation.Start(hoverAmount, 0f, 200);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        pressed = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        var baseColor = Blend(UiTheme.SurfaceColor, UiTheme.AccentSoftColor, hoverAmount);
        if (pressed)
        {
            baseColor = Blend(baseColor, UiTheme.AccentSoftColor, 0.6f);
        }

        using (var fill = new SolidBrush(Enabled ? baseColor : UiTheme.WarmBackgroundColor))
        {
            g.FillEllipse(fill, bounds);
        }

        using (var pen = new Pen(UiTheme.BorderStrongColor, 1f))
        {
            g.DrawEllipse(pen, bounds);
        }

        var cx = Width / 2f;
        var cy = Height / 2f;
        var arrowColor = Enabled
            ? Blend(UiTheme.MutedTextColor, UiTheme.AccentColor, hoverAmount)
            : UiTheme.MutedTextColor;
        using var arrowPen = new Pen(arrowColor, 2.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        var dir = PointsLeft ? -1 : 1;
        g.DrawLine(arrowPen, cx - (4f * dir), cy - 5.5f, cx + (3f * dir), cy);
        g.DrawLine(arrowPen, cx + (3f * dir), cy, cx - (4f * dir), cy + 5.5f);

        if (Focused)
        {
            using var focusPen = new Pen(UiTheme.AccentColor, 1.4f);
            g.DrawEllipse(focusPen, 3, 3, Width - 7, Height - 7);
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
            hoverAnimation.Dispose();
        }

        base.Dispose(disposing);
    }
}
