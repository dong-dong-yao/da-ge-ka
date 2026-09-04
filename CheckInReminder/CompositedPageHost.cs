namespace CheckInReminder;

/// <summary>
/// 页面宿主：由 Windows 在离屏缓冲区中按从底到顶的顺序绘制所有子窗口，
/// 再一次性呈现，避免缩放和页面切换时暴露半完成的布局。
/// </summary>
internal sealed class CompositedPageHost : Panel
{
    private const int WsExComposited = 0x02000000;

    public CompositedPageHost()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExComposited;
            return parameters;
        }
    }

    public void ShowOnly(Control target, params Control[] pages)
    {
        SuspendLayout();
        try
        {
            target.Visible = true;
            target.BringToFront();
            foreach (var page in pages)
            {
                if (!ReferenceEquals(page, target))
                {
                    page.Visible = false;
                }
            }
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }

        Invalidate(invalidateChildren: true);
    }
}
