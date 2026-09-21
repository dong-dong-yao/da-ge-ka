namespace CheckInReminder;

/// <summary>Shared window branding; auxiliary dialogs do not create taskbar entries.</summary>
internal class BrandedForm : Form
{
    private readonly Icon ownedIcon;
    internal BrandedForm()
    {
        using var stream = typeof(BrandedForm).Assembly.GetManifestResourceStream("CheckInReminder.Dagaka.ico")
            ?? throw new InvalidOperationException("缺少应用图标。");
        ownedIcon = new Icon(stream);
        Icon = ownedIcon;
        ShowInTaskbar = false;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) ownedIcon.Dispose();
    }
}
