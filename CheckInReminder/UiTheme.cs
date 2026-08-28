namespace CheckInReminder;

public static class UiTheme
{
    public static string ProductName => "打个卡";
    public static string SettingsTitle => "打个卡设置";

    public static int BannerWidth => 400;
    public static int BannerHeight => 480;
    public static int BannerImageHeight => 400;
    public static int BannerButtonTop => 410;

    public static Color AccentColor { get; } = Color.FromArgb(255, 205, 32);
    public static Color AccentHoverColor { get; } = Color.FromArgb(255, 220, 82);
    public static Color WarmBackgroundColor { get; } = Color.FromArgb(255, 250, 232);
    public static Color SurfaceColor { get; } = Color.FromArgb(255, 255, 250);
    public static Color TextColor { get; } = Color.FromArgb(30, 30, 30);
    public static Color MutedTextColor { get; } = Color.FromArgb(92, 82, 58);
    public static Color BorderColor { get; } = Color.FromArgb(45, 45, 45);
    public static Color PrimaryButtonBackColor => AccentColor;
    public static Color PrimaryButtonForeColor => TextColor;
    public static Color SecondaryButtonBackColor => Color.White;
    public static Color SecondaryButtonForeColor => TextColor;

    public static void StylePrimaryButton(Button button)
    {
        StyleButton(button, PrimaryButtonBackColor, PrimaryButtonForeColor);
        button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 181, 0);
    }

    public static void StyleSecondaryButton(Button button)
    {
        StyleButton(button, SecondaryButtonBackColor, SecondaryButtonForeColor);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 242, 181);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 224, 102);
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 2;
        button.Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }
}
