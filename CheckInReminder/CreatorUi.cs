namespace CheckInReminder;

/// <summary>Shared layout primitives for the character creator only.</summary>
internal static class CreatorUi
{
    internal static BrandButton Button(string text, bool primary = false) => new(primary ? BrandButtonKind.Primary : BrandButtonKind.Secondary)
    { Text = text, AutoSize = true, MinimumSize = new Size(88, 36), Margin = new Padding(0, 4, 8, 4) };

    internal static Label Text(string text, bool title = false) => new()
    {
        Text = text, AutoSize = true, BackColor = Color.Transparent, ForeColor = title ? UiTheme.TextColor : UiTheme.MutedTextColor,
        Font = new Font("Microsoft YaHei UI", title ? 13 : 10, title ? FontStyle.Bold : FontStyle.Regular),
        MaximumSize = new Size(520, 0), Margin = new Padding(0, 6, 0, 12)
    };

    internal static FlowLayoutPanel Stack() => new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty, BackColor = Color.Transparent };

    internal static void Fit(Control control, int width)
    {
        width = Math.Max(80, width);
        if (control is Label label) label.MaximumSize = new Size(width, 0);
        else if (control is TextBox box) box.Width = width;
        else if (control is AssetPreparationGuide guide) guide.Width = width;
        else if (control is CreatorColumns columns) { columns.Width = width; columns.Arrange(); }
        else if (control is CreatorCard card) card.Width = width;
        else if (control is PictureBox picture) picture.Width = width;
        else if (control is CreatorSurface fileSurface) fileSurface.Width = width;
        else if (control is RoundedPanel panel) panel.Width = width;
        else if (control is FlowLayoutPanel flow)
        {
            flow.MinimumSize = new Size(width, 0); flow.MaximumSize = new Size(width, 0); flow.Width = width;
            foreach (Control child in flow.Controls) Fit(child, (Equals(flow.Tag, "reference-pair") ? (width - flow.Padding.Horizontal) / 2 : width - flow.Padding.Horizontal) - child.Margin.Horizontal);
        }
    }
}

internal sealed class CreatorCard : Panel
{
    internal FlowLayoutPanel Content { get; } = CreatorUi.Stack();
    private readonly RoundedPanel surface = new() { ShowShadow = false, Padding = Padding.Empty };
    private bool arranging;
    internal CreatorCard(string? title = null)
    {
        Width = 420; Margin = new Padding(0, 0, 16, 16); BackColor = Color.Transparent;
        Controls.Add(surface); surface.Controls.Add(Content);
        Content.SizeChanged += (_, _) => Arrange();
        Content.ControlAdded += (_, _) => Arrange();
        SizeChanged += (_, _) => Arrange();
        if (title is not null) Content.Controls.Add(CreatorUi.Text(title, true));
        Arrange();
    }
    private void Arrange()
    {
        if (arranging) return;
        arranging = true;
        try
        {
            var inset = (int)(16 * DeviceDpi / 96f);
            Content.Location = new Point(inset, inset);
            CreatorUi.Fit(Content, ClientSize.Width - inset * 2);
            Content.PerformLayout();
            Height = Math.Max(80, Content.PreferredSize.Height + inset * 2);
            surface.Bounds = ClientRectangle;
        }
        finally { arranging = false; }
    }
}

internal sealed class CreatorColumns : Panel
{
    private readonly Control left;
    private readonly Control right;
    private bool arranging;
    internal CreatorColumns(Control left, Control right)
    {
        this.left = left; this.right = right;
        Margin = Padding.Empty; BackColor = Color.Transparent;
        Controls.Add(left); Controls.Add(right);
        SizeChanged += (_, _) => Arrange();
        left.SizeChanged += (_, _) => Arrange(); right.SizeChanged += (_, _) => Arrange();
    }
    internal void Arrange()
    {
        if (arranging) return;
        arranging = true;
        try
        {
            var available = Width;
            var wide = available >= 700;
            var gap = (int)(16 * DeviceDpi / 96f);
            var first = wide ? (int)((available - gap) * .57) : available;
            CreatorUi.Fit(left, first);
            CreatorUi.Fit(right, wide ? available - gap - first : available);
            left.Location = Point.Empty;
            right.Location = wide ? new Point(first + gap, 0) : new Point(0, left.Height + gap);
            Height = wide ? Math.Max(left.Height, right.Height) : left.Height + gap + right.Height;
        }
        finally { arranging = false; }
    }
}

