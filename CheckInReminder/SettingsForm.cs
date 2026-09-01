using System.Globalization;

namespace CheckInReminder;

internal sealed class SettingsForm : Form
{
    private const int ExpandedBreakHeight = 320;
    private const int AutoStartCardHeight = 128;
    private const int ScrollableContentHeight = 956;
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
    private readonly CharacterSelectorControl characterSelector;
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

        Text = UiTheme.SettingsTitle;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 680);
        MinimumSize = new Size(780, 560);
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
        characterSelector = new CharacterSelectorControl(settings.CharacterId);

        var morningCard = CreateSettingsCard(
            IconBadge.IconKind.Sun,
            "上午提醒",
            "上班前这段时间里提醒你打卡",
            ("开始时间", morningStartPicker),
            ("结束时间", morningEndPicker),
            ("提醒间隔（分钟）", morningIntervalBox));
        var eveningCard = CreateSettingsCard(
            IconBadge.IconKind.Moon,
            "晚上提醒",
            "下班前提醒你打卡并温柔挽留",
            ("开始时间", eveningStartPicker),
            ("提醒间隔（分钟）", eveningIntervalBox));
        var breakCardResult = CreateBreakCard();
        var breakCard = breakCardResult.Card;
        breakDetails = breakCardResult.Details;
        var autoStartCard = CreateToggleCard(
            IconBadge.IconKind.Power,
            "开机自启动",
            "登录 Windows 后自动守候提醒",
            autoStartToggle);

        var leftColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 8, 0),
        };
        leftColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 268));
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 208));
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, ExpandedBreakHeight));
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, AutoStartCardHeight));
        AddCard(leftColumn, morningCard, 0);
        AddCard(leftColumn, eveningCard, 1);
        AddCard(leftColumn, breakCard, 2);
        AddCard(leftColumn, autoStartCard, 3);

        var characterCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
            Margin = new Padding(8, 0, 0, 0),
        };
        characterCard.Controls.Add(characterSelector);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = ScrollableContentHeight,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 18, 24, 14),
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(leftColumn, 0, 0);
        content.Controls.Add(characterCard, 1, 0);

        var contentViewport = new BufferedScrollPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = Padding.Empty,
        };
        contentViewport.Controls.Add(content);

        saveButton = new BrandButton
        {
            Text = "保存全部设置",
            Size = new Size(150, 44),
            CornerRadius = 22,
            AccessibleName = "保存全部设置",
        };
        saveButton.Click += (_, _) => Save();
        var testButton = new BrandButton(BrandButtonKind.Secondary)
        {
            Text = "测试提醒",
            Size = new Size(118, 44),
            CornerRadius = 22,
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
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        shell.Controls.Add(CreateHeader(), 0, 0);
        shell.Controls.Add(contentViewport, 0, 1);
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
        contentViewport.Scroll += (_, _) =>
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
        Shown += (_, _) => openingTimer.Start();
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
        var title = new Label
        {
            Text = "打个卡",
            AutoSize = true,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 251, 244),
            BackColor = Color.Transparent,
            Location = new Point(30, 12),
        };
        var subtitle = new Label
        {
            Text = "提醒设置 · 把重要的小事交给可爱的角色",
            AutoSize = true,
            ForeColor = Color.FromArgb(250, 236, 218),
            BackColor = Color.Transparent,
            Location = new Point(32, 58),
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    private static RoundedPanel CreateSettingsCard(
        IconBadge.IconKind icon,
        string title,
        string subtitle,
        params (string Label, Control Control)[] rows)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, ShowOutline = false };
        var header = new CardHeader(icon, title, subtitle) { Dock = DockStyle.Top };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows.Length + 1,
            Padding = new Padding(20, 10, 20, 14),
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
        layout.Controls.Add(header, 0, 0);
        layout.SetColumnSpan(header, 2);
        for (var index = 0; index < rows.Length; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows.Length));
            AddSettingRow(layout, rows[index].Label, rows[index].Control, index + 1);
        }

        card.Controls.Add(layout);
        return card;
    }

    private (RoundedPanel Card, Panel Details) CreateBreakCard()
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, ShowOutline = false };
        var header = new CardHeader(IconBadge.IconKind.Chair, "久坐提醒", "按工作时段提醒你站起来活动");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Padding = new Padding(20, 12, 20, 12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(breakReminderToggle, 1, 0);

        var details = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        for (var row = 0; row < 3; row++)
        {
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
        }
        AddSettingRow(detailsLayout, "开始时间", breakStartPicker, 0);
        AddSettingRow(detailsLayout, "结束时间", breakEndPicker, 1);
        AddSettingRow(detailsLayout, "提醒间隔", breakIntervalBox, 2);
        details.Controls.Add(detailsLayout);
        layout.Controls.Add(details, 0, 1);
        layout.SetColumnSpan(details, 2);

        card.Controls.Add(layout);
        return (card, details);
    }

    private static RoundedPanel CreateToggleCard(IconBadge.IconKind icon, string title, string subtitle, ToggleSwitch toggle)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, ShowOutline = false };
        var header = new CardHeader(icon, title, subtitle);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 12, 20, 12),
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(toggle, 1, 0);
        card.Controls.Add(layout);
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
        testButton.Margin = new Padding(0, 0, 12, 0);
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
            CharacterId = characterSelector.ConfirmedCharacterId,
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

    private static void AddSettingRow(TableLayoutPanel layout, string text, Control control, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
        }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddCard(TableLayoutPanel layout, Control card, int row)
    {
        card.Margin = new Padding(0, row == 0 ? 0 : 6, 0, 6);
        layout.Controls.Add(card, 0, row);
    }

    private void AnimateOpening()
    {
        Opacity = Math.Min(1, Opacity + 0.09);
        if (Opacity >= 1)
        {
            openingTimer.Stop();
        }
    }

    private void UpdatePreviewInteractionState() =>
        characterSelector.SetInteractionPaused(isInteractiveResize || isScrollSettling);
}
