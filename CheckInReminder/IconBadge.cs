using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 卡片标题前的暖色圆形小图标徽章，用几何图形绘制，给设置卡片提供柔和的识别符号。
/// </summary>
internal sealed class IconBadge : Control
{
    public enum IconKind
    {
        Sun,
        Moon,
        Chair,
        Power,
        Gear,
        Paw,
    }

    [DefaultValue(IconKind.Sun)]
    public IconKind Kind { get; set; } = IconKind.Sun;

    public IconBadge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(30, 30);
        BackColor = Color.Transparent;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using (var fill = new SolidBrush(UiTheme.AccentSoftColor))
        {
            g.FillEllipse(fill, bounds);
        }

        var iconColor = UiTheme.AccentColor;
        using var pen = new Pen(iconColor, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        using var brush = new SolidBrush(iconColor);

        var cx = Width / 2f;
        var cy = Height / 2f;

        switch (Kind)
        {
            case IconKind.Sun:
                g.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Math.PI / 4;
                    var x1 = cx + (float)(Math.Cos(angle) * 7.5f);
                    var y1 = cy + (float)(Math.Sin(angle) * 7.5f);
                    var x2 = cx + (float)(Math.Cos(angle) * 9.5f);
                    var y2 = cy + (float)(Math.Sin(angle) * 9.5f);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;

            case IconKind.Moon:
                using (var moonPath = new GraphicsPath())
                {
                    moonPath.AddArc(cx - 7, cy - 7, 14, 14, 60, 240);
                    moonPath.AddArc(cx - 2, cy - 5.6f, 11, 11, 250, -190);
                    moonPath.CloseFigure();
                    g.FillPath(brush, moonPath);
                }
                break;

            case IconKind.Chair:
                // 椅背
                g.DrawLine(pen, cx - 5, cy - 7, cx - 5, cy + 1);
                // 椅座
                g.DrawLine(pen, cx - 5, cy + 1, cx + 5, cy + 1);
                // 椅腿
                g.DrawLine(pen, cx - 4, cy + 1, cx - 4, cy + 7);
                g.DrawLine(pen, cx + 4, cy + 1, cx + 4, cy + 7);
                // 活动小点
                g.FillEllipse(brush, cx + 6, cy - 6, 3, 3);
                break;

            case IconKind.Power:
                g.DrawArc(pen, cx - 6, cy - 5, 12, 12, -60, 300);
                g.DrawLine(pen, cx, cy - 7, cx, cy + 1);
                break;

            case IconKind.Gear:
                // 齿轮外圈
                using (var ringPen = new Pen(iconColor, 2.2f))
                {
                    g.DrawEllipse(ringPen, cx - 4.5f, cy - 4.5f, 9, 9);
                }
                // 六个齿
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * Math.PI / 3;
                    var x1 = cx + (float)(Math.Cos(angle) * 6.8f);
                    var y1 = cy + (float)(Math.Sin(angle) * 6.8f);
                    var x2 = cx + (float)(Math.Cos(angle) * 9.2f);
                    var y2 = cy + (float)(Math.Sin(angle) * 9.2f);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;

            case IconKind.Paw:
                // 主掌垫
                g.FillEllipse(brush, cx - 4.2f, cy - 0.5f, 8.4f, 7.2f);
                // 三个脚趾
                g.FillEllipse(brush, cx - 6.4f, cy - 6.2f, 3.4f, 3.8f);
                g.FillEllipse(brush, cx - 1.7f, cy - 7.6f, 3.4f, 3.8f);
                g.FillEllipse(brush, cx + 3f, cy - 6.2f, 3.4f, 3.8f);
                break;
        }
    }
}
