using System.Drawing;

namespace CheckInReminder;

public readonly record struct DesktopPetRigPose(
    PointF MouseOffset,
    float MouseRotationDegrees,
    float MousePress,
    PointF KeyboardTarget,
    float KeyboardPress,
    bool KeyboardContact,
    bool IsAtRest)
{
    public static DesktopPetRigPose Rest => new(
        PointF.Empty, 0f, 0f, new PointF(0.64f, 0.34f), 0f, false, true);
}

/// <summary>
/// Converts transient desktop input into a smoothly damped render pose.
/// </summary>
public sealed class DesktopPetMotionModel
{
    private const float MouseTiltDegrees = 8f;
    private const float RestTolerance = 0.002f;
    private static readonly PointF NeutralKeyboardTarget = new(0.5f, 0.5f);

    private PointF mouseOffset;
    private float mouseRotationDegrees;
    private float mousePress;
    private PointF keyboardTarget = NeutralKeyboardTarget;
    private float keyboardPress;

    public DesktopPetRigPose Step(
        DesktopInputSnapshot input,
        Rectangle workingArea,
        TimeSpan elapsed) => Step(input, workingArea, elapsed, null, false, false);

    internal DesktopPetRigPose Step(
        DesktopInputSnapshot input,
        Rectangle workingArea,
        TimeSpan elapsed,
        PointF? keyboardPulse,
        bool leftPulse,
        bool rightPulse)
    {
        var alpha = DampingAlpha(elapsed);
        var targetMouseOffset = NormalizeCursor(input.CursorScreen, workingArea);
        var leftDown = input.LeftButtonDown || leftPulse;
        var rightDown = input.RightButtonDown || rightPulse;
        var targetMouseRotation = leftDown == rightDown
            ? 0f
            : leftDown ? -MouseTiltDegrees : MouseTiltDegrees;
        var targetMousePress = leftDown || rightDown ? 1f : 0f;

        var hasKeyboardTarget = KeyboardTargetMapper.TryMap(input.ActiveVirtualKey, out var mappedTarget);
        var targetKeyboardTarget = hasKeyboardTarget ? mappedTarget : keyboardPulse ?? NeutralKeyboardTarget;
        var keyboardContact = hasKeyboardTarget || keyboardPulse.HasValue;
        var targetKeyboardPress = keyboardContact ? 1f : 0f;

        // A complete tap between timer samples still reaches a visible pose.
        // After this single step, normal damping releases it without retaining key identity.
        var mousePressAlpha = leftPulse || rightPulse ? Math.Max(alpha, 0.6f) : alpha;
        var keyboardAlpha = keyboardPulse.HasValue ? Math.Max(alpha, 0.6f) : alpha;

        mouseOffset = Damp(mouseOffset, targetMouseOffset, alpha);
        mouseRotationDegrees = Damp(mouseRotationDegrees, targetMouseRotation, mousePressAlpha);
        mousePress = Damp(mousePress, targetMousePress, mousePressAlpha);
        keyboardPress = Damp(keyboardPress, targetKeyboardPress, keyboardAlpha);
        // A strike selects its actual zone on the first sample. Keep that X during
        // rebound; damping X through neutral would briefly show a different paw.
        var keyboardX = targetKeyboardPress > 0f && keyboardAlpha > 0f
            ? targetKeyboardTarget.X
            : keyboardPress <= RestTolerance ? NeutralKeyboardTarget.X : keyboardTarget.X;
        keyboardTarget = new PointF(keyboardX, Damp(keyboardTarget.Y, targetKeyboardTarget.Y, keyboardAlpha));

        var noInputDown = !leftDown
            && !rightDown
            && !keyboardPulse.HasValue
            && input.ActiveVirtualKey == 0;
        var isAtRest = noInputDown
            && IsNear(mouseOffset, targetMouseOffset)
            && IsNear(mouseRotationDegrees, targetMouseRotation)
            && IsNear(mousePress, targetMousePress);

        var pose = new DesktopPetRigPose(
            mouseOffset,
            mouseRotationDegrees,
            mousePress,
            keyboardTarget,
            keyboardPress,
            keyboardContact,
            isAtRest);
        if (isAtRest)
        {
            keyboardTarget = NeutralKeyboardTarget;
            keyboardPress = 0f;
        }
        return pose;
    }

    private static float DampingAlpha(TimeSpan elapsed)
    {
        var frameUnits = Math.Max(0d, elapsed.TotalMilliseconds) / (1000d / 60d);
        return (float)(1d - Math.Pow(0.75d, frameUnits));
    }

    private static PointF NormalizeCursor(Point cursor, Rectangle workingArea)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
        {
            return PointF.Empty;
        }

        var x = (((double)cursor.X - workingArea.Left) / workingArea.Width * 2d) - 1d;
        var y = (((double)cursor.Y - workingArea.Top) / workingArea.Height * 2d) - 1d;
        return new PointF((float)Math.Clamp(x, -1d, 1d), (float)Math.Clamp(y, -1d, 1d));
    }

    private static PointF Damp(PointF current, PointF target, float alpha) =>
        new(Damp(current.X, target.X, alpha), Damp(current.Y, target.Y, alpha));

    private static float Damp(float current, float target, float alpha) =>
        current + ((target - current) * alpha);

    private static bool IsNear(PointF current, PointF target) =>
        IsNear(current.X, target.X) && IsNear(current.Y, target.Y);

    private static bool IsNear(float current, float target) =>
        Math.Abs(current - target) <= RestTolerance;
}
