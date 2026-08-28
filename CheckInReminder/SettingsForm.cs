using System.Globalization;

namespace CheckInReminder;

internal sealed class SettingsForm : Form
{
    private readonly DateTimePicker morningStartPicker;
    private readonly DateTimePicker morningEndPicker;
    private readonly ComboBox morningIntervalBox;
    private readonly DateTimePicker eveningStartPicker;
    private readonly ComboBox eveningIntervalBox;
    private readonly CheckBox autoStartCheckBox;
    private readonly Func<AppSettings, string?> saveSettings;
    private readonly Action testReminder;

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
        ClientSize = new Size(460, 500);
        MinimumSize = new Size(476, 555);
        BackColor = UiTheme.WarmBackgroundColor;
        ForeColor = UiTheme.TextColor;
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10);

        morningStartPicker = CreateTimePicker(settings.MorningStart);
        morningEndPicker = CreateTimePicker(settings.MorningEnd);
        morningIntervalBox = CreateIntervalBox(settings.MorningIntervalMinutes);
        eveningStartPicker = CreateTimePicker(settings.EveningStart);
        eveningIntervalBox = CreateIntervalBox(settings.EveningIntervalMinutes);
        autoStartCheckBox = new CheckBox
        {
            Text = "开机自启动",
            Checked = settings.AutoStart,
            AutoSize = true,
            ForeColor = UiTheme.TextColor,
            BackColor = UiTheme.WarmBackgroundColor,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 10,
            AutoScroll = true,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            BackColor = UiTheme.WarmBackgroundColor,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        for (var row = 0; row < 8; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddSection(layout, "上午提醒", 0);
        AddRow(layout, "开始时间", morningStartPicker, 1);
        AddRow(layout, "结束时间", morningEndPicker, 2);
        AddRow(layout, "提醒间隔（分钟）", morningIntervalBox, 3);
        AddSection(layout, "晚上提醒", 4);
        AddRow(layout, "开始时间", eveningStartPicker, 5);
        AddRow(layout, "提醒间隔（分钟）", eveningIntervalBox, 6);
        layout.Controls.Add(autoStartCheckBox, 0, 7);
        layout.SetColumnSpan(autoStartCheckBox, 2);

        var testButton = new Button { Text = "测试提醒", Size = new Size(118, 44) };
        UiTheme.StyleSecondaryButton(testButton);
        testButton.Click += (_, _) => this.testReminder();
        var saveButton = new Button { Text = "保存", Size = new Size(100, 44) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();
        AcceptButton = saveButton;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(testButton);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);
        var brandHeader = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.AccentColor,
            Margin = Padding.Empty,
        };
        brandHeader.Controls.Add(new Label
        {
            Text = "打个卡  ·  提醒设置",
            Dock = DockStyle.Fill,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 16, FontStyle.Bold),
            ForeColor = UiTheme.TextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(24, 0, 0, 0),
        });

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = UiTheme.WarmBackgroundColor,
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(brandHeader, 0, 0);
        shell.Controls.Add(layout, 0, 1);
        Controls.Add(shell);
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
            AutoStart = autoStartCheckBox.Checked,
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
        }
    }

    private static DateTimePicker CreateTimePicker(TimeOnly value) => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm",
        ShowUpDown = true,
        Value = DateTime.Today.Add(value.ToTimeSpan()),
        Width = 100,
        CalendarForeColor = UiTheme.TextColor,
        CalendarMonthBackground = UiTheme.SurfaceColor,
    };

    private static ComboBox CreateIntervalBox(int value)
    {
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Width = 100,
            BackColor = UiTheme.SurfaceColor,
            ForeColor = UiTheme.TextColor,
        };
        box.Items.AddRange(["5", "10", "15", "30"]);
        box.Text = value.ToString(CultureInfo.InvariantCulture);
        return box;
    }

    private static void AddSection(TableLayoutPanel layout, string text, int row)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            AutoSize = true,
            ForeColor = UiTheme.TextColor,
            BackColor = UiTheme.WarmBackgroundColor,
            Margin = new Padding(0, row == 0 ? 0 : 14, 0, 4),
        };
        layout.Controls.Add(label, 0, row);
        layout.SetColumnSpan(label, 2);
    }

    private static void AddRow(TableLayoutPanel layout, string labelText, Control control, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.TextColor,
            BackColor = UiTheme.WarmBackgroundColor,
        }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
