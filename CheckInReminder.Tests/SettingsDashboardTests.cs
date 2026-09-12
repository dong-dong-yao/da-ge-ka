using System.Drawing;
using System.Windows.Forms;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SettingsDashboardTests
{
    [STATestMethod]
    public void EnabledToggle_FirstPaintAlreadyShowsEnabledState()
    {
        using var toggle = new ToggleSwitch { Checked = true };
        using var image = new Bitmap(toggle.Width, toggle.Height);
        toggle.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        var track = image.GetPixel(12, image.Height / 2);
        Assert.AreEqual(UiTheme.AccentColor.ToArgb(), track.ToArgb(), "Initial enabled state must not animate from an off-looking switch");
    }

    [STATestMethod]
    [DataRow(980, 700)]
    [DataRow(1280, 900)]
    [DataRow(860, 600)]
    public void SettingsDashboard_ControlsFitAndSaveOriginalValues(int width, int height)
    {
        var original = AppSettings.CreateDefault();
        original.DesktopPetEnabled = true;
        original.BreakReminderEnabled = true;
        AppSettings? saved = null;
        using var form = new SettingsForm(original, value => { saved = value; return null; }, () => { });
        form.ClientSize = new Size(width, height);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.Show();
        var page = Descendants(form).OfType<BufferedScrollPanel>().Single();
        var output = Environment.GetEnvironmentVariable("SETTINGS_DASHBOARD_PROBE_DIR");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, $"settings-{width}x{height}.png"));
        }
        Assert.IsFalse(page.HorizontalScroll.Visible);
        if (width >= 980)
            Assert.IsFalse(page.VerticalScroll.Visible, "Default window should show every setting without a long scroll");
        var inputs = Descendants(page).Where(c => c is SoftTimePicker or SoftComboBox).ToArray();
        Assert.HasCount(8, inputs);
        foreach (var input in inputs)
        {
            Assert.IsTrue(input.Parent!.ClientRectangle.Contains(input.Bounds), $"Clipped input: {input.Bounds}");
            Assert.IsGreaterThanOrEqualTo(90, input.Width);
            Assert.IsGreaterThanOrEqualTo(30, input.Height);
            Assert.IsLessThanOrEqualTo(138d, input.Width * 96d / input.DeviceDpi);
            Assert.IsLessThanOrEqualTo(34d, input.Height * 96d / input.DeviceDpi);
        }
        Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "保存全部设置").PerformClick();
        Assert.IsNotNull(saved);
        foreach (var property in typeof(AppSettings).GetProperties())
            Assert.AreEqual(property.GetValue(original), property.GetValue(saved), property.Name);
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
