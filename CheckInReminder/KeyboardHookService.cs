using System.Runtime.InteropServices;

namespace CheckInReminder;

/// <summary>
/// 全局低级键盘钩子（WH_KEYBOARD_LL）封装。
/// 回调内只做计数（绝不读取键值），经 UI 线程节流后引发 <see cref="KeyTapped"/>。
/// 本功能仅用于驱动桌面宠物动画：不读取、不记录、不上传任何键值。
/// </summary>
internal sealed class KeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;

    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(40);

    private readonly SynchronizationContext? syncContext;
    private readonly KeyTapThrottler throttler = new(ThrottleWindow);
    private readonly LowLevelKeyboardProc hookProc; // 必须存字段，防止 GC 回收后回调崩溃
    private IntPtr hookHandle;
    private int pendingCount;
    private bool disposed;

    public event EventHandler? KeyTapped;

    /// <summary>必须在拥有消息泵的 UI 线程上构造（捕获同步上下文）。</summary>
    public KeyboardHookService()
    {
        syncContext = SynchronizationContext.Current;
        hookProc = HookCallback;
    }

    public bool IsInstalled => hookHandle != IntPtr.Zero;

    /// <summary>安装全局钩子；被杀软拦截等原因失败时返回 false（调用方静默降级）。</summary>
    public bool Install()
    {
        if (disposed || hookHandle != IntPtr.Zero)
        {
            return hookHandle != IntPtr.Zero;
        }

        hookHandle = SetWindowsHookEx(WhKeyboardLl, hookProc, GetModuleHandle(null), 0);
        return hookHandle != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(hookHandle);
        hookHandle = IntPtr.Zero;
        Interlocked.Exchange(ref pendingCount, 0);
        throttler.Reset();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 硬约束：回调必须极快返回（超时系统会静默摘除钩子）；
        // 不分配、不碰 UI、不读 KBDLLHOOKSTRUCT（不记录键值）。
        try
        {
            if (nCode >= 0 &&
                (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown) &&
                Interlocked.Increment(ref pendingCount) == 1)
            {
                syncContext?.Post(_ => DrainPending(), null);
            }
        }
        catch
        {
            // 回调内任何异常都不能逃逸到系统钩子链
        }

        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private void DrainPending()
    {
        var count = Interlocked.Exchange(ref pendingCount, 0);
        if (count <= 0 || disposed || hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (throttler.ShouldSignal(TimeSpan.FromMilliseconds(Environment.TickCount64)))
        {
            KeyTapped?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Uninstall();
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
