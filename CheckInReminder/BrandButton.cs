using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal enum BrandButtonKind
{
    Primary,
    Secondary,
}

internal sealed class BrandButton : Button
{
    private int cornerRadius = 16;

    [DefaultValue(16)]
    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = value;
            UpdateRegion();
        }
    }

    public BrandButton(BrandButtonKind kind = BrandButtonKind.Primary)
    {
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 1;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f, FontStyle.Bold);
        Padding = new Padding(10, 0, 10, 0);

        if (kind == BrandButtonKind.Primary)
        {
            BackColor = UiTheme.PrimaryButtonBackColor;
            ForeColor = UiTheme.PrimaryButtonForeColor;
            FlatAppearance.BorderColor = UiTheme.AccentColor;
            FlatAppearance.MouseOverBackColor = UiTheme.AccentHoverColor;
            FlatAppearance.MouseDownBackColor = UiTheme.AccentPressedColor;
        }
        else
        {
            BackColor = UiTheme.SecondaryButtonBackColor;
            ForeColor = UiTheme.SecondaryButtonForeColor;
            FlatAppearance.BorderColor = UiTheme.BorderColor;
            FlatAppearance.MouseOverBackColor = UiTheme.AccentSoftColor;
            FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 211, 177);
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedPanel.CreateRoundedPath(new Rectangle(0, 0, Width, Height), cornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }
}
