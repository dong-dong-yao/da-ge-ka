namespace CheckInReminder;

/// <summary>紧凑的双栏设置面板；高度由实际卡片决定，不随角色预览无限拉长。</summary>
internal sealed class SettingsDashboardPanel : Panel
{
    private readonly Control[] reminderCards;
    private readonly Control[] companionCards;
    private readonly Label heading;

    public SettingsDashboardPanel(Control[] reminderCards, Control[] companionCards)
    {
        this.reminderCards = reminderCards;
        this.companionCards = companionCards;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Dock = DockStyle.Top;
        BackColor = UiTheme.WarmBackgroundColor;
        heading = new Label
        {
            Text = "按你的节奏，安排每一次提醒",
            ForeColor = UiTheme.MutedTextColor,
            Font = new Font("Microsoft YaHei UI", 9),
        };
        Controls.Add(heading);
        foreach (var card in reminderCards.Concat(companionCards))
        {
            card.Dock = DockStyle.None;
            Controls.Add(card);
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (heading is null) return;
        int S(int value) => (int)Math.Round(value * DeviceDpi / 96f);
        Height = S(524);
        var available = Math.Max(S(580), Math.Min(S(1060), ClientSize.Width - S(48)));
        var left = Math.Max(S(24), (ClientSize.Width - available) / 2);
        var gap = S(14);
        var mainWidth = (int)((available - gap) * 0.61);
        var companionWidth = available - mainWidth - gap;
        heading.Bounds = new Rectangle(left, S(10), available, S(23));
        for (var i = 0; i < reminderCards.Length; i++)
            reminderCards[i].Bounds = new Rectangle(left, S(44 + i * 160), mainWidth, S(148));
        companionCards[0].Bounds = new Rectangle(left + mainWidth + gap, S(44), companionWidth, S(260));
        companionCards[1].Bounds = new Rectangle(left + mainWidth + gap, S(316), companionWidth, S(92));
        companionCards[2].Bounds = new Rectangle(left + mainWidth + gap, S(420), companionWidth, S(92));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) heading.Font.Dispose();
        base.Dispose(disposing);
    }
}
