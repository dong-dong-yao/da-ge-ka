using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CheckInReminder;

internal sealed class LayeredAnimationForm : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int UlwAlpha = 0x00000002;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
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
        Present(rendered);
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

    private void Present(Bitmap bitmap)
    {
        var screenDc = IntPtr.Zero;
        var memoryDc = IntPtr.Zero;
        var hBitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        var bitmapIsSelected = false;
        try
        {
            screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            if (hBitmap == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            previous = SelectObject(memoryDc, hBitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            bitmapIsSelected = true;

            var destination = new NativePoint(Left, Top);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var source = new NativePoint(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha,
            };
            if (!UpdateLayeredWindow(
                Handle,
                screenDc,
                ref destination,
                ref size,
                memoryDc,
                ref source,
                0,
                ref blend,
                UlwAlpha))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (bitmapIsSelected)
            {
                SelectObject(memoryDc, previous);
            }
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }
            if (screenDc != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int width, int height)
    {
        public int Width = width;
        public int Height = height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr window,
        IntPtr destinationDc,
        ref NativePoint destination,
        ref NativeSize size,
        IntPtr sourceDc,
        ref NativePoint source,
        int colorKey,
        ref BlendFunction blend,
        int flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr graphicObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr graphicObject);
}
