namespace CheckInReminder;

/// <summary>
/// 角色选择页：标题 + 两列角色卡片网格。同一时间只播放一张卡片的预览动画
/// （悬停或当前确认的卡片），滚动/缩放期间全部暂停。
/// </summary>
internal sealed class CharactersPage : Panel
{
    private const int CardHeight = 300;
    private const int CardMargin = 6;
    private const int GridColumns = 2;

    private readonly CharacterSelection selection;
    private readonly List<CharacterCardControl> cards = [];
    private readonly System.Windows.Forms.Timer scrollIdleTimer;
    private CharacterCardControl? activeCard;
    private bool interactionPaused;
    private bool scrollSettling;

    public event EventHandler<string>? CharacterConfirmed;

    public string ConfirmedCharacterId => selection.ConfirmedCharacterId;

    public CharactersPage(string? initialCharacterId)
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = UiTheme.WarmBackgroundColor;
        Margin = Padding.Empty;

        selection = new CharacterSelection(AnimationCatalog.Characters, initialCharacterId);

        var characters = AnimationCatalog.Characters;
        var rowCount = (characters.Count + GridColumns - 1) / GridColumns;
        var gridHeight = rowCount * (CardHeight + (CardMargin * 2));

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = GridColumns,
            RowCount = rowCount,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        for (var column = 0; column < GridColumns; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / GridColumns));
        }
        for (var row = 0; row < rowCount; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, CardHeight + (CardMargin * 2)));
        }

        for (var index = 0; index < characters.Count; index++)
        {
            var character = characters[index];
            var card = new CharacterCardControl(
                character,
                isConfirmed: string.Equals(
                    character.Id, selection.ConfirmedCharacterId, StringComparison.Ordinal))
            {
                Margin = new Padding(CardMargin),
            };
            card.Hovered += (_, _) => Activate(card);
            card.Confirmed += (_, _) => ConfirmCard(card);
            cards.Add(card);
            grid.Controls.Add(card, index % GridColumns, index / GridColumns);
        }

        var title = new Label
        {
            Text = "选择提醒角色",
            AutoSize = true,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 14, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(6, 0, 0, 2),
        };
        var subtitle = new Label
        {
            Text = "悬停卡片可以预览动画，确认后提醒时就会播放它",
            AutoSize = true,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(6, 0, 0, 12),
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34 + 24 + 12 + gridHeight + 32,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 18, 24, 14),
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, gridHeight));
        content.Controls.Add(title, 0, 0);
        content.Controls.Add(subtitle, 0, 1);
        content.Controls.Add(grid, 0, 2);
        Controls.Add(content);

        scrollIdleTimer = new System.Windows.Forms.Timer { Interval = 120 };
        scrollIdleTimer.Tick += (_, _) =>
        {
            scrollIdleTimer.Stop();
            scrollSettling = false;
            UpdatePlayback();
        };
        Scroll += (_, _) =>
        {
            scrollSettling = true;
            UpdatePlayback();
            scrollIdleTimer.Stop();
            scrollIdleTimer.Start();
        };

        // 初始让当前确认的卡片播放，页面一打开就是活的
        activeCard = cards.FirstOrDefault(card => string.Equals(
            card.Character.Id, selection.ConfirmedCharacterId, StringComparison.Ordinal));
        UpdatePlayback();
    }

    /// <summary>滚动/缩放等交互期间暂停本页所有预览动画。</summary>
    internal void SetInteractionPaused(bool paused)
    {
        if (interactionPaused == paused)
        {
            return;
        }

        interactionPaused = paused;
        UpdatePlayback();
    }

    private void Activate(CharacterCardControl card)
    {
        if (ReferenceEquals(activeCard, card))
        {
            return;
        }

        activeCard = card;
        UpdatePlayback();
    }

    private void ConfirmCard(CharacterCardControl card)
    {
        selection.SelectById(card.Character.Id);
        var confirmedId = selection.Confirm();
        foreach (var item in cards)
        {
            item.SetConfirmed(string.Equals(item.Character.Id, confirmedId, StringComparison.Ordinal));
        }

        CharacterConfirmed?.Invoke(this, confirmedId);
    }

    private void UpdatePlayback()
    {
        var allowPlaying = !interactionPaused && !scrollSettling;
        foreach (var card in cards)
        {
            card.SetPlaying(allowPlaying && ReferenceEquals(card, activeCard));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            scrollIdleTimer.Stop();
            scrollIdleTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
