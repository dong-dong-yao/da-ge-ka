namespace CheckInReminder;

internal sealed class ThirdPartyNoticesForm : Form
{
    public ThirdPartyNoticesForm()
    {
        Text = "第三方许可";
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
        Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            AccessibleName = "第三方许可全文",
            Text = reader.ReadToEnd(),
        });
    }
}
