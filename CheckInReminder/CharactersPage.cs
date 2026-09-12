namespace CheckInReminder;

/// <summary>随窗口宽度变化的角色画廊，同时只播放鼠标所在卡片。</summary>
internal sealed class CharactersPage : Panel
{
    private readonly CharacterSelection selection;
    private readonly List<CharacterCardControl> cards = [];
    private readonly Panel content;
    private readonly Label title;
    private readonly Label subtitle;
    private readonly System.Windows.Forms.Timer scrollIdleTimer;
    private CharacterCardControl? activeCard;
    private bool interactionPaused;
    private bool scrollSettling;
    private bool arranging;

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
        content = new Panel { BackColor = BackColor, Margin = Padding.Empty };
        title = new Label
        {
            Text = "选一位提醒伙伴",
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            AutoSize = false,
        };
        subtitle = new Label
        {
            Text = "把准时这件小事，交给喜欢的它。选好后记得保存设置。",
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = UiTheme.MutedTextColor,
            AutoSize = false,
        };
        content.Controls.Add(title);
        content.Controls.Add(subtitle);
        foreach (var character in AnimationCatalog.Characters)
        {
            var card = new CharacterCardControl(character, character.Id == selection.ConfirmedCharacterId);
            card.Hovered += (_, _) => { activeCard = card; UpdatePlayback(); };
            card.HoverEnded += (_, _) =>
            {
                if (ReferenceEquals(activeCard, card)) { activeCard = null; UpdatePlayback(); }
            };
            card.Confirmed += (_, _) => ConfirmCard(card);
            cards.Add(card);
            content.Controls.Add(card);
        }
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
            activeCard = null;
            UpdatePlayback();
            scrollIdleTimer.Stop();
            scrollIdleTimer.Start();
        };
        VisibleChanged += (_, _) => { activeCard = null; UpdatePlayback(); };
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (content is null || arranging)
        {
            base.OnLayout(e);
            return;
        }
        arranging = true;
        try
        {
            int S(int value) => (int)Math.Round(value * DeviceDpi / 96f);
            // 留出滚动条空间，避免列数在临界宽度来回切换。
            var available = Math.Max(S(200), Width - SystemInformation.VerticalScrollBarWidth - S(48));
            var width = Math.Min(S(1020), available);
            var columns = width >= S(690) ? 3 : width >= S(440) ? 2 : 1;
            var gap = S(14);
            var cardWidth = (width - gap * (columns - 1)) / columns;
            var rows = (cards.Count + columns - 1) / columns;
            var header = S(56);
            var cardHeight = Math.Clamp((ClientSize.Height - S(24) - header - gap * (rows - 1)) / rows, S(218), S(280));
            var height = header + rows * (cardHeight + gap) - gap;
            AutoScrollMinSize = new Size(0, height + S(24));
            content.Bounds = new Rectangle(Math.Max(S(24), (ClientSize.Width - width) / 2) + AutoScrollPosition.X,
                S(12) + AutoScrollPosition.Y, width, height);
            title.Bounds = new Rectangle(0, 0, width, S(36));
            subtitle.Bounds = new Rectangle(1, S(36), width, S(20));
            for (var i = 0; i < cards.Count; i++)
                cards[i].Bounds = new Rectangle(i % columns * (cardWidth + gap),
                    header + i / columns * (cardHeight + gap), cardWidth, cardHeight);
            // 子控件缩小后再重算滚动范围，否则旧的大尺寸会留下多余滚动条。
            base.OnLayout(e);
        }
        finally { arranging = false; }
    }

    internal void SetInteractionPaused(bool paused)
    {
        interactionPaused = paused;
        UpdatePlayback();
    }

    private void ConfirmCard(CharacterCardControl card)
    {
        selection.SelectById(card.Character.Id);
        var confirmedId = selection.Confirm();
        foreach (var item in cards) item.SetConfirmed(item.Character.Id == confirmedId);
        CharacterConfirmed?.Invoke(this, confirmedId);
    }

    private void UpdatePlayback()
    {
        foreach (var card in cards)
            card.SetPlaying(Visible && !interactionPaused && !scrollSettling && ReferenceEquals(card, activeCard));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            scrollIdleTimer.Stop();
            scrollIdleTimer.Dispose();
            title.Font.Dispose();
            subtitle.Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
