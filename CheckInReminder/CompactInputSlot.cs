namespace CheckInReminder;

/// <summary>输入框固定逻辑高度与最大宽度，避免表格剩余空间拉伸胶囊。</summary>
internal sealed class CompactInputSlot : Panel
{
    private readonly Control input;
    public CompactInputSlot(Control input)
    {
        this.input = input;
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
        input.Dock = DockStyle.None;
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        input.Margin = Padding.Empty;
        Controls.Add(input);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (input is null) return;
        var scale = DeviceDpi / 96f;
        input.Bounds = new Rectangle(0, 0, Math.Max(1, Math.Min(ClientSize.Width, (int)Math.Round(138 * scale))),
            Math.Max(1, Math.Min(ClientSize.Height, (int)Math.Round(34 * scale))));
    }
}
