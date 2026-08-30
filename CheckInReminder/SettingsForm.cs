using System.Globalization;

namespace CheckInReminder;

internal sealed class SettingsForm : Form
{
    private const int ExpandedBreakHeight = 188;
    private const int CollapsedBreakHeight = 82;
    private readonly DateTimePicker morningStartPicker;
    private readonly DateTimePicker morningEndPicker;
    private readonly ComboBox morningIntervalBox;
    private readonly DateTimePicker eveningStartPicker;
    private readonly ComboBox eveningIntervalBox;
    private readonly ToggleSwitch breakReminderToggle;
    private readonly DateTimePicker breakStartPicker;
    private readonly DateTimePicker breakEndPicker;
    private readonly ComboBox breakIntervalBox;
    private readonly ToggleSwitch autoStartToggle;
    private readonly CharacterSelectorControl characterSelector;
    private readonly Panel breakDetails;
    private readonly RowStyle breakRowStyle;
    private readonly System.Windows.Forms.Timer breakAnimationTimer;
    private readonly System.Windows.Forms.Timer openingTimer;
    private readonly System.Windows.Forms.Timer saveFeedbackTimer;
    private readonly BrandButton saveButton;
    private readonly Func<AppSettings, string?> saveSettings;
    private readonly Action testReminder;
    private int targetBreakHeight;

