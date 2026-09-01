using System.Runtime.InteropServices;

namespace CheckInReminder;

internal sealed class ShutdownGuardForm : Form
{
    private const int WmQueryEndSession = 0x0011;
    private const uint EndSessionCloseApp = 0x00000001;
    private const uint EndSessionCritical = 0x40000000;
    private const uint EndSessionLogoff = 0x80000000;
    private const string BlockReason = "还没确认下班打卡，先别关机。";

    private readonly Func<bool> shouldBlock;
    private readonly Action shutdownVetoed;
    private bool reasonRegistered;
    private bool exitRequested;

    public ShutdownGuardForm(Func<bool> shouldBlock, Action shutdownVetoed)
    {
        this.shouldBlock = shouldBlock;
        this.shutdownVetoed = shutdownVetoed;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => true;

    internal static bool ShouldAlwaysAllow(uint flags) =>
        (flags & (EndSessionCloseApp | EndSessionCritical | EndSessionLogoff)) != 0;

    public void UpdateRegistration(bool shouldRegister)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (shouldRegister == reasonRegistered)
        {
            return;
        }

        if (shouldRegister)
        {
            reasonRegistered = ShutdownBlockReasonCreate(Handle, BlockReason);
        }
        else
        {
            ShutdownBlockReasonDestroy(Handle);
            reasonRegistered = false;
        }
    }

    public void CloseForExit()
    {
        if (IsDisposed)
        {
            return;
        }

        exitRequested = true;
        UpdateRegistration(shouldRegister: false);
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        if (!exitRequested && eventArgs.CloseReason == CloseReason.UserClosing)
        {
            eventArgs.Cancel = true;
            return;
        }

        base.OnFormClosing(eventArgs);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmQueryEndSession)
        {
            var flags = unchecked((uint)message.LParam.ToInt64());
            if (!ShouldAlwaysAllow(flags) && shouldBlock())
            {
                BeginInvoke(shutdownVetoed);
                message.Result = IntPtr.Zero;
                return;
            }
        }

        base.WndProc(ref message);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShutdownBlockReasonCreate(IntPtr windowHandle, string reason);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShutdownBlockReasonDestroy(IntPtr windowHandle);
}
