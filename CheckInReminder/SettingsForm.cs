using System.Globalization;

namespace CheckInReminder;

internal sealed class SettingsForm : Form
{
    private const int SideNavWidth = 148;
    private readonly SoftTimePicker morningStartPicker;
    private readonly SoftTimePicker morningEndPicker;
    private readonly SoftComboBox morningIntervalBox;
    private readonly SoftTimePicker eveningStartPicker;
    private readonly SoftComboBox eveningIntervalBox;
    private readonly ToggleSwitch breakReminderToggle;
    private readonly SoftTimePicker breakStartPicker;
    private readonly SoftTimePicker breakEndPicker;
    private readonly SoftComboBox breakIntervalBox;
    private readonly ToggleSwitch autoStartToggle;
    private readonly ToggleSwitch desktopPetToggle;
    private readonly CurrentCharacterPreviewControl currentCharacterPreview;
    private readonly CharactersPage charactersPage;
    private readonly BufferedScrollPanel settingsPage;
    private readonly CompositedPageHost pageHost;
    private readonly SideNavBar sideNav;
    private readonly Panel breakDetails;
    private readonly System.Windows.Forms.Timer openingTimer;
    private readonly System.Windows.Forms.Timer saveFeedbackTimer;
    private readonly System.Windows.Forms.Timer scrollIdleTimer;
    private readonly BrandButton saveButton;
    private readonly Func<AppSettings, string?> saveSettings;
    private readonly Action testReminder;
    private bool isInteractiveResize;
    private bool isScrollSettling;

    public SettingsForm(AppSettings settings, Func<AppSettings, string?> saveSettings, Action testReminder)
    {
        this.saveSettings = saveSettings;
        this.testReminder = testReminder;
        int S(int value) => (int)Math.Round(value * DeviceDpi / 96f);

        Text = UiTheme.SettingsTitle;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(DeviceDpi, DeviceDpi);
        ClientSize = new Size(S(980), S(700));
        MinimumSize = new Size(S(860), S(600));
        BackColor = UiTheme.WarmBackgroundColor;
        ForeColor = UiTheme.TextColor;
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10);
        DoubleBuffered = true;
        Opacity = 0;

        morningStartPicker = CreateTimePicker(settings.MorningStart);
        morningEndPicker = CreateTimePicker(settings.MorningEnd);
        morningIntervalBox = CreateIntervalBox(settings.MorningIntervalMinutes);
        eveningStartPicker = CreateTimePicker(settings.EveningStart);
        eveningIntervalBox = CreateIntervalBox(settings.EveningIntervalMinutes);
        breakReminderToggle = new ToggleSwitch
        {
            Checked = settings.BreakReminderEnabled,
            AccessibleName = "启用久坐提醒",
            Anchor = AnchorStyles.Right,
        };
        breakStartPicker = CreateTimePicker(settings.BreakStart);
        breakEndPicker = CreateTimePicker(settings.BreakEnd);
        breakIntervalBox = CreateBreakIntervalBox(settings.BreakIntervalMinutes);
        autoStartToggle = new ToggleSwitch
        {
            Checked = settings.AutoStart,
            AccessibleName = "开机自启动",
            Anchor = AnchorStyles.Right,
        };
        desktopPetToggle = new ToggleSwitch
        {
            Checked = settings.DesktopPetEnabled,
            AccessibleName = "桌面宠物",
            Anchor = AnchorStyles.Right,
        };
        currentCharacterPreview = new CurrentCharacterPreviewControl(settings.CharacterId);
        charactersPage = new CharactersPage(settings.CharacterId)
        {
            Visible = false,
        };
        charactersPage.CharacterConfirmed += (_, characterId) => currentCharacterPreview.SetCharacter(characterId);

