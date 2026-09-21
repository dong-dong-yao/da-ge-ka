using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckInReminder;

/// <summary>Material-first instructions beside the upload step, not a separate manual.</summary>
internal sealed class AssetPreparationGuide : UserControl
{
    private readonly List<Image> ownedImages = [];
    private Panel tabs = null!;
    private FlowLayoutPanel selectors = null!;
    private bool arranging;
    public AssetPreparationGuide(bool desktopPet)
    {
        Size = new Size(500, 580);
        BackColor = UiTheme.SurfaceColor; ForeColor = UiTheme.TextColor;
        tabs = new Panel();
        selectors = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 0, 0, 8) };
        Controls.Add(tabs); Controls.Add(selectors);
        tabs.Tag = selectors;
        if (desktopPet)
        {
            var idle = Page(tabs, "① 生成抬手图");
            idle.Controls.Add(Description("推荐使用 banana 或 Image2.5 生图模型制作桌面互动图片。"));
            idle.Controls.Add(Description("把下面的动作参考作为图一，你自己的角色三视图作为图二，一起交给生图模型。"));
            var pair = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Tag = "reference-pair" };
            pair.Controls.Add(Reference("pose-reference.jpg", "图一：动作参考", 270, true));
            pair.Controls.Add(Reference("three-view-example.png", "图二：换成你自己的角色三视图", 270, false));
            idle.Controls.Add(pair);
            Prompt(idle, CharacterCreationTutorial.IdlePrompt);
            var press = Page(tabs, "② 生成按左 / 按中 / 按右");
            press.Controls.Add(Description("推荐使用 banana 或 Image2.5 生图模型。提示词可按自己的角色调整。"));
            press.Controls.Add(Description("把上一步生成的抬手图交给生图模型，复制下面的提示词。拿到三张独立透明图片后，分别放进右侧对应位置。"));
            press.Controls.Add(Reference("pose-reference.jpg", "以你生成的抬手图为参考，保持完整场景", 550, false));
            Prompt(press, CharacterCreationTutorial.PressPrompt);
        }
        else
        {
            var character = Page(tabs, "① 准备角色三视图");
            character.Controls.Add(Description("推荐使用 banana 或 Image2.5 生图模型。先选好喜欢的角色，把它的图片交给生图模型，复制下面的提示词。已有三视图可以直接看下一步。"));
            character.Controls.Add(Reference("three-view-example.png", "成品示例：正面、侧面、背面使用同一个角色", 550, false));
            Prompt(character, CharacterCreationTutorial.ThreeViewPrompt);
            var video = Page(tabs, "② 制作提醒视频");
            video.Controls.Add(Description("推荐使用 Seedance 2.0 或 Seedance 2.5 模型。下面是基础动作模板，可以按自己的想法修改提示词，尝试不同动作和表现效果。"));
            video.Controls.Add(Description("将自己的角色三视图交给视频工具，复制下面的提示词生成视频，下载后在右侧上传。"));
            video.Controls.Add(Reference("three-view-example.png", "使用你自己的三视图，只让一个角色入镜", 550, false));
            Prompt(video, CharacterCreationTutorial.VideoPrompt);
            video.Controls.Add(Description("角色本身有大片绿色时，把提示词里的“纯绿背景”改成“纯蓝背景”。"));
        }
        SizeChanged += (_, _) => ArrangeGuide();
        SelectPage(tabs, (Button)selectors.Controls[0]);
    }
    private void SelectPage(Panel tabs, Button selected)
    {
        var content = (Control)selected.Tag!;
        foreach (Control page in tabs.Controls) { page.Visible = ReferenceEquals(page, content); page.Tag = ReferenceEquals(page, content) ? "selected" : null; }
        content.BringToFront();
        ArrangeGuide();
        foreach (Control button in ((FlowLayoutPanel)tabs.Tag!).Controls)
            button.ForeColor = ReferenceEquals(button, selected) ? UiTheme.AccentColor : UiTheme.MutedTextColor;
    }
    private FlowLayoutPanel Page(Panel tabs, string title)
    {
        var content = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, AutoScroll = false, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2, 8, 12, 8), BackColor = UiTheme.SurfaceColor };
        var selectors = (FlowLayoutPanel)tabs.Tag!;
        var button = CreatorUi.Button(title);
        button.Tag = content;
        button.Click += (_, _) => SelectPage(tabs, button);
        selectors.Controls.Add(button); tabs.Controls.Add(content);
        return content;
    }
    private void ArrangeGuide()
    {
        if (arranging || tabs is null || selectors is null) return;
        arranging = true;
        try
        {
            int width = Math.Max(100, ClientSize.Width);
            selectors.MinimumSize = new Size(width, 0); selectors.MaximumSize = new Size(width, 0);
            selectors.Width = width; selectors.PerformLayout();
            selectors.Location = Point.Empty;
            int top = selectors.PreferredSize.Height;
            var selected = tabs.Controls.Cast<Control>().FirstOrDefault(c => c.Visible);
            // Visibility follows the parent's window state; use the selected tab when not yet shown.
            selected ??= tabs.Controls.Cast<Control>().FirstOrDefault(c => Equals(c.Tag, "selected"));
            if (selected is null) return;
            CreatorUi.Fit(selected, width);
            selected.PerformLayout();
            int height = selected.PreferredSize.Height;
            selected.Location = Point.Empty;
            tabs.SetBounds(0, top, width, height);
            Height = top + height;
        }
        finally { arranging = false; }
    }
    private static Label Description(string text) => CreatorUi.Text(text);
    private Control Reference(string file, string caption, int width, bool export)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 8, 4) };
        using var stream = OpenReference(file);
        using var source = Image.FromStream(stream);
        var image = new Bitmap(source); ownedImages.Add(image);
        panel.Controls.Add(new PictureBox { Image = image, Size = new Size(width, 142), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(242, 243, 240), AccessibleName = caption, Margin = Padding.Empty });
        panel.Controls.Add(new Label { Text = caption, AutoSize = true, MaximumSize = new Size(width, 0), Margin = new Padding(0, 3, 0, 4) });
        if (export)
        {
            var buttons = new TableLayoutPanel { Height = 46, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var copy = CreatorUi.Button("复制图片");
            copy.Click += (_, _) => Copy(() => Clipboard.SetImage(image), copy);
            var save = CreatorUi.Button("保存图片");
            save.Click += (_, _) =>
            {
                using var dialog = new SaveFileDialog { Filter = "动作参考图|*.jpg", FileName = "图一-桌面动作参考.jpg" };
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try { using var input = OpenReference(file); using var output = File.Create(dialog.FileName); input.CopyTo(output); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(FindForm(), e.Message, "无法保存参考图"); }
            };
            copy.AutoSize = save.AutoSize = false; copy.MinimumSize = save.MinimumSize = Size.Empty;
            copy.Font = new Font("Microsoft YaHei UI", 9); save.Font = new Font("Microsoft YaHei UI", 9);
            copy.Dock = save.Dock = DockStyle.Fill;
            buttons.Controls.Add(copy); buttons.Controls.Add(save); panel.Controls.Add(buttons);
            panel.SizeChanged += (_, _) => buttons.Width = panel.ClientSize.Width;
        }
        return panel;
    }
    private static Stream OpenReference(string file) => Assembly.GetExecutingAssembly().GetManifestResourceStream("CheckInReminder.CreationGuide." + file)
        ?? throw new InvalidOperationException("缺少角色制作参考图。");
    private void Prompt(FlowLayoutPanel content, string text)
    {
        var card = new CreatorCard("制作提示词");
        var label = CreatorUi.Text(text); label.ForeColor = UiTheme.TextColor; label.AccessibleName = "制作提示词";
        card.Content.Controls.Add(label);
        content.Controls.Add(card);
        var copy = CreatorUi.Button("复制提示词");
        copy.Click += (_, _) => Copy(() => Clipboard.SetText(text), copy);
        card.Content.Controls.Add(copy);
    }
    private void Copy(Action operation, Button button)
    {
        try { operation(); button.Text = "已复制 ✓"; }
        catch (ExternalException) { MessageBox.Show(FindForm(), "剪贴板暂时被占用，请稍后再试。", "复制失败"); }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { foreach (var image in ownedImages) image.Dispose(); ownedImages.Clear(); }
    }
}
