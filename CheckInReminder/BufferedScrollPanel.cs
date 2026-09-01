namespace CheckInReminder;

internal sealed class BufferedScrollPanel : Panel
{
    public BufferedScrollPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }
}
