using System.Drawing;
using System.Threading;

namespace CheckInReminder;

public enum DesktopMouseButton
{
    Left,
    Right,
}

public readonly record struct DesktopInputSnapshot(
    Point CursorScreen,
    bool LeftButtonDown,
    bool RightButtonDown,
    int ActiveVirtualKey,
    long Version)
{
    public long ActiveKeyPressSequence { get; init; }
    public long LeftButtonPressSequence { get; init; }
    public long RightButtonPressSequence { get; init; }
}

/// <summary>
/// Stores the transient, text-free input state consumed by the desktop pet.
/// </summary>
public sealed class DesktopInputState
{
    private const int KeyCount = 256;
    private const int LeftButtonMask = 1;
    private const int RightButtonMask = 2;

    private readonly int[] pressedKeys = new int[KeyCount];
    private readonly long[] pressOrders = new long[KeyCount];
    private readonly object keyUpdateGate = new();
    private readonly object mouseUpdateGate = new();
    private long nextPressOrder;
    private int cursorX;
    private int cursorY;
    private int mouseButtons;
    private long leftButtonPressSequence;
    private long rightButtonPressSequence;
    private long version;

    public void UpdateKey(int virtualKey, bool pressed)
    {
        if ((uint)virtualKey >= KeyCount || virtualKey == 0)
        {
            return;
        }

        lock (keyUpdateGate)
        {
            var previous = Volatile.Read(ref pressedKeys[virtualKey]);
            var value = pressed ? 1 : 0;
            if (previous == value)
            {
                return;
            }

            if (pressed)
            {
                Volatile.Write(ref pressOrders[virtualKey], Interlocked.Increment(ref nextPressOrder));
            }
            else
            {
                Volatile.Write(ref pressOrders[virtualKey], 0);
            }

            Volatile.Write(ref pressedKeys[virtualKey], value);
            Interlocked.Increment(ref version);
        }
    }

    public void UpdatePointer(int x, int y)
    {
        Volatile.Write(ref cursorX, x);
        Volatile.Write(ref cursorY, y);
        Interlocked.Increment(ref version);
    }

    public void UpdateMouseButton(DesktopMouseButton button, bool pressed)
    {
        var mask = button switch
        {
            DesktopMouseButton.Left => LeftButtonMask,
            DesktopMouseButton.Right => RightButtonMask,
            _ => 0,
        };
        if (mask == 0)
        {
            return;
        }

        lock (mouseUpdateGate)
        {
            var wasPressed = (mouseButtons & mask) != 0;
            if (wasPressed == pressed) return;
            if (pressed)
            {
                mouseButtons |= mask;
                if (button == DesktopMouseButton.Left) leftButtonPressSequence++;
                else rightButtonPressSequence++;
            }
            else mouseButtons &= ~mask;
        }

        Interlocked.Increment(ref version);
    }

    public DesktopInputSnapshot ReadSnapshot()
    {
        var activeKey = 0;
        var activePressSequence = 0L;
        lock (keyUpdateGate)
        {
            var activeOrder = 0L;
            for (var key = 1; key < KeyCount; key++)
            {
                if (Volatile.Read(ref pressedKeys[key]) == 0)
                {
                    continue;
                }

                var order = Volatile.Read(ref pressOrders[key]);
                if (order > activeOrder)
                {
                    activeOrder = order;
                    activeKey = key;
                }
            }
            activePressSequence = activeOrder;
        }

        int buttons;
        long leftSequence;
        long rightSequence;
        lock (mouseUpdateGate)
        {
            buttons = mouseButtons;
            leftSequence = leftButtonPressSequence;
            rightSequence = rightButtonPressSequence;
        }
        return new DesktopInputSnapshot(
            new Point(Volatile.Read(ref cursorX), Volatile.Read(ref cursorY)),
            (buttons & LeftButtonMask) != 0,
            (buttons & RightButtonMask) != 0,
            activeKey,
            Volatile.Read(ref version))
        {
            ActiveKeyPressSequence = activePressSequence,
            LeftButtonPressSequence = leftSequence,
            RightButtonPressSequence = rightSequence,
        };
    }
}
