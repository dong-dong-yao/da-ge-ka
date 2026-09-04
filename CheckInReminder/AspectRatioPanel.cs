namespace CheckInReminder;

/// <summary>在可用区域内居中放置唯一子控件，并始终保持指定宽高比。</summary>
internal sealed class AspectRatioPanel : Panel
{
    private readonly double aspectRatio;

    public AspectRatioPanel(double aspectRatio)
    {
        if (aspectRatio <= 0 || double.IsNaN(aspectRatio) || double.IsInfinity(aspectRatio))
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        }

        this.aspectRatio = aspectRatio;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
    }

    protected override void OnLayout(LayoutEventArgs eventArgs)
    {
        base.OnLayout(eventArgs);
        if (Controls.Count == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        var width = ClientSize.Width;
        var height = Math.Max(1, (int)Math.Round(width / aspectRatio));
        if (height > ClientSize.Height)
        {
            height = ClientSize.Height;
            width = Math.Max(1, (int)Math.Round(height * aspectRatio));
        }

        Controls[0].Bounds = new Rectangle(
            (ClientSize.Width - width) / 2,
            (ClientSize.Height - height) / 2,
            width,
            height);
    }
}
