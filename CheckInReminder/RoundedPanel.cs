using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal sealed class RoundedPanel : Panel
{
    [DefaultValue(26)]
    public int CornerRadius { get; set; } = 26;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = UiTheme.SurfaceColor;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OutlineColor { get; set; } = UiTheme.BorderColor;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowOutline { get; set; } = true;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        ResizeRedraw = true;
        Padding = new Padding(20);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var shadowBounds = new Rectangle(5, 6, Math.Max(1, Width - 11), Math.Max(1, Height - 11));
        using var shadowPath = CreateRoundedPath(shadowBounds, CornerRadius);
        using var shadowBrush = new SolidBrush(UiTheme.ShadowColor);
        e.Graphics.FillPath(shadowBrush, shadowPath);

        var cardBounds = new Rectangle(2, 1, Math.Max(1, Width - 10), Math.Max(1, Height - 9));
        using var cardPath = CreateRoundedPath(cardBounds, CornerRadius);
        using var fillBrush = new SolidBrush(SurfaceColor);
        e.Graphics.FillPath(fillBrush, cardPath);
        if (ShowOutline)
        {
            using var borderPen = new Pen(OutlineColor);
            e.Graphics.DrawPath(borderPen, cardPath);
        }
    }

    internal static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
