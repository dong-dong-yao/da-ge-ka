using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CheckInReminder;

/// <summary>固定角色取景框，保留透明背景与原始比例。</summary>
internal sealed class CharacterPreviewImage : PictureBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle SourceBounds { get; set; }

    public CharacterPreviewImage()
    {
        BackColor = Color.Transparent;
        SizeMode = PictureBoxSizeMode.Zoom;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Image is null || SourceBounds.Width <= 0 || SourceBounds.Height <= 0) return;
        var scale = Math.Min(ClientSize.Width / (float)SourceBounds.Width,
            ClientSize.Height / (float)SourceBounds.Height);
        var width = SourceBounds.Width * scale;
        var height = SourceBounds.Height * scale;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(Image, new RectangleF((Width - width) / 2, (Height - height) / 2, width, height),
            SourceBounds, GraphicsUnit.Pixel);
    }
}
