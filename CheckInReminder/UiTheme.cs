namespace CheckInReminder;

public static class UiTheme
{
    public static string ProductName => "打个卡";
    public static string SettingsTitle => "打个卡设置";

    public static Color AccentColor { get; } = Color.FromArgb(167, 104, 47);
    public static Color AccentHoverColor { get; } = Color.FromArgb(188, 121, 58);
    public static Color AccentPressedColor { get; } = Color.FromArgb(140, 84, 35);
    public static Color AccentSoftColor { get; } = Color.FromArgb(243, 227, 204);
    public static Color BrandGoldColor { get; } = Color.FromArgb(217, 158, 90);
    public static Color WarmBackgroundColor { get; } = Color.FromArgb(247, 240, 230);
    public static Color SurfaceColor { get; } = Color.FromArgb(255, 252, 247);
    public static Color TextColor { get; } = Color.FromArgb(74, 53, 38);
    public static Color MutedTextColor { get; } = Color.FromArgb(138, 114, 93);
    public static Color BorderColor { get; } = Color.FromArgb(235, 223, 207);
    public static Color BorderStrongColor { get; } = Color.FromArgb(213, 190, 158);
    public static Color ShadowColor { get; } = Color.FromArgb(22, 120, 80, 40);
    public static Color PrimaryButtonBackColor => AccentColor;
    public static Color PrimaryButtonForeColor => Color.White;
    public static Color SecondaryButtonBackColor => SurfaceColor;
    public static Color SecondaryButtonForeColor => TextColor;

    public static void StylePrimaryButton(Button button)
    {
        StyleButton(button, PrimaryButtonBackColor, PrimaryButtonForeColor);
        button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        button.FlatAppearance.MouseDownBackColor = AccentPressedColor;
    }

    public static void StyleSecondaryButton(Button button)
    {
        StyleButton(button, SecondaryButtonBackColor, SecondaryButtonForeColor);
        button.FlatAppearance.MouseOverBackColor = AccentSoftColor;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 211, 177);
    }

    public static double ContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) +
            (0.7152 * Channel(color.G)) +
            (0.0722 * Channel(color.B));
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }
}
