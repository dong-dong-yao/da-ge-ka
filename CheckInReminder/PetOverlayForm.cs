using System.Runtime.InteropServices;

namespace CheckInReminder;

/// <summary>
/// 桌面宠物窗口：透明、置顶、不抢焦点的无边框悬浮窗。
/// 默认鼠标穿透（点击穿过宠物落在后面的窗口上），托盘菜单可切换到拖动模式调整位置。
/// </summary>
internal sealed class PetOverlayForm : Form, IPetFrameSink
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int GwlExStyle = -20;
    private const int WmNcHitTest = 0x0084;
    private const int HtCaption = 0x0002;
    private const int DefaultMarginRight = 24;
    private const int DefaultMarginBottom = 12;

    private readonly int targetHeightDip;
    private Bitmap? currentSource;
    private bool clickThrough = true;
    private bool positioned;

    public PetOverlayForm(int targetHeightDip = 140)
    {
        this.targetHeightDip = targetHeightDip;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
    }

    protected override bool ShowWithoutActivation => true;

    public bool ClickThrough => clickThrough;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExLayered | WsExNoActivate | WsExToolWindow;
            if (clickThrough)
            {
                parameters.ExStyle |= WsExTransparent;
            }

            return parameters;
        }
    }

    /// <summary>切换鼠标穿透：true 时点击穿过宠物；false 时宠物可拖动换位。</summary>
    public void SetClickThrough(bool value)
    {
        if (clickThrough == value)
        {
            return;
        }

        clickThrough = value;
        if (!IsHandleCreated)
        {
            return;
        }

        var style = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
        style = value ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLongPtr(Handle, GwlExStyle, new IntPtr(style));
    }

    /// <summary>呈现一帧（按目标高度等比缩放，带 DPI 换算）。位图所有权归调用方。</summary>
    public void SetFrame(Bitmap source)
    {
        currentSource = source;
        using var rendered = RenderScaled(source);
        if (Size != rendered.Size)
        {
            Size = rendered.Size;
        }

        if (!positioned)
        {
            positioned = true;
            PositionBottomRight();
        }

        if (!IsHandleCreated)
        {
            _ = Handle;
        }

        LayeredWindowPresenter.Present(Handle, Location, rendered);
    }

    protected override void WndProc(ref Message message)
    {
        // 非穿透（调整位置）模式下，整个窗口体可像标题栏一样拖动
        if (message.Msg == WmNcHitTest && !clickThrough)
        {
            message.Result = new IntPtr(HtCaption);
            return;
        }

        base.WndProc(ref message);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (currentSource is not null && IsHandleCreated)
        {
            SetFrame(currentSource);
        }
    }

    private Bitmap RenderScaled(Bitmap source)
    {
        var scale = targetHeightDip * (DeviceDpi / 96.0) / source.Height;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        }

        return result;
    }

    private void PositionBottomRight()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromHandle(Handle).WorkingArea;
        Location = new Point(
            workingArea.Right - Width - DefaultMarginRight,
            workingArea.Bottom - Height - DefaultMarginBottom);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue);
}
