using CheckInReminder;
using System.Reflection;
using System.Text.Json;

internal static class SelectionProbe
{
    internal static int Run(string output, string package)
    {
        var packaged = File.Exists(Path.Combine(package, "manifest.json"));
        var specs = packaged ? CustomCharacterStore.LoadPackage(package).CustomManifest!.MouseCalibrations! : new Dictionary<string, MouseCalibration>();
        var folder = packaged ? Path.Combine(package, "pet") : package;
        var results = new List<object>();
        foreach (var key in new[] { "idle", "left", "center", "right" })
        {
            using var source = CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, "original-" + key + ".png"));
            using var suggested = MouseCalibrationDialog.Suggest(source);
            if (!packaged) specs[key] = suggested.Spec;
            using var mask = packaged ? CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, "mask-" + key + ".png")) : new Bitmap(suggested.Mask);
            source.Save(Path.Combine(output, "read-source-" + key + ".png"));
            var detected = JsonSerializer.Deserialize<MouseCalibration>(JsonSerializer.Serialize(specs[key]))!;
            if (!MouseSelectionTools.TrySelectPad(source, detected, null)) throw new Exception("Pad not found: " + key);
            using (var diagram = new Bitmap(source))
            {
                using var g = Graphics.FromImage(diagram); using var pen = new Pen(Color.Red, 2);
                g.DrawPolygon(pen, detected.Pad.Select(p => new PointF(p.X, p.Y)).ToArray());
                diagram.Save(Path.Combine(output, key + "-auto-pad.png"));
            }
            using var detectedLayers = CalibratedMouseLayers.Create(source, mask, detected);
            using var before = CalibratedMouseLayers.Create(source, mask, specs[key]);
            var added = MouseSelectionTools.CompleteDarkEdges(source, mask, specs[key]);
            var repeated = MouseSelectionTools.CompleteDarkEdges(source, mask, specs[key]);
            using var after = CalibratedMouseLayers.Create(source, mask, specs[key]);
            after.Save(output, key);
            var pose = DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 1), MousePress = 1 };
            using var a = before.Render(pose); using var b = after.Render(pose);
            using var comparison = new Bitmap(1200, 478);
            using (var g = Graphics.FromImage(comparison))
            {
                g.Clear(Color.FromArgb(231, 234, 230)); g.DrawImageUnscaled(a, 0, 30); g.DrawImageUnscaled(b, 600, 30);
                using var font = new Font("Microsoft YaHei UI", 12);
                g.DrawString("补边前", font, Brushes.Black, 12, 4); g.DrawString("补边后", font, Brushes.Black, 612, 4);
            }
            comparison.Save(Path.Combine(output, key + "-comparison.png"));
            results.Add(new { key, added, repeated });
            if (repeated != 0) throw new Exception("Repeated edge completion grew selection");
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var dialog = new MouseCalibrationDialog(folder, packaged ? specs : null);
        if (dialog.ShowInTaskbar || dialog.Icon is null) throw new Exception("Window branding/taskbar contract");
        dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000); dialog.Show();
        typeof(MouseCalibrationDialog).GetMethod("Animate", flags)!.Invoke(dialog, null); Application.DoEvents();
        Capture(dialog, Path.Combine(output, "mouse-editor.png"));
        var canvas = (Control)typeof(MouseCalibrationDialog).GetField("canvas", flags)!.GetValue(dialog)!;
        var type = canvas.GetType(); type.GetProperty("Mode", flags)!.SetValue(canvas, 1);
        var rect = (RectangleF)type.GetProperty("Canvas", flags)!.GetValue(canvas)!;
        var point = new Point((int)(rect.X + rect.Width * .24f), (int)(rect.Y + rect.Height * .72f));
        type.GetMethod("OnMouseDown", flags)!.Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
        Application.DoEvents(); Capture(dialog, Path.Combine(output, "mouse-editor-loupe.png"));
        type.GetMethod("OnMouseUp", flags)!.Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
        if ((bool)type.GetProperty("IsDrawing", flags)!.GetValue(canvas)!) throw new Exception("Loupe did not stop after release");
        var rig = dialog.Edits["idle"].Spec.Rig;
        var shoulder = new PointF(rig.ShoulderX, rig.ShoulderY);
        rig.ShoulderX = rig.MouseX; rig.ShoulderY = rig.MouseY;
        Descendants(dialog).OfType<Button>().Single(b => b.Text == "补选黑色边缘").PerformClick();
        type.GetProperty("Mode", flags)!.SetValue(canvas, 6);
        type.GetMethod("OnMouseDown", flags)!.Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
        var message = (Label)typeof(MouseCalibrationDialog).GetField("message", flags)!.GetValue(dialog)!;
        if (!message.Text.Contains("重新设置")) throw new Exception("Invalid anchors need actionable feedback");
        rig.ShoulderX = shoulder.X; rig.ShoulderY = shoulder.Y;
        dialog.Size = dialog.MinimumSize; Application.DoEvents(); Capture(dialog, Path.Combine(output, "mouse-editor-small.png"));
        File.WriteAllText(Path.Combine(output, "selection-results.json"), JsonSerializer.Serialize(results));
        Console.WriteLine(JsonSerializer.Serialize(results)); return 0;
    }
    private static void Capture(Form form, string path) { using var b = new Bitmap(form.Width, form.Height); form.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save(path); }
    private static IEnumerable<Control> Descendants(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; } }
}
