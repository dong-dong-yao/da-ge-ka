using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 码表式滚轮选择器：单列纵向滚动，支持鼠标滚轮与拖动，
/// 松手后带 EaseOutCubic 缓动吸附到最近项，选中项放大加深，上下渐隐。
/// </summary>
internal sealed class WheelPicker : Control
{
    private const int ItemHeight = 38;

    private readonly List<string> items = [];
    private float scroll;
    private bool dragging;
    private int lastDragY;
    private bool moved;
    private float snapFrom;
    private float snapTo;
    private int snapElapsed;
    private readonly System.Windows.Forms.Timer snapTimer;

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex { get; private set; }

    public WheelPicker()
    {
        SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        BackColor = UiTheme.SurfaceColor;
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 12f, FontStyle.Bold);
        snapTimer = new System.Windows.Forms.Timer { Interval = 15 };
        snapTimer.Tick += (_, _) => SnapStep();
    }

    public void SetItems(IEnumerable<string> values, int selectedIndex)
    {
        items.Clear();
        items.AddRange(values);
        SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, items.Count - 1));
        scroll = SelectedIndex;
        snapTimer.Stop();
        Invalidate();
    }

    protected override Size DefaultSize => new(84, ItemHeight * 5);

    private float MaxScroll => Math.Max(0, items.Count - 1);

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        snapTimer.Stop();
        scroll = Math.Clamp(scroll - (e.Delta / 120f * 0.42f), 0, MaxScroll);
        Invalidate();
        if (Math.Abs(scroll - Math.Round(scroll)) < 0.001f)
        {
            CommitIndex((int)Math.Round(scroll));
        }
        else
        {
            BeginSnap();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        dragging = true;
        moved = false;
        lastDragY = e.Y;
        snapTimer.Stop();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging)
        {
            return;
        }

        var delta = e.Y - lastDragY;
        if (Math.Abs(delta) > 2)
        {
            moved = true;
        }

        lastDragY = e.Y;
        scroll = Math.Clamp(scroll - (delta / (float)ItemHeight), 0, MaxScroll);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!dragging)
        {
            return;
        }

        dragging = false;
        if (!moved)
        {
            var centerY = Height / 2f;
            var tapped = (int)Math.Round(scroll + ((e.Y - centerY) / (float)ItemHeight));
            tapped = Math.Clamp(tapped, 0, items.Count - 1);
            BeginSnapTo(tapped);
            return;
        }

        BeginSnap();
    }

    private void BeginSnap() => BeginSnapTo((int)Math.Round(scroll));

    private void BeginSnapTo(int index)
    {
        snapFrom = scroll;
        snapTo = Math.Clamp(index, 0, items.Count - 1);
        snapElapsed = 0;
        snapTimer.Start();
    }

    private void SnapStep()
    {
        snapElapsed += 15;
        var t = Math.Min(1f, snapElapsed / 220f);
        var eased = 1f - MathF.Pow(1f - t, 3f);
        scroll = snapFrom + ((snapTo - snapFrom) * eased);
        Invalidate();
        if (t >= 1f)
        {
            snapTimer.Stop();
            scroll = snapTo;
            CommitIndex((int)snapTo);
        }
    }

    private void CommitIndex(int index)
    {
        index = Math.Clamp(index, 0, items.Count - 1);
        if (index == SelectedIndex)
        {
            return;
        }

        SelectedIndex = index;
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var bg = new SolidBrush(UiTheme.SurfaceColor))
        {
            g.FillRectangle(bg, ClientRectangle);
        }

        var centerY = Height / 2f;
        var state = g.Save();

        // 选中行高亮胶囊
        var highlight = new Rectangle(3, (int)(centerY - (ItemHeight / 2f)), Width - 6, ItemHeight);
        using (var highlightPath = RoundedPanel.CreateRoundedPath(highlight, 12))
        using (var highlightBrush = new SolidBrush(UiTheme.AccentSoftColor))
        {
            g.FillPath(highlightBrush, highlightPath);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var itemCenterY = centerY + ((i - scroll) * ItemHeight);
            if (itemCenterY < -ItemHeight || itemCenterY > Height + ItemHeight)
            {
                continue;
            }

            var distance = Math.Abs(itemCenterY - centerY) / ItemHeight;
            var scale = Math.Clamp(1.12f - (distance * 0.16f), 0.86f, 1.12f);
            var alpha = (int)Math.Clamp(255 - (distance * 150), 48, 255);
            var color = Color.FromArgb(alpha, distance < 0.5f ? UiTheme.TextColor : UiTheme.MutedTextColor);

            using var font = new Font(Font.FontFamily, Font.Size * scale, distance < 0.5f ? FontStyle.Bold : FontStyle.Regular);
            var textSize = g.MeasureString(items[i], font);
            g.TranslateTransform(Width / 2f, itemCenterY);
            using var brush = new SolidBrush(color);
            g.DrawString(items[i], font, brush, -textSize.Width / 2f, -textSize.Height / 2f);
            g.Restore(state);
            state = g.Save();
        }

        g.Restore(state);

        // 上下渐隐遮罩
        using (var fadeTop = new LinearGradientBrush(
            new Rectangle(0, 0, Width, ItemHeight),
            UiTheme.SurfaceColor,
            Color.FromArgb(0, UiTheme.SurfaceColor),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(fadeTop, 0, 0, Width, ItemHeight);
        }

        using (var fadeBottom = new LinearGradientBrush(
            new Rectangle(0, Height - ItemHeight, Width, ItemHeight),
            Color.FromArgb(0, UiTheme.SurfaceColor),
            UiTheme.SurfaceColor,
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(fadeBottom, 0, Height - ItemHeight, Width, ItemHeight);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            snapTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
