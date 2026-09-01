using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 自测量卡片头：图标 + 标题 + 副标题，整体高度由两段文字的真实渲染高度决定，
/// 不存在固定高度盒子的概念，因此标题/副标题永远不可能被裁切。
/// </summary>
internal sealed class CardHeader : Control
{
    private readonly string title;
    private readonly string subtitle;
    private readonly Font titleFont;
    private readonly Font subtitleFont;
    private readonly SizeF titleSize;
    private readonly SizeF subtitleSize;
    private readonly Bitmap badgeBitmap;
    private readonly SolidBrush titleBrush;
    private readonly SolidBrush subtitleBrush;

    public CardHeader(IconBadge.IconKind icon, string title, string subtitle)
    {
        this.title = title;
        this.subtitle = subtitle;
        var family = (SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily;
        titleFont = new Font(family, 11f, FontStyle.Bold);
        subtitleFont = new Font(family, 8.5f);
        using (var graphics = CreateGraphics())
        {
            titleSize = graphics.MeasureString(title, titleFont);
            subtitleSize = string.IsNullOrEmpty(subtitle)
                ? SizeF.Empty
                : graphics.MeasureString(subtitle, subtitleFont);
        }

        badgeBitmap = new Bitmap(30, 30);
        using (var badge = new IconBadge { Kind = icon, Size = new Size(30, 30) })
        {
            badge.DrawToBitmap(badgeBitmap, new Rectangle(0, 0, 30, 30));
        }

        titleBrush = new SolidBrush(UiTheme.TextColor);
        subtitleBrush = new SolidBrush(UiTheme.MutedTextColor);
        SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = false;
        Dock = DockStyle.Top;
        Height = MeasureContentHeight();
    }

    private int MeasureContentHeight()
    {
        // 图标与标题垂直居中于标题行，副标题紧随其后
        var contentHeight = (int)Math.Ceiling(Math.Max(30, titleSize.Height) + (subtitleSize.Height > 0 ? 4 + subtitleSize.Height : 0));
        return contentHeight + 8; // 上下各留 4px 呼吸
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var titleHeight = Math.Max(30, titleSize.Height);
        var totalContent = titleHeight + (subtitleSize.Height > 0 ? 4 + subtitleSize.Height : 0);
        var top = (Height - totalContent) / 2f;

        // 图标：与标题行垂直居中对齐
        var iconTop = (int)(top + ((titleHeight - 30) / 2f));
        g.DrawImage(badgeBitmap, 0, iconTop);
        g.DrawString(title, titleFont, titleBrush, 38, top + ((titleHeight - titleSize.Height) / 2f));

        if (subtitleSize.Height > 0)
        {
            g.DrawString(subtitle, subtitleFont, subtitleBrush, 38, top + titleHeight + 4);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            subtitleFont.Dispose();
            badgeBitmap.Dispose();
            titleBrush.Dispose();
            subtitleBrush.Dispose();
        }

        base.Dispose(disposing);
    }
}
