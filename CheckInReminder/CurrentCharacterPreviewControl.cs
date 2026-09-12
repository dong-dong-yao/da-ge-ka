namespace CheckInReminder;

/// <summary>设置页的紧凑伙伴卡，保持角色可见与更换入口就近。</summary>
internal sealed class CurrentCharacterPreviewControl : UserControl
{
    private readonly AnimationPreviewPlayer player;
    private readonly CharacterPreviewImage previewBox;
    private readonly Label titleLabel;
    private readonly Label subtitleLabel;
    private readonly Label nameLabel;
    private readonly BrandButton changeButton;

    public event EventHandler? ChangeCharacterRequested;
    public string CurrentCharacterId { get; private set; }

    public CurrentCharacterPreviewControl(string? initialCharacterId)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        DoubleBuffered = true;
        ResizeRedraw = true;
        var character = AnimationCatalog.FindCharacter(initialCharacterId) ?? AnimationCatalog.Characters[0];
        CurrentCharacterId = character.Id;
        titleLabel = new Label
        {
            Text = "你的提醒伙伴", BackColor = Color.Transparent,
            ForeColor = UiTheme.TextColor, Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
        };
        subtitleLabel = new Label
        {
            Text = "提醒时，和你见面", BackColor = Color.Transparent,
            ForeColor = UiTheme.MutedTextColor, Font = new Font("Microsoft YaHei UI", 8.5f),
        };
        previewBox = new CharacterPreviewImage { AccessibleName = "当前角色动画预览", TabStop = false };
        nameLabel = new Label
        {
            Text = character.DisplayName, BackColor = Color.Transparent,
            ForeColor = UiTheme.TextColor, Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        changeButton = new BrandButton(BrandButtonKind.Secondary)
        {
            Text = "更换 →", CornerRadius = 12,
            AccessibleName = "跳转到角色页面更换角色",
            Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
        };
        changeButton.Click += (_, _) => ChangeCharacterRequested?.Invoke(this, EventArgs.Empty);
        Controls.AddRange([titleLabel, subtitleLabel, previewBox, nameLabel, changeButton]);
        player = new AnimationPreviewPlayer(previewBox);
        player.Load(character);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (previewBox is null) return;
        int S(int value) => (int)Math.Round(value * DeviceDpi / 96f);
        titleLabel.Bounds = new Rectangle(S(18), S(16), Width - S(36), S(24));
        subtitleLabel.Bounds = new Rectangle(S(18), S(43), Width - S(36), S(20));
        previewBox.Bounds = new Rectangle(S(18), S(67), Math.Max(1, Width - S(36)), Math.Max(1, Height - S(127)));
        nameLabel.Bounds = new Rectangle(S(18), Height - S(48), Math.Max(1, Width - S(120)), S(30));
        changeButton.Bounds = new Rectangle(Width - S(100), Height - S(48), S(82), S(32));
    }

    public void SetCharacter(string characterId)
    {
        if (string.Equals(CurrentCharacterId, characterId, StringComparison.Ordinal)) return;
        var character = AnimationCatalog.FindCharacter(characterId);
        if (character is null) return;
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
            titleLabel.Font.Dispose();
            subtitleLabel.Font.Dispose();
            nameLabel.Font.Dispose();
            changeButton.Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
