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
    long Version);

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
    private long nextPressOrder;
    private int cursorX;
    private int cursorY;
    private int mouseButtons;
    private long version;

    public void UpdateKey(int virtualKey, bool pressed)
    {
        if ((uint)virtualKey >= KeyCount || virtualKey == 0)
        {
            return;
        }

        var value = pressed ? 1 : 0;
        var previous = Interlocked.Exchange(ref pressedKeys[virtualKey], value);
        if (pressed && previous == 0)
        {
            Volatile.Write(ref pressOrders[virtualKey], Interlocked.Increment(ref nextPressOrder));
        }

        if (previous != value)
        {
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

        if (pressed)
        {
            Interlocked.Or(ref mouseButtons, mask);
        }
        else
        {
            Interlocked.And(ref mouseButtons, ~mask);
        }

        Interlocked.Increment(ref version);
    }

    public DesktopInputSnapshot ReadSnapshot()
    {
        var activeKey = 0;
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

        var buttons = Volatile.Read(ref mouseButtons);
        return new DesktopInputSnapshot(
            new Point(Volatile.Read(ref cursorX), Volatile.Read(ref cursorY)),
            (buttons & LeftButtonMask) != 0,
            (buttons & RightButtonMask) != 0,
            activeKey,
            Volatile.Read(ref version));
    }
}
