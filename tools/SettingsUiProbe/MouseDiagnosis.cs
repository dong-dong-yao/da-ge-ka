using CheckInReminder;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;

internal static class MouseDiagnosis
{
    internal static int Run(string output, string package)
    {
        if (File.Exists(package))
        {
            using var source = CharacterMediaProcessor.ReadBitmap(package);
            Calibrate(output, source); return 0;
        }
        var character = CustomCharacterStore.LoadPackage(package);
        var folder = Path.Combine(package, "pet");
        using var current = new CustomPetRenderer(folder, character.CustomManifest!.Mouse, character.CustomManifest.UsesTemplateMouse);
        var movement = DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 1), MousePress = 1 };
        using var rest = current.Render(DesktopPetRigPose.Rest);
        using var moved = current.Render(movement);
        rest.Save(Path.Combine(output, "current-rest.png"));
        moved.Save(Path.Combine(output, "current-moved.png"));
        string? rejection = null;
        try { using var candidate = DesktopPetSpriteSet.LoadCustom(folder); }
        catch (InvalidDataException e) { rejection = e.Message; }
        // Diagnostic only: deliberately bypass validation to show why forcing old coordinates is unsafe.
        var constructor = typeof(DesktopPetSpriteSet).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var poses = new[] { "idle", "left", "center", "right" }.Select(n => CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, n + ".png"))).ToArray();
        using var forced = (DesktopPetSpriteSet)constructor.Invoke(poses);
        using var forcedRest = forced.Render(DesktopPetRigPose.Rest);
        using var forcedMoved = forced.Render(movement);
        forcedRest.Save(Path.Combine(output, "diagnostic-forced-template-rest.png"), ImageFormat.Png);
        forcedMoved.Save(Path.Combine(output, "diagnostic-forced-template-moved.png"), ImageFormat.Png);
        Calibrate(output, rest);
        var changed = 0;
        for (var y = 0; y < rest.Height; y++)
        for (var x = 0; x < rest.Width; x++) if (rest.GetPixel(x, y) != moved.GetPixel(x, y)) changed++;
        var result = JsonSerializer.Serialize(new { Name = character.CustomManifest.Name, character.CustomManifest.UsesTemplateMouse, CurrentMouseChangedPixels = changed, TemplateRejection = rejection }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "diagnosis.json"), result); Console.WriteLine(result);
        return 0;
    }
    private static void Calibrate(string output, Bitmap rest)
    {
        using var selected = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(selected)) g.FillPolygon(Brushes.White,
            new Point[] { new(184, 220), new(241, 271), new(219, 291), new(194, 308), new(183, 341), new(161, 358), new(130, 365), new(105, 359), new(93, 342), new(93, 316), new(109, 296), new(127, 280), new(141, 258), new(164, 235) });
        var calibration = new MouseCalibration { Rig = new() { ShoulderX = 197f / 600, ShoulderY = 257f / 448, MouseX = 136f / 600, MouseY = 330f / 448 },
            Pad = [new(115, 271), new(259, 296), new(185, 391), new(13, 341)] };
        using var calibrated = CalibratedMouseLayers.Create(rest, selected, calibration);
        calibrated.Save(output, "calibrated-idle");
        foreach (var (name, offset) in new[] { ("left", new PointF(-1, -1)), ("right", new PointF(1, 1)) })
        { using var frame = calibrated.Render(DesktopPetRigPose.Rest with { MouseOffset = offset, MousePress = 1 }); frame.Save(Path.Combine(output, "calibrated-" + name + ".png")); }
        var frames = Path.Combine(output, "motion-frames"); Directory.CreateDirectory(frames);
        for (var i = 0; i < 48; i++)
        {
            var phase = i * MathF.PI / 24;
            using var rendered = calibrated.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(MathF.Sin(phase), MathF.Cos(phase)), MousePress = i % 24 > 18 ? 1 : 0 });
            using var opaque = new Bitmap(600, 448);
            using (var g = Graphics.FromImage(opaque)) { g.Clear(Color.FromArgb(232, 234, 237)); g.DrawImageUnscaled(rendered, 0, 0); }
            opaque.Save(Path.Combine(frames, $"frame_{i:0000}.png"));
        }
        Task.Run(() => BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", "-v", "error", "-framerate", "12", "-i", Path.Combine(frames, "frame_%04d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p", Path.Combine(output, "calibrated-motion.mp4")], CancellationToken.None)).GetAwaiter().GetResult();
        var fixtures = Path.Combine(output, "dialog-fixtures"); Directory.CreateDirectory(fixtures);
        var specs = new Dictionary<string, MouseCalibration>();
        foreach (var key in new[] { "idle", "left", "center", "right" })
        { rest.Save(Path.Combine(fixtures, key + ".png")); selected.Save(Path.Combine(fixtures, "mask-" + key + ".png")); specs[key] = calibration; }
        using var dialog = new MouseCalibrationDialog(fixtures, specs);
        dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000); dialog.Show(); Application.DoEvents();
        typeof(MouseCalibrationDialog).GetMethod("Animate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog, null);
        using var screenshot = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size)); screenshot.Save(Path.Combine(output, "calibration-dialog.png"));
    }
}
