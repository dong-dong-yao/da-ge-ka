using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Text.Json;

namespace CheckInReminder;

internal sealed class MouseCalibrationEdit(MouseCalibration spec, Bitmap mask) : IDisposable
{
    public MouseCalibration Spec { get; set; } = spec;
    public Bitmap Mask { get; set; } = mask;
    public bool Confirmed { get; set; }
    public void Dispose() => Mask.Dispose();
}

internal sealed class MouseCalibrationDialog : BrandedForm
{
    private readonly Dictionary<string, Bitmap> sources = [];
    internal Dictionary<string, MouseCalibrationEdit> Edits { get; } = [];
    private readonly CalibrationCanvas canvas = new();
    private readonly PictureBox preview = new() { Size = new Size(330, 247), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(222, 225, 229) };
    private readonly Label message = new() { AutoSize = true, MaximumSize = new Size(330, 0) };
    private readonly Label confirmation = new() { AutoSize = true, MaximumSize = new Size(900, 0) };
    private readonly CheckBox keepGray = new() { Text = "手臂是灰色的，保留灰色细节", AutoSize = true };
    private bool binding;
    private readonly ComboBox poses = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly List<BrandButton> poseButtons = [];
    private readonly BrandButton next = new() { Text = "这张好了，下一张 →", Size = new Size(230, 44), Margin = new Padding(0, 0, 8, 8) };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 70 };
    private readonly Stack<(Bitmap Mask, MouseCalibration Spec)> undo = new();
    private CalibratedMouseLayers? layers;
    private int tick;
    private bool dirty = true;
    private string current = "idle";
    private readonly string[] keys = ["idle", "left", "center", "right"];
    public MouseCalibrationDialog(string folder, IReadOnlyDictionary<string, MouseCalibration>? existing = null)
    {
        Text = "手动修复鼠标模块"; Font = new Font("Microsoft YaHei UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        foreach (var key in keys)
        {
            var original = Path.Combine(folder, "original-" + key + ".png");
            sources[key] = CharacterMediaProcessor.ReadBitmap(File.Exists(original) ? original : Path.Combine(folder, key + ".png"));
            if (existing is not null)
                Edits[key] = new(Clone(existing[key]), CharacterMediaProcessor.ReadBitmap(Path.Combine(folder, "mask-" + key + ".png")));
            else Edits[key] = Suggest(sources[key]);
        }
        BackColor = UiTheme.WarmBackgroundColor; ForeColor = UiTheme.TextColor;
        ClientSize = new Size(1180, 820); MinimumSize = new Size(760, 570);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var header = new GradientHeaderPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(22, 14, 22, 14) };
        var heading = new Label { AutoSize = false, Text = "调整鼠标动作", Font = new Font(Font.FontFamily, 24, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
        var subtitle = new Label { AutoSize = false, Text = "点一下选中，细节放大改。右边马上看到效果。", ForeColor = Color.White, BackColor = Color.Transparent };
        header.Controls.Add(heading); header.Controls.Add(subtitle);
        void SizeHeader()
        {
            var available = Math.Max(100, header.ClientSize.Width - header.Padding.Horizontal);
            var titleHeight = TextRenderer.MeasureText(heading.Text, heading.Font, new Size(available, int.MaxValue), TextFormatFlags.WordBreak).Height;
            var subtitleHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font, new Size(available, int.MaxValue), TextFormatFlags.WordBreak).Height;
            var gap = Math.Max(6, DeviceDpi / 16);
            heading.SetBounds(header.Padding.Left, header.Padding.Top, available, titleHeight + 2);
            subtitle.SetBounds(header.Padding.Left, heading.Bottom + gap, available, subtitleHeight + 2);
            header.Height = subtitle.Bottom + header.Padding.Bottom;
        }
        header.SizeChanged += (_, _) => SizeHeader();
        layout.Controls.Add(header);
        poses.Items.AddRange(["抬起手", "按左边", "按中间", "按右边"]);
        var tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 8) };
        for (var i = 0; i < keys.Length; i++)
        {
            var index = i; var tab = Button(poses.Items[i]!.ToString()!, 126);
            tab.Click += (_, _) => { if (canvas.HasPendingPolygon) { message.Text = "请先完成圈选，或重新选择工具放弃这次圈选。"; return; } poses.SelectedIndex = index; };
            poseButtons.Add(tab); tabs.Controls.Add(tab);
        }
        layout.Controls.Add(tabs);
        confirmation.ForeColor = UiTheme.MutedTextColor; confirmation.Margin = new Padding(0, 0, 0, 10); layout.Controls.Add(confirmation);
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 520, ColumnCount = 2, Margin = Padding.Empty };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var editCard = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16), Margin = new Padding(0, 0, 10, 0) };
        var tools = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 3, Padding = new Padding(0, 0, 0, 8), Margin = Padding.Empty };
        for (var column = 0; column < 4; column++) tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (var row = 0; row < 3; row++) tools.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var modeButtons = new Dictionary<int, BrandButton>();
        var finishPolygon = Button("完成圈垫 ✓", 134); finishPolygon.Visible = false;
        finishPolygon.Click += (_, _) => { if (!canvas.HasPendingPolygon) { message.Text = "先沿鼠标垫内沿点至少三个点，再完成圈垫。"; return; } canvas.CompletePolygon(); if (canvas.HasPendingPolygon) message.Text = "至少点击三个点才能完成圈垫。"; };
        void AddTool(Control control, int column, int row, int span = 1)
        {
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 0, 8, 8);
            tools.Controls.Add(control, column, row); tools.SetColumnSpan(control, span);
        }
        void SetMode(int mode, string hint)
        {
            canvas.Mode = mode; canvas.ClearPolygon(); message.Text = hint;
            finishPolygon.Visible = mode == 4;
            foreach (var pair in modeButtons) pair.Value.ForeColor = pair.Key == mode ? UiTheme.AccentColor : UiTheme.TextColor;
        }
        void Mode(string title, int mode, string hint, int column, int row)
        {
            var button = Button(title, 134); modeButtons[mode] = button;
            button.Click += (_, _) => SetMode(mode, hint); AddTool(button, column, row);
        }
        Mode("点选手和鼠标", 6, "点鼠标，再点手臂，把它们选进来。蓝色部分会动。", 0, 0);
        Mode("补涂选区", 1, "按住涂抹会自动出现放大镜。按住 Shift 可用细笔修边。", 1, 0);
        Mode("擦除选区", 2, "涂掉误选的键盘或鼠标垫边框。按住 Shift 可用细笔修边。", 2, 0);
        var undoButton = Button("撤销", 134); undoButton.Click += (_, _) => Undo(); AddTool(undoButton, 3, 0);
        Mode("点肩膀连接处", 3, "点击手臂靠近身体的位置。这个位置及其身体一侧固定不动。", 0, 1);
        Mode("点鼠标中心", 5, "点一下图里鼠标的中心，调整移动方向。", 1, 1);
        Mode("点选鼠标垫", 7, "点一下鼠标垫的灰色区域，自动识别垫面。金色线框是当前范围。", 2, 1);
        Mode("手动圈垫（备用）", 4, "沿纯灰色鼠标垫内沿逐点点击，避开黑色外框，再点“完成圈垫”。", 3, 1);
        keepGray.Text = "保留手臂灰色细节"; keepGray.AutoSize = false; keepGray.Height = 44;
        keepGray.TextAlign = ContentAlignment.MiddleLeft; AddTool(keepGray, 0, 2, 2);
        var reset = Button("重置本姿势", 134);
        reset.Click += (_, _) => { Snapshot(); Edits[current].Dispose(); Edits[current] = Suggest(sources[current]); Bind(); Changed(); };
        AddTool(reset, 2, 2); AddTool(finishPolygon, 3, 2);
        canvas.Dock = DockStyle.Fill; canvas.Mode = 6; modeButtons[6].ForeColor = UiTheme.AccentColor;
        var hint = new Label { Dock = DockStyle.Bottom, Height = 46, Text = "点鼠标，再点手臂，把它们选进来。按住涂抹时，会自动出现放大镜。", ForeColor = UiTheme.MutedTextColor, Padding = new Padding(2, 10, 2, 0) };
        editCard.Controls.Add(canvas); editCard.Controls.Add(hint); editCard.Controls.Add(tools); main.Controls.Add(editCard, 0, 0);
        var side = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(20), Margin = Padding.Empty };
        var sideLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        sideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        sideLayout.Controls.Add(new Label { Text = "看看改完的效果", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 16, FontStyle.Bold) });
        preview.Dock = DockStyle.Fill; preview.BackColor = Color.FromArgb(235, 237, 232); sideLayout.Controls.Add(preview);
        sideLayout.Controls.Add(new Label { Text = "● 正在试动", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = UiTheme.AccentColor });
        sideLayout.Controls.Add(new Label { Text = "还有一圈黑色残影？", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        var edges = Button("补选黑色边缘", 220); edges.Dock = DockStyle.Fill;
        edges.Click += (_, _) =>
        {
            Snapshot();
            try
            {
                var added = MouseSelectionTools.CompleteDarkEdges(sources[current], Edits[current].Mask, Edits[current].Spec);
                if (added > 0) { Changed(); canvas.Invalidate(); }
                message.Text = added > 0 ? "已补上附近轮廓。请检查右侧效果，也可以撤销。" : "附近没有可补选的黑色边缘，可用涂抹工具细调。";
            }
            catch (InvalidDataException error) { ShowSelectionError(error.Message); }
        };
        sideLayout.Controls.Add(edges); message.AutoSize = false; message.Dock = DockStyle.Fill; message.MaximumSize = Size.Empty; message.ForeColor = UiTheme.MutedTextColor; sideLayout.Controls.Add(message);
        side.Controls.Add(sideLayout); main.Controls.Add(side, 1, 0); layout.Controls.Add(main);
        keepGray.CheckedChanged += (_, _) => { if (binding) return; Snapshot(); Edits[current].Spec.RemoveConnectedPadColor = !keepGray.Checked; Changed(); };
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 76, ColumnCount = 4, RowCount = 1, Padding = new Padding(16, 12, 16, 12) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244));
        var cancel = Button("取消调整", 110); cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel; footer.Controls.Add(cancel);
        var copy = Button("复制选区到其他姿势", 230);
        copy.Click += (_, _) =>
        {
            if (canvas.HasPendingPolygon) { message.Text = "请先完成当前圈选。"; return; }
            if (MessageBox.Show(this, "将覆盖其他三张的选区。复制后仍需分别检查并确认，是否继续？", "复制到其他姿势", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (var key in keys.Where(k => k != current))
            {
                var spec = Clone(Edits[current].Spec);
                if (!MouseSelectionTools.TrySelectPad(sources[key], spec, null)) spec.Pad = Edits[key].Spec.Pad.ToArray();
                Edits[key].Dispose(); Edits[key] = new(spec, new Bitmap(Edits[current].Mask));
            }
            UpdateConfirmation(); message.Text = "已复制。请分别检查其他三张，避免按键时跳动。";
        };
        footer.Controls.Add(copy);
        next.Click += (_, _) =>
        {
            if (layers is null || dirty || canvas.HasPendingPolygon) { message.Text = "请先完成选区并等待预览。"; return; }
            Edits[current].Confirmed = true; UpdateConfirmation();
            var unconfirmed = Array.FindIndex(keys, k => !Edits[k].Confirmed);
            if (unconfirmed >= 0) poses.SelectedIndex = unconfirmed;
            else DialogResult = DialogResult.OK;
        };
        next.Margin = new Padding(0, 0, 0, 8); next.Dock = DockStyle.Fill;
        footer.Controls.Add(next, 3, 0);
        scroll.Controls.Add(layout); Controls.Add(scroll); Controls.Add(footer);
        main.RowCount = 2;
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        bool? compactLayout = null;
        bool fittingCards = false;
        void FitCards()
        {
            if (fittingCards) return;
            fittingCards = true;
            try
            {
            var scale = DeviceDpi / 96f;
            confirmation.MaximumSize = new Size(Math.Max(100, layout.ClientSize.Width - 20), 0);
            var compact = layout.Width < 950;
            compactLayout = compact;
            main.SuspendLayout();
            main.ColumnStyles[0].Width = compact ? 100 : 65;
            main.ColumnStyles[1].Width = compact ? 0 : 35;
            main.SetCellPosition(side, new TableLayoutPanelCellPosition(compact ? 0 : 1, compact ? 1 : 0));
            main.RowStyles[0].SizeType = compact ? SizeType.Absolute : SizeType.Percent;
            main.RowStyles[0].Height = compact ? 590 : 100;
            main.RowStyles[1].Height = compact ? 420 : 0;
            main.Height = compact ? 1010 : Math.Max(440, scroll.ClientSize.Height - main.Top - 32);
            editCard.Margin = compact ? new Padding(0, 0, 0, 10) : new Padding(0, 0, 10, 0);
            main.ResumeLayout();
            }
            finally { fittingCards = false; }
        }
        layout.SizeChanged += (_, _) => FitCards();
        scroll.SizeChanged += (_, _) => FitCards();
        Shown += (_, _) => { SizeHeader(); FitCards(); };
        DpiChanged += (_, _) => { compactLayout = null; SizeHeader(); FitCards(); };
        SizeHeader(); FitCards();
        canvas.SelectionMissed = () => message.Text = canvas.Mode == 7 ? "这里没有识别到鼠标垫，请点垫面灰色区域，或使用手动圈垫。" : "这里没有选到手或鼠标，请换个位置点一下";
        canvas.SelectionError = ShowSelectionError;
        canvas.BeforeChange = Snapshot; canvas.Changed = Changed;
        poses.SelectedIndexChanged += (_, _) => { ClearUndo(); current = keys[poses.SelectedIndex]; Bind(); dirty = true; };
        poses.SelectedIndex = 0; timer.Tick += (_, _) => Animate(); timer.Start();
    }
    private void ShowSelectionError(string detail) => message.Text = detail + " 可用上方工具重新设置连接处、鼠标中心或鼠标垫，也可以撤销刚才的调整。";
    private static BrandButton Button(string text, int width) => new(BrandButtonKind.Secondary) { Text = text, Size = new Size(width, 44), Margin = new Padding(0, 0, 8, 8) };
    private void Bind() { binding = true; keepGray.Checked = !Edits[current].Spec.RemoveConnectedPadColor; binding = false; canvas.Bind(sources[current], Edits[current]); UpdateConfirmation(); }
    private void Changed() { Edits[current].Confirmed = false; dirty = true; UpdateConfirmation(); }
    private void Snapshot() { if (undo.Count >= 20) ClearUndo(); undo.Push((new Bitmap(Edits[current].Mask), Clone(Edits[current].Spec))); }
    private void Undo()
    {
        if (!undo.TryPop(out var previous)) return;
        Edits[current].Dispose(); Edits[current] = new(previous.Spec, previous.Mask); Bind(); Changed();
    }
    private void ClearUndo() { while (undo.TryPop(out var e)) e.Mask.Dispose(); }
    private void UpdateConfirmation()
    {
        confirmation.Text = "检查进度：" + string.Join("   ", keys.Select((k, i) => $"{poses.Items[i]} {(Edits[k].Confirmed ? "✓" : "待确认")}"));
        for (var i = 0; i < poseButtons.Count; i++) { poseButtons[i].Text = poses.Items[i] + (Edits[keys[i]].Confirmed ? " ✓" : ""); poseButtons[i].ForeColor = keys[i] == current ? UiTheme.AccentColor : UiTheme.TextColor; }
        next.Text = keys.Count(k => !Edits[k].Confirmed && k != current) == 0 ? "这张好了，应用调整" : "这张好了，下一张 →";
    }
    private void Animate()
    {
        if (canvas.IsDrawing) return;
        if (dirty)
        {
            layers?.Dispose(); layers = null; dirty = false;
            try { layers = CalibratedMouseLayers.Create(sources[current], Edits[current].Mask, Edits[current].Spec); message.Text = "检查：没有旧手残影、破洞；连接身体的地方稳定；键盘和鼠标垫边框不动。"; }
            catch (InvalidDataException e) { message.Text = e.Message; }
        }
        var phase = ++tick * .065f;
        var image = layers?.Render(DesktopPetRigPose.Rest with { MouseOffset = new PointF(MathF.Sin(phase), MathF.Cos(phase)), MousePress = tick % 60 > 49 ? 1 : 0 }) ?? new Bitmap(sources[current]);
        var old = preview.Image; preview.Image = image; old?.Dispose();
    }
    private static MouseCalibration Clone(MouseCalibration s) => JsonSerializer.Deserialize<MouseCalibration>(JsonSerializer.Serialize(s))!;
    internal static MouseCalibrationEdit Suggest(Bitmap source)
    {
        var mask = new Bitmap(600, 448);
        using (var g = Graphics.FromImage(mask)) g.FillPolygon(Brushes.White, new Point[] { new(185, 211), new(270, 269), new(220, 301), new(191, 355), new(130, 366), new(90, 345), new(97, 300), new(145, 250) });
        var spec = new MouseCalibration { Rig = new() { ShoulderX = .34f, ShoulderY = .56f, MouseX = .245f, MouseY = .735f } };
        MouseSelectionTools.TrySelectPad(source, spec, null);
        return new(spec, mask);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); layers?.Dispose(); preview.Image?.Dispose(); preview.Image = null; ClearUndo(); foreach (var s in sources.Values) s.Dispose(); foreach (var e in Edits.Values) e.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class CalibrationCanvas : Control
    {
        private Bitmap? source;
        private MouseCalibrationEdit? edit;
        private readonly List<PointF> polygon = [];
        private bool painting;
        private PointF previous;
        private Point pointer;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal int Mode { get; set; }
        internal bool IsDrawing => painting;
        internal bool HasPendingPolygon => polygon.Count > 0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal Action? BeforeChange { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal Action? Changed { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal Action? SelectionMissed { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal Action<string>? SelectionError { get; set; }
        internal CalibrationCanvas() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Color.FromArgb(235, 237, 232); Cursor = Cursors.Cross; }
        internal void Bind(Bitmap image, MouseCalibrationEdit state) { source = image; edit = state; ClearPolygon(); Invalidate(); }
        private RectangleF Canvas { get { var scale = Math.Min(Width / 600f, Height / 448f); return new((Width - 600 * scale) / 2, (Height - 448 * scale) / 2, 600 * scale, 448 * scale); } }
        internal void ClearPolygon() { polygon.Clear(); Invalidate(); }
        internal void CompletePolygon()
        {
            if (edit is null || polygon.Count < 3 || Mode != 4) return;
            BeforeChange?.Invoke();
            edit.Spec.Pad = polygon.Select(p => new CalibrationPoint(p.X, p.Y)).ToArray();
            ClearPolygon(); Changed?.Invoke();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if (source is null || edit is null) return;
            var c = Canvas; var g = e.Graphics; g.TranslateTransform(c.X, c.Y); g.ScaleTransform(c.Width / 600, c.Height / 448);
            g.DrawImageUnscaled(source, 0, 0);
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] { [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, .30f, 0], [.1f, .5f, 1, 0, 1] }));
            g.DrawImage(edit.Mask, new Rectangle(0, 0, 600, 448), 0, 0, 600, 448, GraphicsUnit.Pixel, attributes);
            using var padPen = new Pen(Color.Goldenrod, 2); if (edit.Spec.Pad.Length >= 3) g.DrawPolygon(padPen, edit.Spec.Pad.Select(p => new PointF(p.X, p.Y)).ToArray());
            using var pen = new Pen(Color.DeepSkyBlue, 2); if (polygon.Count > 1) g.DrawLines(pen, polygon.ToArray());
            foreach (var p in polygon) g.FillEllipse(Brushes.DeepSkyBlue, p.X - 3, p.Y - 3, 6, 6);
            var rig = edit.Spec.Rig; g.FillEllipse(Brushes.OrangeRed, rig.ShoulderX * 600 - 4, rig.ShoulderY * 448 - 4, 8, 8);
            g.DrawEllipse(Pens.RoyalBlue, rig.MouseX * 600 - 5, rig.MouseY * 448 - 5, 10, 10);
            g.ResetTransform();
            if (painting) DrawMagnifier(g);
        }
        private void DrawMagnifier(Graphics g)
        {
            if (source is null || edit is null) return;
            var dpi = DeviceDpi / 96f;
            var size = Math.Min((int)(170 * dpi), Math.Min(Width - 12, Height - 38));
            if (size < 60) return;
            var gap = (int)(24 * dpi); var title = (int)(28 * dpi);
            var x = pointer.X + gap;
            if (x + size + 8 > Width) x = pointer.X - gap - size;
            x = Math.Clamp(x, 4, Math.Max(4, Width - size - 4));
            var y = Math.Clamp(pointer.Y - size / 2, title + 4, Math.Max(title + 4, Height - size - 4));
            // If horizontal room is insufficient, keep the lens above or below the actual brush.
            if (pointer.X >= x - 8 && pointer.X <= x + size + 8)
                y = pointer.Y >= size + title + gap ? pointer.Y - size - gap : Math.Min(Height - size - 4, pointer.Y + gap + title);
            var outer = new Rectangle(x - 4, y - title, size + 8, size + title + 4);
            using var path = RoundedPanel.CreateRoundedPath(outer, (int)(12 * dpi));
            using var background = new SolidBrush(UiTheme.SurfaceColor); g.FillPath(background, path);
            using var border = new Pen(UiTheme.BorderStrongColor); g.DrawPath(border, path);
            TextRenderer.DrawText(g, "涂抹时自动放大  3×", Font, new Rectangle(x, y - title, size, title), UiTheme.MutedTextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            var state = g.Save(); g.SetClip(new Rectangle(x, y, size, size));
            g.FillRectangle(Brushes.WhiteSmoke, x, y, size, size);
            var zoom = Canvas.Width / 600f * 3; var p = ImagePoint(pointer);
            g.TranslateTransform(x + size / 2f, y + size / 2f); g.ScaleTransform(zoom, zoom); g.TranslateTransform(-p.X, -p.Y);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(source, 0, 0);
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] { [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, 0, 0], [0, 0, 0, .30f, 0], [.1f, .5f, 1, 0, 1] }));
            g.DrawImage(edit.Mask, new Rectangle(0, 0, 600, 448), 0, 0, 600, 448, GraphicsUnit.Pixel, attributes);
            var brushSize = (ModifierKeys & Keys.Shift) != 0 ? 3f : 10f;
            using var cursorPen = new Pen(Mode == 2 ? Color.Firebrick : UiTheme.AccentColor, 1.5f / zoom);
            g.DrawEllipse(cursorPen, p.X - brushSize / 2, p.Y - brushSize / 2, brushSize, brushSize);
            g.DrawLine(cursorPen, p.X - 3 / zoom, p.Y, p.X + 3 / zoom, p.Y); g.DrawLine(cursorPen, p.X, p.Y - 3 / zoom, p.X, p.Y + 3 / zoom);
            g.Restore(state);
        }
        private PointF ImagePoint(Point p) { var c = Canvas; return new(Math.Clamp((p.X - c.X) * 600 / c.Width, 0, 599), Math.Clamp((p.Y - c.Y) * 448 / c.Height, 0, 447)); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (edit is null || e.Button != MouseButtons.Left || !Canvas.Contains(e.Location)) return;
            var p = ImagePoint(e.Location);
            if (Mode == 4) { if (polygon.Count < 128) polygon.Add(p); Invalidate(); return; }
            BeforeChange?.Invoke();
            if (Mode is 6 or 7)
            {
                try
                {
                    if (source is not null && (Mode == 7 ? MouseSelectionTools.TrySelectPad(source, edit.Spec, Point.Round(p)) : MouseSelectionTools.SelectConnected(source, edit.Mask, edit.Spec, Point.Round(p)))) { Changed?.Invoke(); Invalidate(); }
                    else SelectionMissed?.Invoke();
                }
                catch (InvalidDataException error) { SelectionError?.Invoke(error.Message); }
                return;
            }
            if (Mode == 3) { edit.Spec.Rig.ShoulderX = p.X / 600; edit.Spec.Rig.ShoulderY = p.Y / 448; }
            else if (Mode == 5) { edit.Spec.Rig.MouseX = p.X / 600; edit.Spec.Rig.MouseY = p.Y / 448; }
            else { painting = true; pointer = e.Location; Capture = true; previous = p; PaintMask(p); }
            Changed?.Invoke(); Invalidate();
        }
        private void PaintMask(PointF p)
        {
            using var g = Graphics.FromImage(edit!.Mask); g.CompositingMode = CompositingMode.SourceCopy;
            var color = Mode == 2 ? Color.Transparent : Color.White;
            var size = (ModifierKeys & Keys.Shift) != 0 ? 3 : 10;
            using var pen = new Pen(color, size) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, previous, p); using var brush = new SolidBrush(color); g.FillEllipse(brush, p.X - size / 2f, p.Y - size / 2f, size, size); previous = p;
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (!painting) return; pointer = e.Location; PaintMask(ImagePoint(e.Location)); Changed?.Invoke(); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); painting = false; Capture = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { painting = false; Invalidate(); } }
    }
}
