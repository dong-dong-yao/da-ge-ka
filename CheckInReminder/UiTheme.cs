namespace CheckInReminder;

public static class UiTheme
{
    public static string ProductName => "打个卡";
    public static string SettingsTitle => "打个卡设置";

    public static Color AccentColor { get; } = Color.FromArgb(177, 91, 31);
    public static Color AccentHoverColor { get; } = Color.FromArgb(198, 109, 43);
    public static Color AccentPressedColor { get; } = Color.FromArgb(145, 69, 21);
    public static Color AccentSoftColor { get; } = Color.FromArgb(248, 225, 198);
    public static Color BrandGoldColor { get; } = Color.FromArgb(239, 169, 66);
    public static Color WarmBackgroundColor { get; } = Color.FromArgb(249, 245, 238);
    public static Color SurfaceColor { get; } = Color.FromArgb(255, 253, 249);
    public static Color TextColor { get; } = Color.FromArgb(48, 36, 28);
    public static Color MutedTextColor { get; } = Color.FromArgb(102, 82, 68);
    public static Color BorderColor { get; } = Color.FromArgb(225, 211, 195);
    public static Color ShadowColor { get; } = Color.FromArgb(28, 91, 57, 32);
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
