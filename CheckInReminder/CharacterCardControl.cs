using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>以角色为主体的画廊卡片，静止时展示完整封面，悬停时播放。</summary>
internal sealed class CharacterCardControl : UserControl
{
    private readonly AnimationPreviewPlayer player;
    private readonly CharacterPreviewImage preview;
    private readonly BrandButton confirmButton;
    private readonly Font nameFont = new("Microsoft YaHei UI", 11, FontStyle.Bold);
    private readonly Font detailFont = new("Microsoft YaHei UI", 8.5f);
    private bool confirmed;
    private bool hovered;

    public event EventHandler? Hovered;
    public event EventHandler? HoverEnded;
    public event EventHandler? Confirmed;
    public ReminderCharacter Character { get; }

    public CharacterCardControl(ReminderCharacter character, bool isConfirmed)
    {
        Character = character;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = UiTheme.WarmBackgroundColor;
        AccessibleName = $"角色 {character.DisplayName}";
        preview = new CharacterPreviewImage
        {
            AccessibleName = $"{character.DisplayName}动画预览",
            TabStop = false,
        };
        confirmButton = new BrandButton(BrandButtonKind.Secondary)
        {
            CornerRadius = 12,
            AccessibleName = $"使用角色 {character.DisplayName}",
            Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
        };
        confirmButton.Click += (_, _) => Confirmed?.Invoke(this, EventArgs.Empty);
        Controls.Add(preview);
        Controls.Add(confirmButton);
        player = new AnimationPreviewPlayer(preview);
        player.SetPaused(true);
        player.Load(character);
        SetConfirmed(isConfirmed);
        WireHover(this);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (preview is null) return;
        var unit = DeviceDpi / 96f;
        int S(int value) => (int)Math.Round(value * unit);
        preview.Bounds = new Rectangle(S(18), S(28), Math.Max(1, Width - S(36)), Math.Max(1, Height - S(88)));
        confirmButton.Bounds = new Rectangle(Width - S(99), Height - S(50), S(82), S(32));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int S(int value) => (int)Math.Round(value * DeviceDpi / 96f);
        var bounds = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));
        using var path = RoundedPanel.CreateRoundedPath(bounds, S(18));
        using var brush = new SolidBrush(StageColor());
        g.FillPath(brush, path);
        using var pen = new Pen(confirmed ? UiTheme.AccentColor : hovered ? UiTheme.BorderStrongColor : Color.FromArgb(226, 218, 207), confirmed ? S(2) : 1);
        g.DrawPath(pen, path);
        var textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, confirmed ? "●  当前使用" : "悬停预览", detailFont,
            new Rectangle(S(17), S(10), Width - S(34), S(20)),
            confirmed ? UiTheme.AccentColor : UiTheme.MutedTextColor, textFlags);
        TextRenderer.DrawText(g, Character.DisplayName, nameFont,
            new Rectangle(S(17), Height - S(52), Math.Max(1, Width - S(119)), S(25)), UiTheme.TextColor, textFlags);
        TextRenderer.DrawText(g, "提醒伙伴", detailFont,
            new Rectangle(S(17), Height - S(28), Width - S(119), S(17)), UiTheme.MutedTextColor, textFlags);
    }

    private Color StageColor() => Character.Id switch
    {
        "white-bear" => Color.FromArgb(239, 230, 216),
        "yellow-hippo" => Color.FromArgb(248, 232, 200),
        "blue-hat-cat" => Color.FromArgb(224, 236, 239),
        "stick-dog" => Color.FromArgb(239, 226, 218),
        "scooter-dinosaur" => Color.FromArgb(231, 237, 214),
        _ => UiTheme.AccentSoftColor,
    };

    public void SetPlaying(bool playing)
    {
        player.SetPaused(!playing);
        if (!playing) player.ShowPosterFrame();
    }

    public void SetConfirmed(bool isConfirmed)
    {
        confirmed = isConfirmed;
        confirmButton.Enabled = !isConfirmed;
        confirmButton.Text = isConfirmed ? "已选择 ✓" : "选用 →";
        Invalidate();
    }

    private void WireHover(Control control)
    {
        control.MouseEnter += (_, _) =>
        {
            hovered = true;
            Invalidate();
            Hovered?.Invoke(this, EventArgs.Empty);
        };
        control.MouseLeave += (_, _) =>
        {
            if (RectangleToScreen(ClientRectangle).Contains(Cursor.Position)) return;
            hovered = false;
            Invalidate();
            HoverEnded?.Invoke(this, EventArgs.Empty);
        };
        foreach (Control child in control.Controls) WireHover(child);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            player.Dispose();
            nameFont.Dispose();
            detailFont.Dispose();
            confirmButton.Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
