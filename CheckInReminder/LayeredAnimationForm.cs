using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CheckInReminder;

internal sealed class LayeredAnimationForm : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly ReminderFrameTransform transform;
    private readonly double scale;

    public LayeredAnimationForm(Size sourceSize, ScreenEdge sourceEdge, ScreenEdge targetEdge, double scale)
    {
        transform = ReminderFrameTransformResolver.Resolve(sourceEdge, targetEdge);
        this.scale = scale;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = GetRenderedSize(sourceSize, transform, scale);
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

    public static Size GetRenderedSize(Size sourceSize, ReminderFrameTransform transform, double scale)
    {
        var scaled = new Size(
            Math.Max(1, (int)Math.Round(sourceSize.Width * scale)),
            Math.Max(1, (int)Math.Round(sourceSize.Height * scale)));
        return ReminderFrameTransformResolver.SwapsAxes(transform)
            ? new Size(scaled.Height, scaled.Width)
            : scaled;
    }

    public void SetFrame(Bitmap source)
    {
        using var rendered = RenderFrame(source, transform, scale);
        LayeredWindowPresenter.Present(Handle, new Point(Left, Top), rendered);
    }

    private static Bitmap RenderFrame(Bitmap source, ReminderFrameTransform transform, double scale)
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

        result.RotateFlip(transform switch
        {
            ReminderFrameTransform.FlipHorizontal => RotateFlipType.RotateNoneFlipX,
            ReminderFrameTransform.FlipVertical => RotateFlipType.RotateNoneFlipY,
            ReminderFrameTransform.RotateClockwise => RotateFlipType.Rotate90FlipNone,
            ReminderFrameTransform.RotateCounterClockwise => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone,
        });
        return result;
    }
}
