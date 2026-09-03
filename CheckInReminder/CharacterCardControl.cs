namespace CheckInReminder;

/// <summary>
/// 角色页网格中的一张角色卡：动画预览（悬停时播放）+ 名称 + 确认按钮。
/// 非活动卡片停在第 0 帧静帧，避免多个动画 Timer 同时运行。
/// </summary>
internal sealed class CharacterCardControl : UserControl
{
    private readonly AnimationPreviewPlayer player;
    private readonly Label statusLabel;
    private readonly BrandButton confirmButton;

    public event EventHandler? Hovered;

    public event EventHandler? Confirmed;

    public ReminderCharacter Character { get; }

    public CharacterCardControl(ReminderCharacter character, bool isConfirmed)
    {
        Character = character;
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Margin = new Padding(4);
        AccessibleName = $"角色 {character.DisplayName}";

        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            ShowOutline = false,
            Padding = new Padding(14),
        };

        var previewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = UiTheme.AccentSoftColor,
            Margin = Padding.Empty,
        };
        var previewShell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.AccentSoftColor,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10),
        };
        previewShell.Controls.Add(previewBox);

        var nameRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nameRow.Controls.Add(new Label
        {
            Text = character.DisplayName,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 12, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
        }, 0, 0);
        statusLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9.5f, FontStyle.Bold),
            ForeColor = UiTheme.AccentColor,
            BackColor = Color.Transparent,
        };
        nameRow.Controls.Add(statusLabel, 1, 0);

        confirmButton = new BrandButton
        {
            Text = "使用这个角色",
            Dock = DockStyle.Fill,
            CornerRadius = 16,
            AccessibleName = $"使用角色 {character.DisplayName}",
            Margin = new Padding(0, 8, 0, 0),
        };
        confirmButton.Click += (_, _) => Confirmed?.Invoke(this, EventArgs.Empty);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.Controls.Add(previewShell, 0, 0);
        layout.Controls.Add(nameRow, 0, 1);
        layout.Controls.Add(confirmButton, 0, 2);
        card.Controls.Add(layout);
        Controls.Add(card);

        player = new AnimationPreviewPlayer(previewBox);
        player.SetPaused(true);
        player.Load(character);
        SetConfirmed(isConfirmed);

        WireHover(this);
    }

    /// <summary>播放或停帧：playing=false 时回到第 0 帧静帧。</summary>
    public void SetPlaying(bool playing)
    {
        if (playing)
        {
            player.SetPaused(false);
        }
        else
        {
            player.SetPaused(true);
            player.ShowFirstFrame();
        }
    }

    public void SetConfirmed(bool isConfirmed)
    {
        statusLabel.Text = isConfirmed ? "使用中 ✓" : string.Empty;
        confirmButton.Enabled = !isConfirmed;
        confirmButton.Text = isConfirmed ? "当前使用" : "使用这个角色";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            player.Dispose();
        }

        base.Dispose(disposing);
    }

    private void WireHover(Control control)
    {
        control.MouseEnter += (_, _) => Hovered?.Invoke(this, EventArgs.Empty);
        foreach (Control child in control.Controls)
        {
            WireHover(child);
        }
    }
}
