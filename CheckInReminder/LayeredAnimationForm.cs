using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CheckInReminder;

internal sealed class LayeredAnimationForm : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly ScreenEdge edge;
    private readonly double scale;

    public LayeredAnimationForm(Size sourceSize, ScreenEdge edge, double scale)
    {
        this.edge = edge;
        this.scale = scale;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = GetRenderedSize(sourceSize, edge, scale);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExLayered | WsExTransparent | WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    public static Size GetRenderedSize(Size sourceSize, ScreenEdge edge, double scale)
    {
        var scaled = new Size(
            Math.Max(1, (int)Math.Round(sourceSize.Width * scale)),
            Math.Max(1, (int)Math.Round(sourceSize.Height * scale)));
        return edge is ScreenEdge.Top or ScreenEdge.Bottom
            ? new Size(scaled.Height, scaled.Width)
            : scaled;
    }

    public void SetFrame(Bitmap source)
    {
        using var rendered = RenderFrame(source, edge, scale);
        LayeredWindowPresenter.Present(Handle, new Point(Left, Top), rendered);
    }

    private static Bitmap RenderFrame(Bitmap source, ScreenEdge edge, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        }

        result.RotateFlip(edge switch
        {
            ScreenEdge.Left => RotateFlipType.RotateNoneFlipX,
            ScreenEdge.Top => RotateFlipType.Rotate270FlipNone,
            ScreenEdge.Bottom => RotateFlipType.Rotate90FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone,
        });
        return result;
    }
}
