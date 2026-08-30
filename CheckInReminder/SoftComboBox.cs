using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 暖色胶囊选择控件：纯自绘，不含系统 ComboBox。
/// 点击弹出码表式滚轮列表，可滚轮滚动、拖动、点击跳转，松手缓动吸附。
/// </summary>
internal sealed class SoftComboBox : Control
{
    private readonly List<string> items = [];
    private int selectedIndex = -1;
    private string text = string.Empty;
    private float highlight;
    private bool hovered;
    private readonly AnimationHelper highlightAnimation;
    private PopupSurface? popup;

    public SoftComboBox()
    {
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

    public IList<string> Items => items;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Text
    {
        get => text;
        set
        {
            if (text == value)
            {
                return;
            }

            text = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (selectedIndex == value)
            {
                return;
            }

            selectedIndex = value;
            text = value >= 0 && value < items.Count ? items[value] : string.Empty;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

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
        if (popup is not null || items.Count == 0)
        {
            return;
        }

        var wheel = new WheelPicker { Dock = DockStyle.Fill };
        wheel.SetItems(items, Math.Max(0, selectedIndex));

        popup = new PopupSurface(Handle, () => ClosePopup())
        {
            Size = new Size(Math.Max(160, Width + 22), 196),
        };
        popup.Controls.Add(wheel);

        wheel.SelectedIndexChanged += (_, _) =>
        {
            SelectedIndex = wheel.SelectedIndex;
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

    private void ClosePopup()
    {
        if (popup is null)
        {
            return;
        }

        var closing = popup;
        popup = null;
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

        // 文本
        using var textBrush = new SolidBrush(UiTheme.TextColor);
        var textSize = g.MeasureString(text, Font);
        g.DrawString(text, Font, textBrush, 16, (Height - textSize.Height) / 2f);

        // 右侧箭头
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
}
