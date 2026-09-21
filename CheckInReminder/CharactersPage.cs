namespace CheckInReminder;

/// <summary>随窗口宽度变化的角色画廊，同时只播放鼠标所在卡片。</summary>
internal sealed class CharactersPage : Panel
{
    private CharacterSelection selection;
    private readonly Button createButton = new BrandButton(BrandButtonKind.Primary) { Text = "＋ 创建我的角色" };
    private readonly Button deleteButton = new BrandButton(BrandButtonKind.Secondary) { Text = "删除所选自定义角色" };
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
    public event EventHandler<string>? CharacterDeleted;
    public string ConfirmedCharacterId => selection.ConfirmedCharacterId;

    public CharactersPage(string? initialCharacterId)
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = UiTheme.WarmBackgroundColor;
        Margin = Padding.Empty;
        selection = new CharacterSelection(AnimationCatalog.AllCharacters, initialCharacterId);
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
        content.Controls.Add(createButton);
        content.Controls.Add(deleteButton);
        createButton.Click += (_, _) => CreateCharacter();
        deleteButton.Click += (_, _) => DeleteCharacter();
        foreach (var character in AnimationCatalog.AllCharacters)
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
        if (AnimationCatalog.CustomLoadErrors.Count > 0)
            subtitle.Text = $"已跳过 {AnimationCatalog.CustomLoadErrors.Count} 个损坏的自定义角色包，其他角色可正常使用。";
        deleteButton.Enabled = AnimationCatalog.FindCharacter(selection.ConfirmedCharacterId)?.CustomPackagePath is not null;
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
            var compactHeader = width >= S(650);
            var header = S(compactHeader ? 56 : 96);
            var cardHeight = Math.Clamp((ClientSize.Height - S(24) - header - gap * (rows - 1)) / rows, S(218), S(280));
            var height = header + rows * (cardHeight + gap) - gap;
            AutoScrollMinSize = new Size(0, height + S(24));
            content.Bounds = new Rectangle(Math.Max(S(24), (ClientSize.Width - width) / 2) + AutoScrollPosition.X,
                S(12) + AutoScrollPosition.Y, width, height);
            title.Bounds = new Rectangle(0, 0, compactHeader ? width - S(338) : width, S(36));
            subtitle.Bounds = new Rectangle(1, S(36), width, S(20));
            createButton.Bounds = new Rectangle(compactHeader ? width - S(330) : 0, S(compactHeader ? 0 : 59), S(140), S(34));
            deleteButton.Bounds = new Rectangle(compactHeader ? width - S(178) : S(152), S(compactHeader ? 0 : 59), S(178), S(34));
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
        deleteButton.Enabled = card.Character.CustomPackagePath is not null;
    }

    private void CreateCharacter()
    {
        SetInteractionPaused(true);
        try
        {
            using var wizard = new CharacterCreationWizard();
            if (wizard.ShowDialog(FindForm()) != DialogResult.OK || wizard.CreatedCharacter is null) return;
            var id = wizard.SelectAfterCreation ? wizard.CreatedCharacter.Id : selection.ConfirmedCharacterId;
            RefreshCards(id);
            if (wizard.SelectAfterCreation) CharacterConfirmed?.Invoke(this, id);
        }
        finally { SetInteractionPaused(false); }
    }

    private void DeleteCharacter()
    {
        var character = AnimationCatalog.FindCharacter(selection.ConfirmedCharacterId);
        if (character?.CustomPackagePath is null) return;
        if (MessageBox.Show(FindForm(), $"删除“{character.DisplayName}”的本地角色包？原始素材文件不会删除。删除后选回白熊。", "删除自定义角色", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            new CustomCharacterStore().Delete(character.Id);
            AnimationCatalog.RefreshCustomCharacters();
            CharacterDeleted?.Invoke(this, character.Id);
            RefreshCards(AnimationCatalog.DefaultCharacterId);
            CharacterConfirmed?.Invoke(this, AnimationCatalog.DefaultCharacterId);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { MessageBox.Show(FindForm(), e.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void RefreshCards(string id)
    {
        activeCard = null;
        foreach (var card in cards) { content.Controls.Remove(card); card.Dispose(); }
        cards.Clear();
        selection = new CharacterSelection(AnimationCatalog.AllCharacters, id);
        foreach (var character in AnimationCatalog.AllCharacters)
        {
            var card = new CharacterCardControl(character, character.Id == selection.ConfirmedCharacterId);
            card.Hovered += (_, _) => { activeCard = card; UpdatePlayback(); };
            card.HoverEnded += (_, _) => { if (ReferenceEquals(activeCard, card)) { activeCard = null; UpdatePlayback(); } };
            card.Confirmed += (_, _) => ConfirmCard(card);
            cards.Add(card); content.Controls.Add(card);
        }
        deleteButton.Enabled = AnimationCatalog.FindCharacter(id)?.CustomPackagePath is not null;
        if (AnimationCatalog.CustomLoadErrors.Count > 0) subtitle.Text = $"已跳过 {AnimationCatalog.CustomLoadErrors.Count} 个损坏的自定义角色包。其他角色可正常使用。";
        PerformLayout(); UpdatePlayback();
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
