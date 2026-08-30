using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal sealed class GradientHeaderPanel : Panel
{
    public GradientHeaderPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(
            ClientRectangle,
            UiTheme.BrandGoldColor,
            UiTheme.AccentColor,
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
