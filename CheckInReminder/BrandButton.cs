using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal enum BrandButtonKind
{
    Primary,
    Secondary,
}

/// <summary>
/// 品牌按钮：圆角胶囊造型，悬停/按下时背景色平滑过渡。
/// </summary>
internal sealed class BrandButton : Button
{
    private int cornerRadius = 16;
    private readonly BrandButtonKind kind;
    private float hoverAmount;
    private float pressAmount;
    private readonly AnimationHelper stateAnimation;
    private bool animatingToHover;
    private bool animatingToPress;

    [DefaultValue(16)]
    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = value;
            UpdateRegion();
            Invalidate();
        }
    }

    public BrandButton(BrandButtonKind kind = BrandButtonKind.Primary)
    {
        this.kind = kind;
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f, FontStyle.Bold);
        Padding = new Padding(10, 0, 10, 0);
        BackColor = kind == BrandButtonKind.Primary
            ? UiTheme.PrimaryButtonBackColor
            : UiTheme.SecondaryButtonBackColor;
        ForeColor = kind == BrandButtonKind.Primary
            ? UiTheme.PrimaryButtonForeColor
            : UiTheme.SecondaryButtonForeColor;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        stateAnimation = new AnimationHelper(v =>
        {
            if (animatingToPress)
            {
                pressAmount = v;
            }
            else
            {
                hoverAmount = v;
            }

            Invalidate();
        });
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        AnimateTo(hover: true, target: 1f);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        animatingToPress = false;
        pressAmount = 0f;
        AnimateTo(hover: true, target: 0f);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        AnimateTo(hover: false, target: 1f, duration: 120);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        AnimateTo(hover: false, target: 0f, duration: 160);
    }

    private void AnimateTo(bool hover, float target, int duration = 200)
    {
        animatingToHover = hover;
        animatingToPress = !hover;
        var start = hover ? hoverAmount : pressAmount;
        stateAnimation.Start(start, target, duration);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color baseColor;
        Color hoverColor;
        Color pressColor;
        if (kind == BrandButtonKind.Primary)
        {
            baseColor = UiTheme.AccentColor;
            hoverColor = UiTheme.AccentHoverColor;
            pressColor = UiTheme.AccentPressedColor;
        }
        else
        {
            baseColor = UiTheme.SurfaceColor;
            hoverColor = UiTheme.AccentSoftColor;
            pressColor = Color.FromArgb(236, 215, 188);
        }

        var color = Blend(baseColor, hoverColor, hoverAmount);
        color = Blend(color, pressColor, pressAmount);

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPanel.CreateRoundedPath(bounds, cornerRadius);
        using var fill = new SolidBrush(color);
        g.FillPath(fill, path);

        if (kind == BrandButtonKind.Secondary)
        {
            using var pen = new Pen(UiTheme.BorderStrongColor);
            g.DrawPath(pen, path);
        }

        if (Focused)
        {
            using var focusPen = new Pen(UiTheme.AccentColor, 1.4f);
            var focusBounds = new Rectangle(2, 2, Width - 5, Height - 5);
            using var focusPath = RoundedPanel.CreateRoundedPath(focusBounds, Math.Max(4, cornerRadius - 2));
            g.DrawPath(focusPen, focusPath);
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            ClientRectangle,
            Enabled ? ForeColor : UiTheme.MutedTextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedPanel.CreateRoundedPath(new Rectangle(0, 0, Width, Height), cornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            stateAnimation.Dispose();
        }

        base.Dispose(disposing);
    }
}
