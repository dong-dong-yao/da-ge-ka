namespace CheckInReminder;

internal sealed class ThirdPartyNoticesForm : BrandedForm
{
    public ThirdPartyNoticesForm()
    {
        Text = "使用声明与许可";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(12);
        BackColor = SystemColors.Window;

        using var stream = typeof(ThirdPartyNoticesForm).Assembly.GetManifestResourceStream(
            "CheckInReminder.THIRD_PARTY_NOTICES.md")
            ?? throw new InvalidOperationException("缺少嵌入的第三方许可资源。");
        using var reader = new StreamReader(stream);
        var content = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            TabStop = false,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            AccessibleName = "使用声明与第三方许可全文",
            Text = reader.ReadToEnd().ReplaceLineEndings(Environment.NewLine),
        };
        Controls.Add(content);
        content.Select(0, 0);
        Shown += (_, _) => { content.Select(0, 0); content.ScrollToCaret(); };
    }
}
