using System.Diagnostics;

namespace CheckInReminder;

internal sealed class CharacterCreationWizard : BrandedForm
{
    private readonly Label heading = new() { AutoSize = true, Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold) };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(680, 0), ForeColor = UiTheme.MutedTextColor };
    private readonly Panel host = new BufferedScrollPanel() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Button back = CreatorUi.Button("上一步");
    private readonly Button next = CreatorUi.Button("下一步 →", true);
    private readonly Button cancel = CreatorUi.Button("取消创建");
    private readonly Label[] navigation = new Label[4];
    private readonly FlowLayoutPanel[] pages = new FlowLayoutPanel[4];
    private readonly TextBox nameBox = new() { Width = 400, MaxLength = 40, AccessibleName = "角色名称" };
    private readonly TextBox video = new() { Width = 475, ReadOnly = true };
    private readonly ComboBox sourceEdge = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly Dictionary<ScreenEdge, CheckBox> edges = [];
    private readonly Dictionary<ScreenEdge, TextBox> directionFiles = [];
    private readonly Dictionary<string, TextBox> petFiles = [];
    private readonly CheckBox keyboardOnly = new() { Text = "仅保留键盘动作", AutoSize = true, Visible = false };
    private readonly LinkLabel mouseFallback = new() { Text = "鼠标效果不合适？改为仅键盘互动", AutoSize = true, Visible = false, Margin = new Padding(0, 8, 0, 8) };
    private readonly Button mouseAdjust = CreatorUi.Button("手动修复鼠标模块");
    private readonly PictureBox preview = new() { Size = new Size(520, 350), BackColor = Color.FromArgb(230, 232, 229), SizeMode = PictureBoxSizeMode.Zoom, TabStop = true, AccessibleName = "角色互动预览" };
    private readonly CheckBox selectAfter = new() { Text = "创建后选用这个角色（回到设置后保存生效）", AutoSize = true, Checked = true };
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 50 };
    private readonly Stopwatch previewClock = new();
    private PreparedCharacter? prepared;
    private AnimationSequence? previewSequence;
    private CustomPetRenderer? previewPet;
    private DesktopPetRigPose previewPose = DesktopPetRigPose.Rest;
    private CancellationTokenSource? processing;
    private readonly CancellationTokenSource lifetime = new();
    private int step;
    private int lastFrame = -1;
    private bool showPet;
    private ScreenEdge previewEdge = ScreenEdge.Right;
    private ScreenEdge previewSource = ScreenEdge.Right;
    public ReminderCharacter? CreatedCharacter { get; private set; }
    public bool SelectAfterCreation => selectAfter.Checked;

    public CharacterCreationWizard()
    {
        Text = "创建自己的角色 · 打个卡";
        Font = new Font("Microsoft YaHei UI", 10);
        BackColor = UiTheme.WarmBackgroundColor;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(DeviceDpi, DeviceDpi);
        DoubleBuffered = true;
        ClientSize = new Size(1180, 840); MinimumSize = new Size(780, 600);
        ForeColor = UiTheme.TextColor;
        StartPosition = FormStartPosition.CenterParent; KeyPreview = true;
        var header = new GradientHeaderPanel { Dock = DockStyle.Top, Height = 105, Padding = new Padding(26, 12, 20, 10) };
        var brand = CreatorUi.Text("创建你的提醒伙伴", true); brand.ForeColor = Color.White; brand.Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold); brand.Location = new Point(26, 12); brand.MaximumSize = Size.Empty;
        var subtitle = CreatorUi.Text("把喜欢的角色，变成每天陪你打卡的小伙伴。"); subtitle.ForeColor = Color.White; subtitle.Location = new Point(28, 65); subtitle.MaximumSize = Size.Empty;
        header.Controls.Add(brand); header.Controls.Add(subtitle);
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 204, Padding = new Padding(16, 36, 12, 16) };
        var steps = CreatorUi.Stack(); steps.Dock = DockStyle.Top;
        var names = new[] { "准备提醒视频", "选择出现位置", "添加桌面动作", "试一试，完成" };
        for (int i = 0; i < 4; i++)
        {
            navigation[i] = new Label { Text = $"{i + 1}  {names[i]}", Font = new Font("Microsoft YaHei UI", 9), AutoSize = false, Size = new Size(180, 64), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Margin = new Padding(0, 0, 0, 10) };
            steps.Controls.Add(navigation[i]);
        }
        sidebar.Controls.Add(steps);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(16, 8, 16, 8), FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(next); footer.Controls.Add(back); footer.Controls.Add(cancel);
        var titleArea = new TableLayoutPanel { Dock = DockStyle.Top, Height = 106, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 10, 12, 4) };
        heading.Margin = Padding.Empty; status.Margin = new Padding(0, 6, 0, 0); status.Dock = DockStyle.Fill; status.AutoSize = false;
        titleArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); titleArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        titleArea.Controls.Add(heading); titleArea.Controls.Add(status);
        var main = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        main.Controls.Add(host); main.Controls.Add(footer); main.Controls.Add(titleArea);
        Controls.Add(main); Controls.Add(sidebar); Controls.Add(header);
        for (var i = 0; i < pages.Length; i++) pages[i] = new FlowLayoutPanel
        { AutoSize = false, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 4, 0, 12) };
        host.SizeChanged += (_, _) => ResizePage();
        mouseAdjust.Visible = false;
        BuildBasics(); BuildDirections(); BuildPet(); BuildPreview();
        back.Click += (_, _) => { ClearPreview(); prepared?.Dispose(); prepared = null; ShowStep(Math.Max(0, step - 1)); };
        next.Click += async (_, _) => await NextAsync();
        cancel.Click += (_, _) => { if (processing is not null) processing.Cancel(); else Close(); };
        FormClosing += (_, e) => { if (processing is not null) { processing.Cancel(); e.Cancel = true; status.Text = "正在取消，请稍候……"; } };
        previewTimer.Tick += (_, _) => RenderPreview();
        KeyDown += (_, e) =>
        {
            if (step != 3 || !showPet || !KeyboardTargetMapper.TryMap((int)e.KeyCode, out var target)) return;
            previewPose = previewPose with { KeyboardContact = true, KeyboardPress = 1, KeyboardTarget = target }; e.Handled = true;
        };
        KeyUp += (_, _) => previewPose = previewPose with { KeyboardContact = false, KeyboardPress = 0 };
        Deactivate += (_, _) => previewPose = DesktopPetRigPose.Rest;
        ShowStep(0);
    }

    internal static string EdgeName(ScreenEdge edge) => edge switch { ScreenEdge.Left => "左侧", ScreenEdge.Top => "上方", ScreenEdge.Right => "右侧", _ => "下方" };
    private static Label Note(string text) => CreatorUi.Text(text);
    private Control FileRow(TextBox box, bool movie, bool optional = true, Action? changed = null)
    {
        return new CreatorFileCard(box, movie, optional, () =>
        {
            using var dialog = new OpenFileDialog { Filter = movie ? "提醒视频|*.mp4;*.mov;*.webm" : "透明 PNG 图片|*.png", CheckFileExists = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            box.Text = dialog.FileName; changed?.Invoke();
        }, changed);
    }
    private bool resizingPage;
    private void ResizePage()
    {
        if (resizingPage || pages[step] is null) return;
        resizingPage = true;
        host.SuspendLayout();
        try
        {
            var page = pages[step];
            int width = Math.Max(240, host.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 20);
            page.Width = width;
            foreach (Control child in page.Controls)
            {
                CreatorUi.Fit(child, width - page.Padding.Horizontal);
                if (child is CreatorColumns columns) columns.Arrange();
            }
            page.PerformLayout();
            page.Height = page.Controls.Cast<Control>().Select(c => c.Bottom + c.Margin.Bottom).DefaultIfEmpty(0).Max() + page.Padding.Bottom;
            page.Location = host.AutoScrollPosition;
        }
        // Commit the new content extent now; otherwise AutoScroll keeps its old
        // range until an unrelated window resize causes a host layout pass.
        finally { host.ResumeLayout(true); resizingPage = false; }
    }
    private void BuildBasics()
    {
        var guideCard = new CreatorCard(); guideCard.Content.Controls.Add(new AssetPreparationGuide(false));
        var upload = new CreatorCard("你的角色");
        upload.Content.Controls.Add(Note("角色名称")); upload.Content.Controls.Add(new CreatorNameInput(nameBox));
        upload.Content.Controls.Add(CreatorUi.Text("提醒视频", true));
        upload.Content.Controls.Add(FileRow(video, true, false));
        upload.Content.Controls.Add(Note("MP4 / MOV / WebM · 3–15 秒\n绿幕或透明背景，最大 150 MB。"));
        upload.Content.Controls.Add(Note("已有视频？直接上传就好。"));
        pages[0].Controls.Add(new CreatorColumns(guideCard, upload));
        guideCard.SizeChanged += (_, _) => ResizePage();
    }
    private void BuildDirections()
    {
        var original = new CreatorCard("视频里，它从哪边出来？");
        original.Content.Controls.Add(Note("按照你上传的视频选择。"));
        sourceEdge.Items.AddRange(Enum.GetValues<ScreenEdge>().Select(e => (object)EdgeName(e)).ToArray()); sourceEdge.SelectedIndex = 2;
        sourceEdge.Visible = false; original.Content.Controls.Add(sourceEdge);
        foreach (var edge in Enum.GetValues<ScreenEdge>())
        {
            var choice = new RadioButton { Text = EdgeName(edge), Checked = edge == ScreenEdge.Right, AutoSize = true, Padding = new Padding(12), Margin = new Padding(0, 5, 0, 5) };
            choice.CheckedChanged += (_, _) => { if (choice.Checked) sourceEdge.SelectedIndex = (int)edge; };
            original.Content.Controls.Add(choice);
        }
        var appear = new CreatorCard("提醒时，可以从哪些边出来？");
        appear.Content.Controls.Add(Note("可以多选，至少选一个。下一步还能查看效果。"));
        var screen = new TableLayoutPanel { Width = 300, Height = 208, ColumnCount = 3, RowCount = 3, Margin = new Padding(0, 10, 0, 20), BackColor = UiTheme.WarmBackgroundColor };
        for (int i = 0; i < 3; i++) { screen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); screen.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f)); }
        screen.Controls.Add(new Label { Text = "你的屏幕", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = UiTheme.MutedTextColor }, 1, 1);
        foreach (var edge in Enum.GetValues<ScreenEdge>())
        {
            var check = new CheckBox { Text = EdgeName(edge), Checked = edge == ScreenEdge.Right, AutoSize = true, Anchor = AnchorStyles.None };
            edges[edge] = check;
            var point = edge switch { ScreenEdge.Left => new Point(0, 1), ScreenEdge.Top => new Point(1, 0), ScreenEdge.Right => new Point(2, 1), _ => new Point(1, 2) };
            screen.Controls.Add(check, point.X, point.Y);
        }
        appear.Content.Controls.Add(screen);
        var separate = new CheckBox { Text = "为某个方向上传不同视频（选填）", AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        var extras = CreatorUi.Stack();
        foreach (var edge in Enum.GetValues<ScreenEdge>())
        {
            extras.Controls.Add(Note(EdgeName(edge)));
            var box = new TextBox { ReadOnly = true }; directionFiles[edge] = box;
            extras.Controls.Add(FileRow(box, true));
        }
        extras.Visible = false; separate.CheckedChanged += (_, _) =>
        {
            host.SuspendLayout(); appear.Content.SuspendLayout();
            extras.Visible = separate.Checked;
            appear.Content.ResumeLayout(true);
            host.ResumeLayout(false);
            ResizePage();
        };
        appear.Content.Controls.Add(separate); appear.Content.Controls.Add(extras);
        pages[1].Controls.Add(new CreatorColumns(original, appear));
    }
    private void BuildPet()
    {
        var guide = new CreatorCard(); guide.Content.Controls.Add(new AssetPreparationGuide(true));
        var uploads = new CreatorCard("上传四张动作图");
        uploads.Content.Controls.Add(Note("透明 PNG · 四张图保持同样大小和位置\n每张都包含完整的角色、键盘和鼠标。"));
        foreach (var (key, label) in new[] { ("idle", "抬起手"), ("left", "按键盘左边"), ("center", "按键盘中间"), ("right", "按键盘右边") })
        {
            uploads.Content.Controls.Add(Note(label));
            var box = new TextBox { ReadOnly = true }; petFiles[key] = box;
            uploads.Content.Controls.Add(FileRow(box, false));
        }
        uploads.Content.Controls.Add(keyboardOnly);
        var skip = CreatorUi.Button("暂不需要桌面互动，跳过 →");
        skip.Click += async (_, _) => { foreach (var box in petFiles.Values) box.Clear(); await NextAsync(); };
        guide.Content.Controls.Add(Note("只想用它提醒？可以直接跳过这一步。")); guide.Content.Controls.Add(skip);
        pages[2].Controls.Add(new CreatorColumns(guide, uploads));
        guide.SizeChanged += (_, _) => ResizePage();
    }
    private void BuildPreview()
    {
        var stage = new CreatorCard();
        var checks = new CreatorCard("看这三处就好");
        checks.Content.Controls.Add(Note("✓  手和鼠标一起动\n\n✓  身体和键盘不乱动\n\n✓  边缘没有残影、缺口"));
        var p = stage.Content;
        pages[3].Controls.Add(new CreatorColumns(stage, checks));
        p.Controls.Add(Note("预览的是处理后的实际素材。检查角色边缘、方向和互动效果，不满意可返回修改。"));
        var row = new FlowLayoutPanel { AutoSize = true };
        foreach (var edge in Enum.GetValues<ScreenEdge>())
        {
            var button = CreatorUi.Button(EdgeName(edge) + "提醒");
            button.Click += (_, _) => StartAnimation(edge); row.Controls.Add(button);
        }
        var pet = CreatorUi.Button("桌面互动");
        pet.Click += (_, _) =>
        {
            if (previewPet is null) { status.Text = "此角色仅用于提醒，未上传桌宠素材。"; return; }
            showPet = true; previewPose = DesktopPetRigPose.Rest; preview.Focus(); status.Text = "在预览里移动/点击鼠标，或按键盘；也可以用下面按钮试动作。";
        };
        row.Controls.Add(pet); p.Controls.Add(row); p.Controls.Add(preview);
        var keys = new FlowLayoutPanel { AutoSize = true };
        foreach (var (label, x) in new[] { ("按左", .1f), ("按中", .5f), ("按右", .9f) })
        {
            var button = CreatorUi.Button(label);
            button.MouseDown += (_, _) => { if (previewPet is not null) { showPet = true; previewPose = previewPose with { KeyboardContact = true, KeyboardPress = 1, KeyboardTarget = new PointF(x, .5f) }; RenderPreview(); } };
            button.MouseUp += (_, _) => previewPose = previewPose with { KeyboardContact = false, KeyboardPress = 0 };
            keys.Controls.Add(button);
        }
        p.Controls.Add(keys);
        preview.MouseMove += (_, e) => previewPose = previewPose with { MouseOffset = new PointF(e.X / (float)preview.Width * 2 - 1, e.Y / (float)preview.Height * 2 - 1) };
        preview.MouseDown += (_, _) => { preview.Focus(); previewPose = previewPose with { MousePress = 1 }; RenderPreview(); };
        preview.MouseUp += (_, _) => previewPose = previewPose with { MousePress = 0 };
        preview.MouseLeave += (_, _) => previewPose = previewPose with { MouseOffset = PointF.Empty, MousePress = 0 };
        mouseFallback.LinkClicked += (_, _) =>
        {
            ClearPreview(); prepared?.Dispose(); prepared = null;
            keyboardOnly.Visible = true; keyboardOnly.Checked = true;
            ShowStep(2); status.Text = "已选择仅键盘互动，点击“处理并预览”再次确认效果。";
        };
        mouseAdjust.Click += (_, _) => AdjustMouse();
        checks.Content.Controls.Add(CreatorUi.Text("鼠标动作不太对？", true));
        checks.Content.Controls.Add(mouseAdjust); checks.Content.Controls.Add(mouseFallback); checks.Content.Controls.Add(selectAfter);
        selectAfter.AutoSize = false; selectAfter.Size = new Size(240, 62);
        mouseFallback.MaximumSize = new Size(240, 0);
    }

    private CharacterCreationRequest Request() => new()
    {
        Name = nameBox.Text, VideoPath = video.Text, Background = BackgroundRemoval.Auto,
        SourceEdge = (ScreenEdge)sourceEdge.SelectedIndex, AllowedEdges = edges.Where(x => x.Value.Checked).Select(x => x.Key).ToArray(),
        Directions = directionFiles.Where(x => x.Value.Text.Length > 0).ToDictionary(x => x.Key, x => x.Value.Text),
        PetImages = petFiles.Where(x => x.Value.Text.Length > 0).ToDictionary(x => x.Key, x => x.Value.Text),
        PetBackground = BackgroundRemoval.Preserve, StrictPetPng = true, UseTemplateMouse = !keyboardOnly.Checked, AllowMouseCalibration = true,
        Mouse = null
    };
    private async Task NextAsync()
    {
        try
        {
            status.Text = "";
            if (step == 0)
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text)) throw new InvalidDataException("请填写角色名称。");
                CharacterMediaProcessor.CheckSource(video.Text, [".mp4", ".mov", ".webm"]); ShowStep(1);
            }
            else if (step == 1)
            {
                if (!edges.Any(x => x.Value.Checked)) throw new InvalidDataException("至少选一个出现方向。"); ShowStep(2);
            }
            else if (step == 2)
            {
                var request = Request();
                processing = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                next.Enabled = back.Enabled = host.Enabled = false;
                cancel.Text = "取消处理";
                prepared?.Dispose(); prepared = null;
                prepared = await new CharacterCreationService(new CustomCharacterStore()).PrepareAsync(request, new Progress<string>(text => status.Text = text), processing.Token);
                ShowStep(3);
                mouseFallback.Visible = prepared.Character.CustomManifest!.UsesTemplateMouse;
                mouseAdjust.Visible = prepared.Character.HasPetAssets;
                if (prepared.Character.HasPetAssets) previewPet = new CustomPetRenderer(Path.Combine(prepared.Character.CustomPackagePath!, "pet"), prepared.Character.CustomManifest!.Mouse, prepared.Character.CustomManifest.UsesTemplateMouse, prepared.Character.CustomManifest.MouseCalibrations);
                StartAnimation(request.AllowedEdges[0]); previewTimer.Start();
                if (request.UseTemplateMouse && prepared.Character.HasPetAssets && !prepared.Character.CustomManifest.UsesTemplateMouse)
                    status.Text = "这组图片需要校准鼠标。点击“手动修复鼠标模块”；未调整时只响应键盘。";
            }
            else
            {
                ClearPreview(); CreatedCharacter = prepared!.Commit(); AnimationCatalog.RefreshCustomCharacters(); DialogResult = DialogResult.OK; Close();
            }
        }
        catch (OperationCanceledException) { status.Text = "已取消，素材原文件没有改动。"; }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException)
        { status.Text = e.Message; if (e.Message.StartsWith("桌面构图", StringComparison.Ordinal)) keyboardOnly.Visible = true; }
        finally
        {
            processing?.Dispose(); processing = null;
            if (!IsDisposed) { next.Enabled = host.Enabled = true; back.Enabled = step > 0; cancel.Text = "取消"; }
        }
    }
    private void AdjustMouse()
    {
        if (prepared is null || !prepared.Character.HasPetAssets) return;
        previewTimer.Stop();
        try
        {
            using var dialog = new MouseCalibrationDialog(Path.Combine(prepared.Character.CustomPackagePath!, "pet"), prepared.Character.CustomManifest!.MouseCalibrations);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            prepared.ApplyMouseCalibration(dialog.Edits); ClearPreview();
            previewPet = new CustomPetRenderer(Path.Combine(prepared.Character.CustomPackagePath!, "pet"), null, false, prepared.Character.CustomManifest!.MouseCalibrations);
            previewPose = DesktopPetRigPose.Rest; showPet = true; mouseFallback.Visible = true;
            status.Text = "已应用四个姿势的校准。请移动鼠标、点击并试按左中右，满意后创建。";
            RenderPreview();
        }
        catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        { status.Text = e.Message; }
        finally { previewTimer.Start(); }
    }
    private void ShowStep(int value)
    {
        host.SuspendLayout();
        step = value; host.Controls.Clear(); host.Controls.Add(pages[step]); host.AutoScrollPosition = Point.Empty;
        heading.Text = new[] { "先做一个提醒视频", "想让它从哪里探出来？", "让它陪你敲键盘、动鼠标", "试一试，你的新伙伴" }[step];
        status.Text = new[] { "给角色取个名字，再上传它探头打招呼的视频。", "先告诉我们视频里的方向，再选你喜欢的提醒位置。", "这一步可以跳过。需要桌面互动时，再准备四张图片。", "移动鼠标、点一下、敲几个键，看看它动得对不对。" }[step];
        next.Text = step == 3 ? "创建角色" : step == 2 ? "处理并预览" : "下一步 →";
        back.Enabled = step > 0;
        for (int i = 0; i < navigation.Length; i++)
        { navigation[i].BackColor = i == step ? UiTheme.AccentSoftColor : Color.Transparent; navigation[i].ForeColor = i == step ? UiTheme.AccentColor : UiTheme.MutedTextColor; }
        ResizePage(); host.ResumeLayout(true);
    }
    private void StartAnimation(ScreenEdge edge)
    {
        if (prepared is null) return;
        var character = prepared.Character;
        if (!character.AllowedEdges.Contains(edge)) { status.Text = "这个方向未启用，可返回出现方式修改。"; return; }
        previewSequence?.Dispose(); previewSequence = null;
        var path = character.SequenceName; var duration = character.Duration; previewSource = character.SourceEdge;
        if (character.CustomManifest!.Directions.TryGetValue(edge, out var info)) { path = Path.Combine(character.CustomPackagePath!, "direction-" + edge); duration = TimeSpan.FromSeconds(info.DurationSeconds); previewSource = edge; }
        previewSequence = AnimationSequence.Load(path, duration, true);
        showPet = false; previewEdge = edge; lastFrame = -1; previewClock.Restart(); RenderPreview();
    }
    private void RenderPreview()
    {
        if (step != 3) return;
        Bitmap? image = null;
        if (showPet && previewPet is not null) image = previewPet.Render(previewPose);
        else if (previewSequence is not null)
        {
            var index = previewSequence.Timeline.GetFrameIndex(previewClock.Elapsed);
            if (lastFrame == index) return;
            lastFrame = index;
            image = new Bitmap(previewSequence.Frames[index]);
            image.RotateFlip(ReminderFrameTransformResolver.Resolve(previewSource, previewEdge) switch
            {
                ReminderFrameTransform.FlipHorizontal => RotateFlipType.RotateNoneFlipX,
                ReminderFrameTransform.FlipVertical => RotateFlipType.RotateNoneFlipY,
                ReminderFrameTransform.RotateClockwise => RotateFlipType.Rotate90FlipNone,
                ReminderFrameTransform.RotateCounterClockwise => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            });
        }
        if (image is null) return;
        var previous = preview.Image; preview.Image = image; previous?.Dispose();
    }
    private void ClearPreview()
    {
        previewTimer.Stop(); previewClock.Stop();
        preview.Image?.Dispose(); preview.Image = null;
        previewSequence?.Dispose(); previewSequence = null; previewPet?.Dispose(); previewPet = null;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lifetime.Cancel(); lifetime.Dispose(); ClearPreview(); previewTimer.Dispose(); prepared?.Dispose();
            foreach (var page in pages) page.Dispose(); heading.Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
