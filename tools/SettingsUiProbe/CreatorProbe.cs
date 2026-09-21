using CheckInReminder;
using System.Reflection;
using System.Drawing.Imaging;

internal static class CreatorProbe
{
    public static int Run(string output)
    {
        var samples = Path.Combine(output, "sample-materials"); Directory.CreateDirectory(samples);
        MakeImages(samples);
        var video = Path.Combine(samples, "提醒示例.mp4");
        Task.Run(() => BundledMediaTools.RunAsync("ffmpeg", ["-nostdin", "-y", "-v", "error", "-loop", "1", "-i", Path.Combine(samples, "notify.png"), "-t", "3", "-r", "12", "-pix_fmt", "yuv420p", video], CancellationToken.None)).GetAwaiter().GetResult();
        var request = new CharacterCreationRequest
        {
            Name = "示例小黄", VideoPath = video, Background = BackgroundRemoval.Green,
            AllowedEdges = Enum.GetValues<ScreenEdge>(),
            PetImages = new[] { "idle", "left", "right", "center" }.ToDictionary(k => k, k => Path.Combine(samples, k + ".png")),
            StrictPetPng = true, UseTemplateMouse = true
        };
        using var prepared = Task.Run(() => new CharacterCreationService(new CustomCharacterStore(Path.Combine(output, "probe-store"))).PrepareAsync(request, null, CancellationToken.None)).GetAwaiter().GetResult();
        using var form = new CharacterCreationWizard();
        var type = form.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((TextBox)type.GetField("nameBox", flags)!.GetValue(form)!).Text = request.Name;
        ((TextBox)type.GetField("video", flags)!.GetValue(form)!).Text = video;
        var petFiles = (Dictionary<string, TextBox>)type.GetField("petFiles", flags)!.GetValue(form)!;
        foreach (var (key, path) in request.PetImages) petFiles[key].Text = path;
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show();
        for (var step = 0; step < 4; step++)
        {
            type.GetMethod("ShowStep", flags)!.Invoke(form, [step]);
            if (step == 3)
            {
                type.GetField("prepared", flags)!.SetValue(form, prepared);
                type.GetField("previewPet", flags)!.SetValue(form, new CustomPetRenderer(Path.Combine(prepared.Character.CustomPackagePath!, "pet"), request.Mouse, useTemplateMouse: true));
                type.GetField("showPet", flags)!.SetValue(form, true);
                ((Button)type.GetField("mouseAdjust", flags)!.GetValue(form)!).Visible = true;
                ((LinkLabel)type.GetField("mouseFallback", flags)!.GetValue(form)!).Visible = true;
                type.GetMethod("RenderPreview", flags)!.Invoke(form, null);
            }
            Application.DoEvents();
            File.WriteAllLines(Path.Combine(output, $"layout-{step}.txt"), Descendants(form).Select(c => $"{c.GetType().Name} {c.Bounds} visible={c.Visible} parent={c.Parent?.GetType().Name} text={c.Text.Replace(Environment.NewLine, " ")}"));
            Capture(form, Path.Combine(output, $"creator-step-{step + 1}.png"));
            if (step is 0 or 2)
            {
                var guide = Descendants(form).OfType<AssetPreparationGuide>().Single();
                AssertGuidePrompt(guide, step == 0 ? CharacterCreationTutorial.ThreeViewPrompt : CharacterCreationTutorial.IdlePrompt);
                Descendants(guide).OfType<Button>().Single(b => b.Text.StartsWith("②", StringComparison.Ordinal)).PerformClick();
                Application.DoEvents();
                AssertGuidePrompt(guide, step == 0 ? CharacterCreationTutorial.VideoPrompt : CharacterCreationTutorial.PressPrompt);
                Capture(form, Path.Combine(output, $"creator-step-{step + 1}-guide-2.png"));
                if (File.ReadAllBytes(Path.Combine(output, $"creator-step-{step + 1}.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(output, $"creator-step-{step + 1}-guide-2.png"))))
                    throw new InvalidOperationException("Guide page screenshots must differ.");
                Descendants(guide).OfType<Button>().Single(b => b.Text.StartsWith("①", StringComparison.Ordinal)).PerformClick();
            }
            if (step == 1)
            {
                var extra = Descendants(form).OfType<CheckBox>().Single(c => c.Text.StartsWith("为某个方向上传不同视频", StringComparison.Ordinal));
                var originalSize = form.ClientSize;
                extra.Checked = true; Application.DoEvents();
                var scrollHost = (Panel)type.GetField("host", flags)!.GetValue(form)!;
                scrollHost.AutoScrollPosition = new Point(0, scrollHost.Controls[0].Height); Application.DoEvents();
                if (scrollHost.Controls[0].Bottom > scrollHost.ClientSize.Height + 20 || scrollHost.AutoScrollPosition.Y >= -100)
                    throw new Exception("Expanded uploads cannot scroll to the bottom without resizing");
                Capture(form, Path.Combine(output, "directions-bottom-without-resize.png"));
                extra.Checked = false; Application.DoEvents(); scrollHost.AutoScrollPosition = Point.Empty;
                for (var cycle = 0; cycle < 40; cycle++)
                {
                    extra.Checked = !extra.Checked;
                    form.ClientSize = cycle % 2 == 0 ? new Size(900, 720) : originalSize;
                    Application.DoEvents();
                    var pageHost = (Panel)type.GetField("host", flags)!.GetValue(form)!;
                    if (pageHost.Controls.Count != 1 || !pageHost.Controls[0].Visible || pageHost.Controls[0].Height < 100 || pageHost.Controls[0].Width < 100)
                        throw new Exception("Direction layout disappeared during toggle/resize stress");
                    if (!pageHost.ClientRectangle.IntersectsWith(pageHost.Controls[0].Bounds)) throw new Exception("Page moved outside viewport");
                }
                form.ClientSize = originalSize; extra.Checked = true;
                Application.DoEvents(); Capture(form, Path.Combine(output, "creator-directions-expanded.png"));
            }
            if (step == 2)
            {
                var host = (Panel)type.GetField("host", flags)!.GetValue(form)!;
                host.AutoScrollPosition = new Point(0, host.DisplayRectangle.Height);
                Application.DoEvents();
                Capture(form, Path.Combine(output, "creator-pet-uploads.png"));
                host.AutoScrollPosition = Point.Empty;
            }
            var regularSize = form.ClientSize;
            form.ClientSize = new Size(780, 620);
            Application.DoEvents();
            Capture(form, Path.Combine(output, $"creator-step-{step + 1}-narrow.png"));
            form.ClientSize = regularSize;
            ((Panel)type.GetField("host", flags)!.GetValue(form)!).AutoScrollPosition = Point.Empty;
            Application.DoEvents();
        }
        using var renderer = new CustomPetRenderer(Path.Combine(prepared.Character.CustomPackagePath!, "pet"), request.Mouse, useTemplateMouse: true);
        foreach (var (label, pose) in new[]
        {
            ("rest", DesktopPetRigPose.Rest),
            ("mouse-right-click", DesktopPetRigPose.Rest with { MouseOffset = new PointF(1, 1), MousePress = 1 }),
            ("key-left-mouse-left", DesktopPetRigPose.Rest with { KeyboardContact = true, KeyboardTarget = new PointF(.1f, .5f), MouseOffset = new PointF(-1, -1) })
        }) { using var frame = renderer.Render(pose); frame.Save(Path.Combine(output, label + ".png")); }
        Console.WriteLine("Creator screenshots and real video import probe passed.");
        return 0;
    }
    private static void AssertGuidePrompt(AssetPreparationGuide guide, string expected)
    {
        var prompt = Descendants(guide).OfType<Label>().Single(box => box.Visible && box.AccessibleName == "制作提示词");
        if (prompt.Text != expected.Replace("\n", Environment.NewLine))
            throw new InvalidOperationException("The selected guide does not show its expected prompt.");
    }
    private static IEnumerable<Control> Descendants(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Capture(Form form, string file)
    { using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(file, ImageFormat.Png); }
    private static void MakeImages(string folder)
    {
        if (!DesktopPetSpriteSet.TryLoad("yellow-hippo", out var source)) throw new InvalidOperationException("Missing fixture");
        using (source)
        {
            source.Idle.Save(Path.Combine(folder, "idle.png"), ImageFormat.Png);
            foreach (var item in new[] { ("left", PetKeyboardPose.Left), ("center", PetKeyboardPose.Center), ("right", PetKeyboardPose.Right) })
                source.GetPressed(item.Item2).Save(Path.Combine(folder, item.Item1 + ".png"), ImageFormat.Png);
        }
        foreach (var name in new[] { "notify" })
        {
            using var b = new Bitmap(600, 448); using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(name == "notify" ? Color.Lime : Color.Transparent);
                using var body = new SolidBrush(Color.FromArgb(103, 165, 217));
                using var dark = new Pen(Color.FromArgb(37, 65, 92), 5);
                if (name == "mouse")
                {
                    g.FillEllipse(Brushes.SlateGray, 437, 316, 58, 37);
                    g.DrawEllipse(dark, 437, 316, 58, 37);
                    using var armPen = new Pen(body, 38) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                    g.DrawLine(armPen, 378, 224, 464, 321);
                }
                else
                {
                    g.FillEllipse(body, 190, 115, 220, 200); g.DrawEllipse(dark, 190, 115, 220, 200);
                    g.FillEllipse(Brushes.White, 239, 168, 30, 40); g.FillEllipse(Brushes.White, 329, 168, 30, 40);
                    g.FillEllipse(Brushes.Black, 249, 180, 13, 18); g.FillEllipse(Brushes.Black, 339, 180, 13, 18);
                    g.DrawArc(dark, 272, 203, 49, 37, 10, 160);
                    g.FillRectangle(Brushes.Bisque, 80, 303, 450, 65);
                    g.FillRectangle(Brushes.SlateGray, 145, 311, 246, 38);
                    for (var x = 152; x < 383; x += 21) g.DrawRectangle(Pens.White, x, 318, 14, 12);
                    var end = name switch { "left" => new Point(170, 320), "center" => new Point(265, 320), "right" => new Point(354, 320), _ => new Point(210, 273) };
                    using var armPen = new Pen(body, 35) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                    g.DrawLine(armPen, 221, 243, end.X, end.Y);
                }
            }
            b.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
        }
    }
}
