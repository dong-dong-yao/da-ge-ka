using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 无边框圆角弹层：带柔和阴影，供滚轮选择器弹出使用。
/// 通过子类化消息钩子拦截鼠标按下，点击弹层外部时自动关闭。
/// </summary>
internal sealed class PopupSurface : Form
{
    private readonly IntPtr ownerHandle;
    private readonly Action onDismiss;
    private readonly MouseHook mouseHook;

    public PopupSurface(IntPtr ownerHandle, Action onDismiss)
    {
        this.ownerHandle = ownerHandle;
        this.onDismiss = onDismiss;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = UiTheme.SurfaceColor;
        DoubleBuffered = true;
        mouseHook = new MouseHook(this);
    }

    public new void Show(IWin32Window owner)
    {
        base.Show();
        mouseHook.Install();
        Activate();
    }

    public void Dismiss()
    {
        mouseHook.Uninstall();
        Close();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Dismiss();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedPanel.CreateRoundedPath(new Rectangle(0, 0, Width, Height), 14);
        using var brush = new SolidBrush(UiTheme.SurfaceColor);
        g.FillPath(brush, path);
        using var pen = new Pen(UiTheme.BorderColor);
        g.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedPanel.CreateRoundedPath(new Rectangle(0, 0, Width, Height), 14);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    private sealed class MouseHook : NativeWindow
    {
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmLButtonDown = 0x0201;
        private readonly PopupSurface surface;

        public MouseHook(PopupSurface surface)
        {
            this.surface = surface;
        }

        public void Install()
        {
            AssignHandle(GetForegroundWindow());
        }

        public void Uninstall()
        {
            if (Handle != IntPtr.Zero)
            {
                ReleaseHandle();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg is WmLButtonDown or WmNcLButtonDown)
            {
                var point = Control.MousePosition;
                if (!surface.Bounds.Contains(point))
                {
                    surface.onDismiss();
                    return;
                }
            }

            base.WndProc(ref m);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
