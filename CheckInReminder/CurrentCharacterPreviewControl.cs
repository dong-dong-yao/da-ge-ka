namespace CheckInReminder;

/// <summary>
/// 设置页右列的只读预览卡：循环播放当前确认角色的提醒动画，
/// 点击「更换角色 →」跳转到角色页。
/// </summary>
internal sealed class CurrentCharacterPreviewControl : UserControl
{
    private readonly AnimationPreviewPlayer player;
    private readonly PictureBox previewBox;
    private readonly Label nameLabel;
    private readonly Label statusLabel;

    public event EventHandler? ChangeCharacterRequested;

    public string CurrentCharacterId { get; private set; }

    public CurrentCharacterPreviewControl(string? initialCharacterId)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Padding = new Padding(4);

        var character = AnimationCatalog.FindCharacter(initialCharacterId)
            ?? AnimationCatalog.Characters[0];
        CurrentCharacterId = character.Id;

        var title = new Label
        {
            Text = "当前角色",
            AutoSize = true,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 14, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 2),
        };
        var subtitle = new Label
        {
            Text = "提醒时会播放这个角色的动画",
            AutoSize = true,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 14),
        };

        previewBox = new PictureBox
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
            Padding = new Padding(12),
            Margin = new Padding(0, 2, 0, 14),
        };
        previewShell.Controls.Add(previewBox);

        nameLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 14, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
        };
        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.AccentColor,
            BackColor = Color.Transparent,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9.5f, FontStyle.Bold),
            Text = "使用中 ✓",
        };
        var changeButton = new BrandButton(BrandButtonKind.Secondary)
        {
            Text = "更换角色 →",
            Dock = DockStyle.Fill,
            CornerRadius = 18,
            AccessibleName = "跳转到角色页面更换角色",
            Margin = new Padding(0, 8, 0, 0),
        };
        changeButton.Click += (_, _) => ChangeCharacterRequested?.Invoke(this, EventArgs.Empty);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent,
            Padding = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(previewShell, 0, 2);
        layout.Controls.Add(nameLabel, 0, 3);
        layout.Controls.Add(statusLabel, 0, 4);
        layout.Controls.Add(changeButton, 0, 5);
        Controls.Add(layout);

        player = new AnimationPreviewPlayer(previewBox);
        nameLabel.Text = character.DisplayName;
        player.Load(character);
    }

    /// <summary>角色页确认新角色后由 SettingsForm 调用，切换到该角色的预览。</summary>
    public void SetCharacter(string characterId)
    {
        if (string.Equals(CurrentCharacterId, characterId, StringComparison.Ordinal))
        {
            return;
        }

        var character = AnimationCatalog.FindCharacter(characterId);
        if (character is null)
        {
            return;
        }

        CurrentCharacterId = character.Id;
        nameLabel.Text = character.DisplayName;
        player.Load(character);
    }

    internal void SetInteractionPaused(bool paused) => player.SetPaused(paused);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            player.Dispose();
        }

        base.Dispose(disposing);
    }
}
