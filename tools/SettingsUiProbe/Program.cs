using CheckInReminder;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--unaware"))
        {
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        else
        {
            var configuration = typeof(AnimationCatalog).Assembly.GetTypes()
                .Single(type => type.Name == "ApplicationConfiguration");
            configuration.GetMethod("Initialize", System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null);
        }
        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        if (args.Contains("--notices"))
        {
            using var notices = new ThirdPartyNoticesForm();
            notices.StartPosition = FormStartPosition.Manual;
            notices.Location = new Point(-20000, -20000);
            notices.Show();
            Application.DoEvents();
            using var capture = new Bitmap(notices.Width, notices.Height);
            notices.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
            capture.Save(Path.Combine(output, "usage-notice.png"));
            return notices.Controls.OfType<TextBox>().Single().Text.Contains("使用声明与免责声明") && !notices.ShowInTaskbar ? 0 : 1;
        }
        if (args.Contains("--selection")) return SelectionProbe.Run(output, args[^1]);
        if (args.Contains("--mouse-diagnosis")) return MouseDiagnosis.Run(output, args[^1]);
        if (args.Contains("--creator")) return CreatorProbe.Run(output);
        var settings = AppSettings.CreateDefault();
        settings.BreakReminderEnabled = true;
        using var form = new SettingsForm(settings, _ => null, () => { });
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.Show();
        if (args.Contains("--gallery"))
        {
            typeof(SettingsForm).GetMethod("ShowPage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(form, ["characters"]);
            Application.DoEvents();
            using var capture = new Bitmap(form.Width, form.Height); form.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
            capture.Save(Path.Combine(output, "gallery.png"));
            if (form.ShowInTaskbar || form.Icon is null) throw new Exception("Settings branding contract");
            return 0;
        }
        var page = Descendants(form).OfType<BufferedScrollPanel>().Single();
        Descendants(form).OfType<CurrentCharacterPreviewControl>().Single().SetInteractionPaused(true);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(output, $"settings-{form.DeviceDpi}dpi.png"));
        var inputs = Descendants(page).Where(c => c is SoftTimePicker or SoftComboBox)
            .Select(c => new { c.AccessibleName, c.Width, c.Height, c.DeviceDpi,
                LogicalWidth = c.Width * 96d / c.DeviceDpi, LogicalHeight = c.Height * 96d / c.DeviceDpi }).ToArray();
        var result = new { form.DeviceDpi, form.ClientSize, Inputs = inputs };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "measurements.json"), json);
        Console.WriteLine(json);
        return inputs.All(c => c.LogicalWidth <= 140 && c.LogicalHeight <= 38 && c.LogicalHeight >= 30) ? 0 : 1;
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
