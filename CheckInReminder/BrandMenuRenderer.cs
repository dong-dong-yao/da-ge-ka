namespace CheckInReminder;

internal sealed class BrandMenuRenderer : ToolStripProfessionalRenderer
{
    public BrandMenuRenderer()
        : base(new BrandColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
    {
        eventArgs.TextColor = UiTheme.TextColor;
        base.OnRenderItemText(eventArgs);
    }

    private sealed class BrandColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => UiTheme.WarmBackgroundColor;
        public override Color ImageMarginGradientBegin => UiTheme.WarmBackgroundColor;
        public override Color ImageMarginGradientMiddle => UiTheme.WarmBackgroundColor;
        public override Color ImageMarginGradientEnd => UiTheme.WarmBackgroundColor;
        public override Color MenuItemSelected => UiTheme.AccentColor;
        public override Color MenuItemSelectedGradientBegin => UiTheme.AccentColor;
        public override Color MenuItemSelectedGradientEnd => UiTheme.AccentColor;
        public override Color MenuItemBorder => UiTheme.BorderColor;
        public override Color MenuBorder => UiTheme.BorderColor;
        public override Color SeparatorDark => Color.FromArgb(210, 190, 120);
        public override Color SeparatorLight => UiTheme.SurfaceColor;
    }
}