internal sealed class CreatorNameInput : CreatorSurface
{
    private readonly TextBox input;
    internal CreatorNameInput(TextBox input)
    {
        this.input = input; ShowShadow = false; Height = 48; Width = 300; Margin = new Padding(0, 0, 0, 12);
        input.BorderStyle = BorderStyle.None; input.BackColor = SurfaceColor;
        Controls.Add(input); SizeChanged += (_, _) => ArrangeInput(); ArrangeInput();
    }
    private void ArrangeInput()
    {
        int gap = (int)(12 * DeviceDpi / 96f);
        input.SetBounds(gap, Math.Max(0, (Height - input.PreferredHeight) / 2), Math.Max(40, Width - gap * 2), input.PreferredHeight);
    }
}

internal sealed class CreatorFileCard : CreatorSurface
{
    private readonly TextBox data;
    private readonly Label filename;
    private readonly Label detail;
    private readonly FlowLayoutPanel actions;
    private readonly PictureBox? thumbnail;
    internal CreatorFileCard(TextBox data, bool movie, bool optional, Action browseAction, Action? changed)
    {
        this.data = data; ShowShadow = false; Width = 340; Height = movie ? 132 : 150;
        Margin = new Padding(0, 0, 0, 12); SurfaceColor = UiTheme.WarmBackgroundColor;
        data.Visible = false; data.TabStop = false; Controls.Add(data);
        filename = new Label { AutoEllipsis = true, ForeColor = UiTheme.TextColor, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
        detail = new Label { AutoEllipsis = true, ForeColor = UiTheme.MutedTextColor, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
        actions = new FlowLayoutPanel { AutoSize = false, WrapContents = false, BackColor = Color.Transparent };
        var browse = CreatorUi.Button(movie ? "选择视频" : "选择图片"); browse.Click += (_, _) => browseAction(); actions.Controls.Add(browse);
        if (optional) { var clear = CreatorUi.Button("清除"); clear.Click += (_, _) => { data.Clear(); changed?.Invoke(); }; actions.Controls.Add(clear); }
        if (!movie) { thumbnail = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = UiTheme.SurfaceColor, AccessibleName = "上传图片缩略图" }; Controls.Add(thumbnail); }
        Controls.Add(filename); Controls.Add(detail); Controls.Add(actions);
        data.TextChanged += (_, _) => UpdateFile(movie);
        SizeChanged += (_, _) => ArrangeFile(); UpdateFile(movie); ArrangeFile();
    }
    private void UpdateFile(bool movie)
    {
        filename.Text = data.Text.Length == 0 ? (movie ? "尚未选择提醒视频" : "尚未选择动作图片") : Path.GetFileName(data.Text);
        detail.Text = data.Text.Length == 0 ? (movie ? "MP4 · MOV · WebM" : "透明 PNG 图片") : data.Text;
        if (thumbnail is null) return;
        var old = thumbnail.Image; thumbnail.Image = null; old?.Dispose();
        if (File.Exists(data.Text)) try { using var source = Image.FromFile(data.Text); thumbnail.Image = new Bitmap(source); }
        catch (Exception e) when (e is IOException or ArgumentException or OutOfMemoryException) { detail.Text = "无法显示缩略图，请检查 PNG 文件。"; }
    }
    private void ArrangeFile()
    {
        int S(int n) => (int)Math.Round(n * DeviceDpi / 96f);
        int gap = S(12), start = gap;
        Height = S(thumbnail is null ? 132 : 150);
        if (thumbnail is not null) { thumbnail.SetBounds(gap, gap, S(68), S(68)); start += S(80); }
        filename.SetBounds(start, gap, Math.Max(40, Width - start - gap), S(28));
        detail.SetBounds(start, gap + S(30), Math.Max(40, Width - start - gap), S(28));
        actions.SetBounds(gap, Height - S(56), Math.Max(40, Width - gap * 2), S(48));
    }
    protected override void Dispose(bool disposing) { if (disposing) thumbnail?.Image?.Dispose(); base.Dispose(disposing); }
}

internal class CreatorSurface : Panel
{
    private readonly RoundedPanel surface = new() { Dock = DockStyle.Fill, ShowShadow = false };
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool ShowShadow { get => surface.ShowShadow; set => surface.ShowShadow = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Color SurfaceColor { get => surface.SurfaceColor; set => surface.SurfaceColor = value; }
    internal CreatorSurface()
    {
        DoubleBuffered = true; BackColor = Color.Transparent; Controls.Add(surface);
        ControlAdded += (_, e) => { if (e.Control is { } child && !ReferenceEquals(child, surface)) surface.Controls.Add(child); };
    }
}
