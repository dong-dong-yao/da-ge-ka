using System.Runtime.InteropServices;

namespace CheckInReminder;

internal readonly record struct GlobalInputInstallResult(bool Success, int Win32Error);

/// <summary>
/// Captures transient keyboard and mouse state without translating keys to text.
/// Low-level hooks execute on their installing thread; subscribers may map bounded visual pulses, never render.
/// </summary>
internal sealed class GlobalInputService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseMove = 0x0200;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;

    private readonly DesktopInputState state;
    private readonly Func<int, Delegate, IntPtr> installHook;
    private readonly Func<IntPtr, bool> uninstallHook;
    private readonly LowLevelHookProc keyboardHookProc;
    private readonly LowLevelHookProc mouseHookProc;
    private IntPtr keyboardHookHandle;
    private IntPtr mouseHookHandle;
    private bool disposed;

    public event EventHandler? InputAvailable;

    internal GlobalInputService(DesktopInputState state)
        : this(state, InstallNativeHook, UnhookWindowsHookEx)
    {
    }

    internal GlobalInputService(
        DesktopInputState state,
        Func<int, Delegate, IntPtr> installHook,
        Func<IntPtr, bool> uninstallHook)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.installHook = installHook ?? throw new ArgumentNullException(nameof(installHook));
        this.uninstallHook = uninstallHook ?? throw new ArgumentNullException(nameof(uninstallHook));
        keyboardHookProc = KeyboardHookCallback;
        mouseHookProc = MouseHookCallback;
    }

    public bool IsInstalled => keyboardHookHandle != IntPtr.Zero && mouseHookHandle != IntPtr.Zero;

    public GlobalInputInstallResult Install()
    {
        if (disposed)
        {
            return new GlobalInputInstallResult(false, 0);
        }

        if (IsInstalled)
        {
            return new GlobalInputInstallResult(true, 0);
        }

        keyboardHookHandle = installHook(WhKeyboardLl, keyboardHookProc);
        if (keyboardHookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            return new GlobalInputInstallResult(false, error);
        }

        mouseHookHandle = installHook(WhMouseLl, mouseHookProc);
        if (mouseHookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            uninstallHook(keyboardHookHandle);
            keyboardHookHandle = IntPtr.Zero;
            return new GlobalInputInstallResult(false, error);
        }

        return new GlobalInputInstallResult(true, 0);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = unchecked((int)wParam.ToInt64());
                if (message is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
                {
                    var virtualKey = Marshal.ReadInt32(lParam);
                    state.UpdateKey(virtualKey, message is WmKeyDown or WmSysKeyDown);
                    InputAvailable?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch
        {
            // Exceptions must never escape a low-level system hook callback.
        }

        return CallNextHookEx(keyboardHookHandle, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = unchecked((int)wParam.ToInt64());
                if (message is WmMouseMove or WmLeftButtonDown or WmLeftButtonUp or
                    WmRightButtonDown or WmRightButtonUp)
                {
                    state.UpdatePointer(Marshal.ReadInt32(lParam), Marshal.ReadInt32(lParam, sizeof(int)));
                    if (message is WmLeftButtonDown or WmLeftButtonUp)
                    {
                        state.UpdateMouseButton(DesktopMouseButton.Left, message == WmLeftButtonDown);
                    }
                    else if (message is WmRightButtonDown or WmRightButtonUp)
                    {
                        state.UpdateMouseButton(DesktopMouseButton.Right, message == WmRightButtonDown);
                    }

                    InputAvailable?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch
        {
            // Exceptions must never escape a low-level system hook callback.
        }

        return CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        var keyboardHandle = keyboardHookHandle;
        var mouseHandle = mouseHookHandle;
        keyboardHookHandle = IntPtr.Zero;
        mouseHookHandle = IntPtr.Zero;

        if (keyboardHandle != IntPtr.Zero)
        {
            uninstallHook(keyboardHandle);
        }

        if (mouseHandle != IntPtr.Zero)
        {
            uninstallHook(mouseHandle);
        }
    }

    private static IntPtr InstallNativeHook(int hookType, Delegate callback)
    {
        var callbackPointer = Marshal.GetFunctionPointerForDelegate(callback);
        return SetWindowsHookEx(hookType, callbackPointer, GetModuleHandle(null), 0);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, IntPtr callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
