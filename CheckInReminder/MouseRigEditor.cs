using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

internal sealed class MouseRigEditor : Control
{
    private Bitmap? background;
    private Bitmap? arm;
    private Point? dragStart;
    private RectangleF initialPlacement;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int EditMode { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RectangleF Placement { get; set; } = new(0, 0, 1, 1);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CustomMouseRig Rig { get; set; } = new();
    public MouseRigEditor()
    {
        DoubleBuffered = true;
        Size = new Size(450, 336);
        BackColor = Color.FromArgb(230, 232, 229);
        Cursor = Cursors.Cross;
        AccessibleName = "鼠标手臂摆放预览";
    }
    public void SetImages(Bitmap? bottom, Bitmap? moving)
    {
        background?.Dispose(); arm?.Dispose();
        background = bottom; arm = moving; Invalidate();
    }
    private RectangleF Canvas
    {
        get { var scale = Math.Min(Width / 600f, Height / 448f); return new((Width - 600 * scale) / 2, (Height - 448 * scale) / 2, 600 * scale, 448 * scale); }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var c = Canvas;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (background is not null) e.Graphics.DrawImage(background, c);
        if (arm is not null) e.Graphics.DrawImage(arm, new RectangleF(c.X + Placement.X * c.Width, c.Y + Placement.Y * c.Height, Placement.Width * c.Width, Placement.Height * c.Height));
        DrawMarker(e.Graphics, c, Rig.ShoulderX, Rig.ShoulderY, "连接处", Color.OrangeRed);
        DrawMarker(e.Graphics, c, Rig.MouseX, Rig.MouseY, "鼠标", Color.RoyalBlue);
    }
    private void DrawMarker(Graphics g, RectangleF c, float x, float y, string label, Color color)
    {
        using var pen = new Pen(color, 2);
        var px = c.X + x * c.Width; var py = c.Y + y * c.Height;
        g.DrawEllipse(pen, px - 5, py - 5, 10, 10);
        TextRenderer.DrawText(g, label, Font, new Point((int)px + 7, (int)py), color);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var c = Canvas;
        if (!c.Contains(e.Location)) return;
        var x = Math.Clamp((e.X - c.X) / c.Width, 0, 1); var y = Math.Clamp((e.Y - c.Y) / c.Height, 0, 1);
        if (EditMode == 1) { Rig.ShoulderX = x; Rig.ShoulderY = y; }
        else if (EditMode == 2) { Rig.MouseX = x; Rig.MouseY = y; }
        else { dragStart = e.Location; initialPlacement = Placement; Capture = true; }
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragStart is not { } start) return;
        var c = Canvas;
        Placement = initialPlacement with { X = Math.Clamp(initialPlacement.X + (e.X - start.X) / c.Width, -.8f, .8f), Y = Math.Clamp(initialPlacement.Y + (e.Y - start.Y) / c.Height, -.8f, .8f) };
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragStart = null; Capture = false; base.OnMouseUp(e); }
    protected override void Dispose(bool disposing) { if (disposing) { background?.Dispose(); arm?.Dispose(); } base.Dispose(disposing); }
}