        var morningCard = CreateSettingsCard(
            IconBadge.IconKind.Sun,
            "上午提醒",
            "上班前这段时间里提醒你打卡",
            ("开始时间", morningStartPicker),
            ("结束时间", morningEndPicker),
            ("间隔（分钟）", morningIntervalBox));
        var eveningCard = CreateSettingsCard(
            IconBadge.IconKind.Moon,
            "晚上提醒",
            "下班前提醒你打卡并温柔挽留",
            ("开始时间", eveningStartPicker),
            ("间隔（分钟）", eveningIntervalBox));
        var breakCardResult = CreateBreakCard();
        var breakCard = breakCardResult.Card;
        breakDetails = breakCardResult.Details;
        var autoStartCard = CreateToggleCard(
            IconBadge.IconKind.Power,
            "开机自启动",
            "登录 Windows 后自动启动",
            autoStartToggle);
        var desktopPetCard = CreateToggleCard(
            IconBadge.IconKind.Paw,
            "桌面宠物",
            "敲键盘时，陪你一起敲",
            desktopPetToggle);

        var characterCard = CreateSurface();
        characterCard.Padding = Padding.Empty;
        characterCard.Controls.Add(currentCharacterPreview);
        var content = new SettingsDashboardPanel(
            [morningCard, eveningCard, breakCard],
            [characterCard, autoStartCard, desktopPetCard]);
        settingsPage = new BufferedScrollPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = Padding.Empty,
        };
        settingsPage.Controls.Add(content);

        sideNav = new SideNavBar(
            [
                new SideNavBar.NavItem("settings", "设置", IconBadge.IconKind.Gear),
                new SideNavBar.NavItem("characters", "角色", IconBadge.IconKind.Paw),
            ],
            "settings")
        {
            Dock = DockStyle.Fill,
        };
        sideNav.NavigationRequested += (_, key) => ShowPage(key);
        currentCharacterPreview.ChangeCharacterRequested += (_, _) => sideNav.SelectPage("characters");

        pageHost = new CompositedPageHost
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        pageHost.Controls.Add(charactersPage);
        pageHost.Controls.Add(settingsPage);

        var middleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        middleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(SideNavWidth)));
        middleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        middleRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        middleRow.Controls.Add(sideNav, 0, 0);
        middleRow.Controls.Add(pageHost, 1, 0);

        saveButton = new BrandButton
        {
            Text = "保存全部设置",
            Size = new Size(S(150), S(44)),
            CornerRadius = S(22),
            AccessibleName = "保存全部设置",
        };
        saveButton.Click += (_, _) => Save();
        var testButton = new BrandButton(BrandButtonKind.Secondary)
        {
            Text = "测试提醒",
            Size = new Size(S(118), S(44)),
            CornerRadius = S(22),
            AccessibleName = "测试提醒",
        };
        testButton.Click += (_, _) => this.testReminder();
        AcceptButton = saveButton;

        var footer = CreateFooter(testButton, saveButton);
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, S(86)));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, S(78)));
        shell.Controls.Add(CreateHeader(), 0, 0);
        shell.Controls.Add(middleRow, 0, 1);
        shell.Controls.Add(footer, 0, 2);
        Controls.Add(shell);

        breakDetails.Enabled = settings.BreakReminderEnabled;
        breakDetails.Visible = true;
        breakReminderToggle.CheckedChanged += (_, _) => breakDetails.Enabled = breakReminderToggle.Checked;

        scrollIdleTimer = new System.Windows.Forms.Timer { Interval = 120 };
        scrollIdleTimer.Tick += (_, _) =>
        {
            scrollIdleTimer.Stop();
            isScrollSettling = false;
            UpdatePreviewInteractionState();
        };
        settingsPage.Scroll += (_, _) =>
        {
            isScrollSettling = true;
            UpdatePreviewInteractionState();
            scrollIdleTimer.Stop();
            scrollIdleTimer.Start();
        };
        ResizeBegin += (_, _) =>
        {
            isInteractiveResize = true;
            UpdatePreviewInteractionState();
        };
        ResizeEnd += (_, _) =>
        {
            isInteractiveResize = false;
            UpdatePreviewInteractionState();
        };

        openingTimer = new System.Windows.Forms.Timer { Interval = 16 };
        openingTimer.Tick += (_, _) => AnimateOpening();
        saveFeedbackTimer = new System.Windows.Forms.Timer { Interval = 750 };
        saveFeedbackTimer.Tick += (_, _) =>
        {
            saveFeedbackTimer.Stop();
            Close();
        };
        Shown += (_, _) =>
        {
            openingTimer.Start();
            // 构造期间窗体未显示，VisibleCore 为 false，Shown 时按真实可见性重估播放状态
            UpdatePreviewInteractionState();
        };
        UpdatePreviewInteractionState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            openingTimer.Dispose();
            saveFeedbackTimer.Dispose();
            scrollIdleTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Control CreateHeader()
    {
        var header = new GradientHeaderPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        int S(int value) => (int)Math.Round(value * header.DeviceDpi / 96f);
        var title = new Label
        {
            Text = "打个卡",
            AutoSize = true,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 251, 244),
            BackColor = Color.Transparent,
            Location = new Point(S(30), S(12)),
        };
        var subtitle = new Label
        {
            Text = "提醒设置 · 把重要的小事交给可爱的角色",
            AutoSize = true,
            ForeColor = Color.FromArgb(250, 236, 218),
            BackColor = Color.Transparent,
            Location = new Point(S(32), S(58)),
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    private static RoundedPanel CreateSurface()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill, ShowShadow = false, ShowOutline = true,
            SurfaceColor = UiTheme.SurfaceColor, OutlineColor = UiTheme.BorderColor,
            Margin = Padding.Empty,
        };
        card.CornerRadius = (int)Math.Round(18 * card.DeviceDpi / 96f);
        card.Padding = new Padding((int)Math.Round(16 * card.DeviceDpi / 96f));
        return card;
    }
    private static RoundedPanel CreateSettingsCard(
        IconBadge.IconKind icon, string title, string subtitle,
        params (string Label, Control Control)[] rows)
    {
        var card = CreateSurface();
        var header = new CardHeader(icon, title, subtitle) { Dock = DockStyle.Top };
        var fields = CreateFieldGrid(rows);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent, Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(fields, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static TableLayoutPanel CreateFieldGrid(params (string Label, Control Control)[] rows)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = rows.Length, RowCount = 2,
            BackColor = Color.Transparent, Margin = Padding.Empty,
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, (int)Math.Round(22 * grid.DeviceDpi / 96f)));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows.Length; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / rows.Length));
            grid.Controls.Add(new Label
            {
                Text = rows[i].Label, Dock = DockStyle.Fill,
                ForeColor = UiTheme.MutedTextColor, BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft,
            }, i, 0);
            rows[i].Control.AccessibleName = rows[i].Label;
            grid.Controls.Add(new CompactInputSlot(rows[i].Control)
            {
                Margin = new Padding(0, 0, i == rows.Length - 1 ? 0 : (int)Math.Round(10 * grid.DeviceDpi / 96f), 0),
            }, i, 1);
        }
        return grid;
    }

    private (RoundedPanel Card, Panel Details) CreateBreakCard()
    {
        var card = CreateSurface();
        var header = new CardHeader(IconBadge.IconKind.Chair, "久坐提醒", "按工作时段提醒你站起来活动");
        var layout = CreateToggleLayout(header, breakReminderToggle);
        var details = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
        details.Controls.Add(CreateFieldGrid(
            ("开始时间", breakStartPicker), ("结束时间", breakEndPicker), ("提醒间隔", breakIntervalBox)));
        layout.Controls.Add(details, 0, 1);
        layout.SetColumnSpan(details, 2);
        card.Controls.Add(layout);
        return (card, details);
    }

    private static TableLayoutPanel CreateToggleLayout(CardHeader header, ToggleSwitch toggle)
    {
        header.Margin = Padding.Empty;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            BackColor = Color.Transparent, Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var dpiScale = layout.DeviceDpi / 96f;
        toggle.Size = new Size((int)Math.Round(48 * dpiScale), (int)Math.Round(26 * dpiScale));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)Math.Round(54 * dpiScale)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
        // 即便没有详情也保留弹性尾行，避免最后一个绝对行被拉伸而改变标题/开关对齐。
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(toggle, 1, 0);
        return layout;
    }

    private static RoundedPanel CreateToggleCard(IconBadge.IconKind icon, string title, string subtitle, ToggleSwitch toggle)
    {
        var card = CreateSurface();
        card.Controls.Add(CreateToggleLayout(new CardHeader(icon, title, subtitle), toggle));
        return card;
    }
    private static Control CreateFooter(BrandButton testButton, BrandButton saveButton)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(26, 12, 26, 16),
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        int S(int value) => (int)Math.Round(value * footer.DeviceDpi / 96f);
        footer.Padding = new Padding(S(26), S(12), S(26), S(16));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label
        {
            Text = "所有设置仅保存在这台电脑上",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
        }, 0, 0);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        testButton.Margin = new Padding(0, 0, S(12), 0);
        testButton.Anchor = AnchorStyles.None;
        saveButton.Margin = Padding.Empty;
        saveButton.Anchor = AnchorStyles.None;
        buttons.Controls.Add(testButton);
        buttons.Controls.Add(saveButton);
        footer.Controls.Add(buttons, 1, 0);
        return footer;
    }

    private void Save()
    {
        if (!int.TryParse(morningIntervalBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var morningInterval) ||
            !int.TryParse(eveningIntervalBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var eveningInterval))
        {
            MessageBox.Show(this, "提醒间隔必须是整数。", "设置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var candidate = new AppSettings
        {
            MorningStart = morningStartPicker.Value,
            MorningEnd = morningEndPicker.Value,
            MorningIntervalMinutes = morningInterval,
            EveningStart = eveningStartPicker.Value,
            EveningIntervalMinutes = eveningInterval,
            AutoStart = autoStartToggle.Checked,
            BreakReminderEnabled = breakReminderToggle.Checked,
            BreakStart = breakStartPicker.Value,
            BreakEnd = breakEndPicker.Value,
            BreakIntervalMinutes = GetSelectedBreakInterval(),
            CharacterId = charactersPage.ConfirmedCharacterId,
            DesktopPetEnabled = desktopPetToggle.Checked,
        };

        if (!SettingsService.TryValidate(candidate, out var validationMessage))
        {
            MessageBox.Show(this, validationMessage, "设置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var saveError = saveSettings(candidate);
        if (saveError is not null)
        {
            MessageBox.Show(this, saveError, "保存设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        saveButton.Text = "已保存 ✓";
        saveFeedbackTimer.Stop();
        saveFeedbackTimer.Start();
    }

    private static SoftTimePicker CreateTimePicker(TimeOnly value) => new(value)
    {
        Size = new Size(138, 38),
        Anchor = AnchorStyles.Right,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11f, FontStyle.Bold),
    };

    private static SoftComboBox CreateIntervalBox(int value)
    {
        var box = new SoftComboBox
        {
            Size = new Size(138, 38),
            Anchor = AnchorStyles.Right,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11f, FontStyle.Bold),
        };
        foreach (var item in new[] { "5", "10", "15", "30" })
        {
            box.Items.Add(item);
        }

        box.Text = value.ToString(CultureInfo.InvariantCulture);
        return box;
    }

    private static SoftComboBox CreateBreakIntervalBox(int value)
    {
        var box = new SoftComboBox
        {
            Size = new Size(138, 38),
            Anchor = AnchorStyles.Right,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11f, FontStyle.Bold),
        };
        foreach (var item in new[] { "1 小时", "1.5 小时", "2 小时", "3 小时" })
        {
            box.Items.Add(item);
        }

        box.SelectedIndex = value switch
        {
            90 => 1,
            120 => 2,
            180 => 3,
            _ => 0,
        };
        return box;
    }

    private int GetSelectedBreakInterval() => breakIntervalBox.SelectedIndex switch
    {
        1 => 90,
        2 => 120,
        3 => 180,
        _ => 60,
    };

    private void AnimateOpening()
    {
        Opacity = Math.Min(1, Opacity + 0.09);
        if (Opacity >= 1)
        {
            openingTimer.Stop();
        }
    }

    private void ShowPage(string key)
    {
        var showSettings = string.Equals(key, "settings", StringComparison.Ordinal);
        pageHost.ShowOnly(
            showSettings ? settingsPage : charactersPage,
            settingsPage,
            charactersPage);
        UpdatePreviewInteractionState();
    }

    private void UpdatePreviewInteractionState()
    {
        currentCharacterPreview.SetInteractionPaused(
            isInteractiveResize || isScrollSettling || !settingsPage.Visible);
        charactersPage.SetInteractionPaused(isInteractiveResize || settingsPage.Visible);
    }
}