    public SettingsForm(AppSettings settings, Func<AppSettings, string?> saveSettings, Action testReminder)
    {
        this.saveSettings = saveSettings;
        this.testReminder = testReminder;

        Text = UiTheme.SettingsTitle;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 700);
        MinimumSize = new Size(916, 739);
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
            "上午提醒",
            ("开始时间", morningStartPicker),
            ("结束时间", morningEndPicker),
            ("提醒间隔", morningIntervalBox));
        var eveningCard = CreateSettingsCard(
            "晚上提醒",
            ("开始时间", eveningStartPicker),
            ("提醒间隔", eveningIntervalBox));
        var breakCardResult = CreateBreakCard();
        var breakCard = breakCardResult.Card;
        breakDetails = breakCardResult.Details;
        var autoStartCard = CreateToggleCard(
            "开机自启动",
            "登录 Windows 后自动守候提醒",
            autoStartToggle);

        breakRowStyle = new RowStyle(
            SizeType.Absolute,
            settings.BreakReminderEnabled ? ExpandedBreakHeight : CollapsedBreakHeight);
        var leftColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 9, 0),
        };
        leftColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        leftColumn.RowStyles.Add(breakRowStyle);
        leftColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddCard(leftColumn, morningCard, 0);
        AddCard(leftColumn, eveningCard, 1);
        AddCard(leftColumn, breakCard, 2);
        AddCard(leftColumn, autoStartCard, 3);

        var characterCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
            Margin = new Padding(9, 0, 0, 0),
        };
        characterCard.Controls.Add(characterSelector);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 20, 24, 16),
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(leftColumn, 0, 0);
        content.Controls.Add(characterCard, 1, 0);

        saveButton = new BrandButton
        {
            Text = "保存全部设置",
            Size = new Size(148, 44),
            CornerRadius = 18,
            AccessibleName = "保存全部设置",
        };
        saveButton.Click += (_, _) => Save();
        var testButton = new BrandButton(BrandButtonKind.Secondary)
        {
            Text = "测试提醒",
            Size = new Size(122, 44),
            CornerRadius = 18,
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
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        shell.Controls.Add(CreateHeader(), 0, 0);
        shell.Controls.Add(content, 0, 1);
        shell.Controls.Add(footer, 0, 2);
        Controls.Add(shell);

        breakDetails.Enabled = settings.BreakReminderEnabled;
        breakDetails.Visible = settings.BreakReminderEnabled;
        targetBreakHeight = settings.BreakReminderEnabled ? ExpandedBreakHeight : CollapsedBreakHeight;
        breakReminderToggle.CheckedChanged += (_, _) => BeginBreakTransition();

        breakAnimationTimer = new System.Windows.Forms.Timer { Interval = 15 };
        breakAnimationTimer.Tick += (_, _) => AnimateBreakCard();
        openingTimer = new System.Windows.Forms.Timer { Interval = 16 };
        openingTimer.Tick += (_, _) => AnimateOpening();
        saveFeedbackTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        saveFeedbackTimer.Tick += (_, _) =>
        {
            saveFeedbackTimer.Stop();
            saveButton.Text = "保存全部设置";
        };
        Shown += (_, _) => openingTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            breakAnimationTimer.Dispose();
            openingTimer.Dispose();
            saveFeedbackTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Control CreateHeader()
    {
        var header = new GradientHeaderPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(28, 15, 28, 12),
        };
        var title = new Label
        {
            Text = "打个卡",
            AutoSize = true,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 20, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(28, 14),
        };
        var subtitle = new Label
        {
            Text = "提醒设置 · 把重要的小事交给可爱的角色",
            AutoSize = true,
            ForeColor = Color.FromArgb(246, 237, 227),
            BackColor = Color.Transparent,
            Location = new Point(31, 55),
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    private static RoundedPanel CreateSettingsCard(
        string title,
        params (string Label, Control Control)[] rows)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows.Length + 1,
            Padding = new Padding(18, 10, 18, 10),
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.Controls.Add(CreateSectionTitle(title), 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 2);
        for (var index = 0; index < rows.Length; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddSettingRow(layout, rows[index].Label, rows[index].Control, index + 1);
        }

        card.Controls.Add(layout);
        return card;
    }

    private (RoundedPanel Card, Panel Details) CreateBreakCard()
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill };
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 55,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 12, 18, 6),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        var textStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        textStack.Controls.Add(CreateSectionTitle("久坐提醒"));
        textStack.Controls.Add(new Label
        {
            Text = "按工作时段提醒你站起来活动",
            AutoSize = true,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        });
        header.Controls.Add(textStack, 0, 0);
        header.Controls.Add(breakReminderToggle, 1, 0);

        var details = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 0, 18, 10),
        };
        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        for (var row = 0; row < 3; row++)
        {
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
        }
        AddSettingRow(detailsLayout, "开始时间", breakStartPicker, 0);
        AddSettingRow(detailsLayout, "结束时间", breakEndPicker, 1);
        AddSettingRow(detailsLayout, "提醒间隔", breakIntervalBox, 2);
        details.Controls.Add(detailsLayout);

        card.Controls.Add(details);
        card.Controls.Add(header);
        return (card, details);
    }

    private static RoundedPanel CreateToggleCard(string title, string subtitle, ToggleSwitch toggle)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18, 10, 18, 10),
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        var textStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        textStack.Controls.Add(CreateSectionTitle(title));
        textStack.Controls.Add(new Label
        {
            Text = subtitle,
            AutoSize = true,
            ForeColor = UiTheme.MutedTextColor,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        });
        layout.Controls.Add(textStack, 0, 0);
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
            Padding = new Padding(25, 11, 25, 15),
            BackColor = UiTheme.SurfaceColor,
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
        buttons.Controls.Add(testButton);
        buttons.Controls.Add(saveButton);
        footer.Controls.Add(buttons, 1, 0);
        return footer;
    }

    private void Save()
    {
        if (!int.TryParse(morningIntervalBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var morningInterval) ||
            !int.TryParse(eveningIntervalBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var eveningInterval))
        {
            MessageBox.Show(this, "提醒间隔必须是整数。", "设置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var candidate = new AppSettings
        {
            MorningStart = TimeOnly.FromDateTime(morningStartPicker.Value),
            MorningEnd = TimeOnly.FromDateTime(morningEndPicker.Value),
            MorningIntervalMinutes = morningInterval,
            EveningStart = TimeOnly.FromDateTime(eveningStartPicker.Value),
            EveningIntervalMinutes = eveningInterval,
            AutoStart = autoStartToggle.Checked,
            BreakReminderEnabled = breakReminderToggle.Checked,
            BreakStart = TimeOnly.FromDateTime(breakStartPicker.Value),
            BreakEnd = TimeOnly.FromDateTime(breakEndPicker.Value),
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

    private static DateTimePicker CreateTimePicker(TimeOnly value) => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm",
        ShowUpDown = true,
        Value = DateTime.Today.Add(value.ToTimeSpan()),
        Width = 132,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11),
        CalendarForeColor = UiTheme.TextColor,
        CalendarMonthBackground = UiTheme.SurfaceColor,
        Anchor = AnchorStyles.Right,
    };

    private static ComboBox CreateIntervalBox(int value)
    {
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Width = 132,
            BackColor = UiTheme.SurfaceColor,
            ForeColor = UiTheme.TextColor,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f),
            Anchor = AnchorStyles.Right,
        };
        box.Items.AddRange(["5", "10", "15", "30"]);
        box.Text = value.ToString(CultureInfo.InvariantCulture);
        return box;
    }

    private static ComboBox CreateBreakIntervalBox(int value)
    {
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 132,
            BackColor = UiTheme.SurfaceColor,
            ForeColor = UiTheme.TextColor,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f),
            Anchor = AnchorStyles.Right,
        };
        box.Items.AddRange(["1 小时", "1.5 小时", "2 小时", "3 小时"]);
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

    private static Label CreateSectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11.5f, FontStyle.Bold),
        ForeColor = UiTheme.TextColor,
        BackColor = Color.Transparent,
        Margin = Padding.Empty,
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

    private void BeginBreakTransition()
    {
        breakDetails.Visible = true;
        targetBreakHeight = breakReminderToggle.Checked ? ExpandedBreakHeight : CollapsedBreakHeight;
        breakAnimationTimer.Start();
    }

    private void AnimateBreakCard()
    {
        var current = (int)breakRowStyle.Height;
        var distance = targetBreakHeight - current;
        if (Math.Abs(distance) <= 10)
        {
            breakRowStyle.Height = targetBreakHeight;
            breakAnimationTimer.Stop();
            breakDetails.Enabled = breakReminderToggle.Checked;
            breakDetails.Visible = breakReminderToggle.Checked;
            return;
        }

        breakRowStyle.Height = current + Math.Sign(distance) * 10;
    }

    private void AnimateOpening()
    {
        Opacity = Math.Min(1, Opacity + 0.09);
        if (Opacity >= 1)
        {
            openingTimer.Stop();
        }
    }
}
