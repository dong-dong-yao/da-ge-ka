namespace CheckInReminder;

public enum ReminderFrameTransform
{
    None,
    FlipHorizontal,
    FlipVertical,
    RotateClockwise,
    RotateCounterClockwise,
}

public static class ReminderFrameTransformResolver
{
    public static ReminderFrameTransform Resolve(ScreenEdge sourceEdge, ScreenEdge targetEdge) =>
        (sourceEdge, targetEdge) switch
        {
            (ScreenEdge.Right, ScreenEdge.Right) or
            (ScreenEdge.Left, ScreenEdge.Left) or
            (ScreenEdge.Top, ScreenEdge.Top) or
            (ScreenEdge.Bottom, ScreenEdge.Bottom) => ReminderFrameTransform.None,

            (ScreenEdge.Right, ScreenEdge.Left) or
            (ScreenEdge.Left, ScreenEdge.Right) => ReminderFrameTransform.FlipHorizontal,

            (ScreenEdge.Top, ScreenEdge.Bottom) or
            (ScreenEdge.Bottom, ScreenEdge.Top) => ReminderFrameTransform.FlipVertical,

            (ScreenEdge.Right, ScreenEdge.Bottom) or
            (ScreenEdge.Left, ScreenEdge.Top) or
            (ScreenEdge.Top, ScreenEdge.Right) or
            (ScreenEdge.Bottom, ScreenEdge.Left) => ReminderFrameTransform.RotateClockwise,

            _ => ReminderFrameTransform.RotateCounterClockwise,
        };

    public static bool SwapsAxes(ReminderFrameTransform transform) =>
        transform is ReminderFrameTransform.RotateClockwise or ReminderFrameTransform.RotateCounterClockwise;
}
