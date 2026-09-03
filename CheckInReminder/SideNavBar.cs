using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>
/// 设置窗口左侧的垂直导航栏：圆角选中底 + 左侧指示条 + 图标文字，悬停有柔和过渡。
/// </summary>
internal sealed class SideNavBar : Panel
{
    public sealed record NavItem(string Key, string Text, IconBadge.IconKind Icon);

    private const int ItemHeight = 46;
    private readonly Dictionary<string, SideNavItem> items = new(StringComparer.Ordinal);

    public event EventHandler<string>? NavigationRequested;

    public string SelectedKey { get; private set; }

    public SideNavBar(IReadOnlyList<NavItem> navigationItems, string initialKey)
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(12, 16, 8, 12);
        SelectedKey = initialKey;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = navigationItems.Count,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < navigationItems.Count; index++)
        {
            var item = navigationItems[index];
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ItemHeight));
            var view = new SideNavItem(item)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Selected = string.Equals(item.Key, initialKey, StringComparison.Ordinal),
            };
            view.Navigate += (_, _) => SelectPage(item.Key);
            items[item.Key] = view;
            layout.Controls.Add(view, 0, index);
        }

        Controls.Add(layout);
    }

    public void SelectPage(string key)
    {
        if (!items.TryGetValue(key, out var target) ||
            string.Equals(SelectedKey, key, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var (itemKey, view) in items)
        {
            view.Selected = string.Equals(itemKey, key, StringComparison.Ordinal);
        }

        SelectedKey = key;
        NavigationRequested?.Invoke(this, key);
    }

    private sealed class SideNavItem : Panel
    {
        private readonly NavItem item;
        private readonly IconBadge icon;
        private readonly AnimationHelper hoverAnimation;
        private float hoverAmount;
        private bool selected;
        private bool pressed;

        public event EventHandler? Navigate;

        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                {
                    return;
                }

                selected = value;
                Invalidate();
            }
        }

        public SideNavItem(NavItem item)
        {
            this.item = item;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = item.Text;
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f, FontStyle.Bold);

            icon = new IconBadge
            {
                Kind = item.Icon,
                Size = new Size(26, 26),
                Location = new Point(14, (ItemHeight - 26) / 2),
                Enabled = false,
            };
            Controls.Add(icon);

            hoverAnimation = new AnimationHelper(v =>
            {
                hoverAmount = v;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hoverAnimation.Start(hoverAmount, 1f);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            pressed = false;
            hoverAnimation.Start(hoverAmount, 0f);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Focus();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var wasPressed = pressed;
            pressed = false;
            Invalidate();
            if (wasPressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
            {
                Navigate?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Navigate?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            if (selected)
            {
                using var path = RoundedPanel.CreateRoundedPath(bounds, 14);
                using var fill = new SolidBrush(UiTheme.AccentSoftColor);
                g.FillPath(fill, path);
                using var indicatorBrush = new SolidBrush(UiTheme.AccentColor);
                using var indicatorPath = RoundedPanel.CreateRoundedPath(new Rectangle(4, 10, 4, Height - 21), 2);
                g.FillPath(indicatorBrush, indicatorPath);
            }
            else
            {
                var hoverColor = Color.FromArgb(
                    (int)(UiTheme.AccentSoftColor.A * hoverAmount * (pressed ? 1f : 0.6f)),
                    UiTheme.AccentSoftColor);
                if (hoverColor.A > 0)
                {
                    using var path = RoundedPanel.CreateRoundedPath(bounds, 14);
                    using var fill = new SolidBrush(hoverColor);
                    g.FillPath(fill, path);
                }
            }

            if (Focused)
            {
                using var focusPen = new Pen(UiTheme.AccentColor, 1.2f);
                var focusBounds = new Rectangle(2, 2, Width - 5, Height - 5);
                using var focusPath = RoundedPanel.CreateRoundedPath(focusBounds, 12);
                g.DrawPath(focusPen, focusPath);
            }

            TextRenderer.DrawText(
                g,
                item.Text,
                Font,
                new Rectangle(50, 0, Width - 56, Height),
                selected ? UiTheme.AccentColor : UiTheme.TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hoverAnimation.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
