using System.Drawing;

namespace CheckInReminder;

/// <summary>
/// Maps Windows virtual keys onto a perspective-neutral logical keyboard.
/// </summary>
public static class KeyboardTargetMapper
{
    private static readonly KeyRow[] Rows =
    [
        new([0x1B], 0.03f, 0.03f, 0.06f),
        new([0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B], 0.12f, 0.97f, 0.06f),
        new([0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30], 0.08f, 0.82f, 0.18f),
        new([0x51, 0x57, 0x45, 0x52, 0x54, 0x59, 0x55, 0x49, 0x4F, 0x50], 0.10f, 0.90f, 0.28f),
        new([0x41, 0x53, 0x44, 0x46, 0x47, 0x48, 0x4A, 0x4B, 0x4C], 0.14f, 0.86f, 0.55f),
        new([0x5A, 0x58, 0x43, 0x56, 0x42, 0x4E, 0x4D], 0.18f, 0.78f, 0.80f),
    ];

    private static readonly KeyTarget[] SpecialKeys =
    [
        new(0x08, 0.94f, 0.18f), // Backspace
        new(0x09, 0.03f, 0.28f), // Tab
        new(0x0D, 0.95f, 0.55f), // Enter
        new(0x10, 0.05f, 0.80f), // Shift
        new(0xA0, 0.05f, 0.80f), // Left Shift
        new(0xA1, 0.95f, 0.80f), // Right Shift
        new(0x11, 0.05f, 0.92f), // Control
        new(0xA2, 0.05f, 0.92f), // Left Control
        new(0xA3, 0.95f, 0.92f), // Right Control
        new(0x12, 0.24f, 0.92f), // Alt
        new(0xA4, 0.24f, 0.92f), // Left Alt
        new(0xA5, 0.76f, 0.92f), // Right Alt
        new(0x20, 0.50f, 0.92f), // Space
    ];

    public static bool TryMap(int virtualKey, out PointF normalizedTarget)
    {
        foreach (var row in Rows)
        {
            var index = Array.IndexOf(row.VirtualKeys, virtualKey);
            if (index < 0)
            {
                continue;
            }

            var x = row.VirtualKeys.Length == 1
                ? row.StartX
                : row.StartX + ((row.EndX - row.StartX) * index / (row.VirtualKeys.Length - 1));
            normalizedTarget = new PointF(x, row.Y);
            return true;
        }

        foreach (var key in SpecialKeys)
        {
            if (key.VirtualKey != virtualKey)
            {
                continue;
            }

            normalizedTarget = new PointF(key.X, key.Y);
            return true;
        }

        normalizedTarget = PointF.Empty;
        return false;
    }

    private readonly record struct KeyRow(int[] VirtualKeys, float StartX, float EndX, float Y);

    private readonly record struct KeyTarget(int VirtualKey, float X, float Y);
}
