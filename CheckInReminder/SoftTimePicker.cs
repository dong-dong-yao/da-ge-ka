using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 暖色胶囊时间选择控件：纯自绘，不含任何系统控件。
/// 点击后弹出码表式双列滚轮（小时/分钟），可滚轮滚动、拖动、点击跳转，松手缓动吸附。
/// </summary>
internal sealed class SoftTimePicker : Control
{
    private TimeOnly value;
    private float highlight;
    private bool hovered;
    private readonly AnimationHelper highlightAnimation;
    private PopupSurface? popup;

    public SoftTimePicker(TimeOnly initial)
    {
        value = initial;
        SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable, true);
        Size = new Size(138, 38);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.ComboBox;

        highlightAnimation = new AnimationHelper(v =>
        {
            highlight = v;
            Invalidate();
        });
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeOnly Value
    {
        get => value;
        set
        {
            if (this.value == value)
            {
                return;
            }

            this.value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    public void Pulse()
    {
        highlightAnimation.Start(1f, 0f, 450);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowPopup();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            ShowPopup();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void ShowPopup()
    {
        if (popup is not null)
        {
            return;
        }

        var hourItems = Enumerable.Range(0, 24).Select(h => h.ToString("D2")).ToArray();
        var minuteItems = Enumerable.Range(0, 60).Select(m => m.ToString("D2")).ToArray();

        var hourWheel = new WheelPicker { Dock = DockStyle.Fill };
        var minuteWheel = new WheelPicker { Dock = DockStyle.Fill };
        hourWheel.SetItems(hourItems, value.Hour);
        minuteWheel.SetItems(minuteItems, value.Minute);

        var colon = new ColonSeparator { Dock = DockStyle.Fill };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = UiTheme.SurfaceColor,
            Padding = new Padding(8, 6, 8, 6),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.Controls.Add(hourWheel, 0, 0);
        layout.Controls.Add(colon, 1, 0);
        layout.Controls.Add(minuteWheel, 2, 0);

        popup = new PopupSurface(Handle, () => ClosePopup(commit: true))
        {
            Size = new Size(196, 214),
        };
        popup.Controls.Add(layout);

        hourWheel.SelectedIndexChanged += (_, _) =>
        {
            value = new TimeOnly(hourWheel.SelectedIndex, value.Minute);
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        minuteWheel.SelectedIndexChanged += (_, _) =>
        {
            value = new TimeOnly(value.Hour, minuteWheel.SelectedIndex);
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };

        popup.FormClosed += (_, _) =>
        {
            popup = null;
            Pulse();
            Invalidate();
        };

        var screen = PointToScreen(new Point(Width - popup.Width, Height + 6));
        popup.Location = ClampToScreen(screen, popup.Size);
        popup.Show(this);
    }

    private void ClosePopup(bool commit)
    {
        if (popup is null)
        {
            return;
        }

        var closing = popup;
        popup = null;
        closing.FormClosed -= null;
        closing.Dismiss();
        Pulse();
    }

    private static Point ClampToScreen(Point location, Size size)
    {
        var area = Screen.FromPoint(location).WorkingArea;
        var x = Math.Clamp(location.X, area.Left + 4, area.Right - size.Width - 4);
        var y = location.Y + size.Height > area.Bottom
            ? Math.Max(area.Top + 4, location.Y - size.Height - 44)
            : location.Y;
        return new Point(x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var parentBack = Parent?.BackColor ?? UiTheme.WarmBackgroundColor;
        using (var bg = new SolidBrush(parentBack))
        {
            g.FillRectangle(bg, ClientRectangle);
        }

        var bounds = new Rectangle(1, 1, Width - 2, Height - 2);
        using var path = RoundedPanel.CreateRoundedPath(bounds, Height / 2);

        var baseColor = UiTheme.SurfaceColor;
        if (highlight > 0.01f)
        {
            baseColor = Blend(baseColor, UiTheme.AccentSoftColor, highlight);
        }

        using (var fill = new SolidBrush(baseColor))
        {
            g.FillPath(fill, path);
        }

        var border = UiTheme.BorderColor;
        if (popup is not null || Focused)
        {
            border = UiTheme.AccentColor;
        }
        else if (hovered)
        {
            border = UiTheme.BorderStrongColor;
        }

        using (var pen = new Pen(border, border == UiTheme.AccentColor ? 1.6f : 1f))
        {
            g.DrawPath(pen, path);
        }

        // 时间文字
        var text = value.ToString("HH:mm");
        using var textBrush = new SolidBrush(UiTheme.TextColor);
        var textSize = g.MeasureString(text, Font);
        g.DrawString(text, Font, textBrush, 16, (Height - textSize.Height) / 2f);

        // 右侧小箭头
        var arrowColor = hovered || popup is not null ? UiTheme.AccentColor : UiTheme.MutedTextColor;
        using var arrowPen = new Pen(arrowColor, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        var cx = Width - 18;
        var cy = Height / 2f;
        g.DrawLine(arrowPen, cx - 4, cy - 2, cx, cy + 2);
        g.DrawLine(arrowPen, cx, cy + 2, cx + 4, cy - 2);
    }

    private static Color Blend(Color a, Color b, float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            255,
            (int)(a.R + ((b.R - a.R) * clamped)),
            (int)(a.G + ((b.G - a.G) * clamped)),
            (int)(a.B + ((b.B - a.B) * clamped)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            highlightAnimation.Dispose();
            popup?.Dismiss();
        }

        base.Dispose(disposing);
    }

    /// <summary>弹层中小时与分钟之间的自绘冒号分隔符。</summary>
    private sealed class ColonSeparator : Control
    {
        public ColonSeparator()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = UiTheme.SurfaceColor;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using var bg = new SolidBrush(UiTheme.SurfaceColor);
            g.FillRectangle(bg, ClientRectangle);
            using var brush = new SolidBrush(UiTheme.MutedTextColor);
            var cx = Width / 2f;
            var cy = Height / 2f;
            g.FillEllipse(brush, cx - 2.5f, cy - 9, 5, 5);
            g.FillEllipse(brush, cx - 2.5f, cy + 4, 5, 5);
        }
    }
}
