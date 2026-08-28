using Microsoft.Win32;
using System.Security;

namespace CheckInReminder;

internal sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CheckInReminder";

    public bool Apply(bool enabled, out string? errorMessage)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled)
            {
                var executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    errorMessage = "无法获取当前程序路径，未能开启开机自启动。";
                    return false;
                }

                var expectedValue = $"\"{executablePath}\"";
                var currentValue = key.GetValue(ValueName) as string;
                if (!string.Equals(currentValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(ValueName, expectedValue, RegistryValueKind.String);
                }
            }
            else
            {
                if (key.GetValue(ValueName) is not null)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }

            errorMessage = null;
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            errorMessage = $"开机自启动设置失败：{exception.Message}";
            return false;
        }
    }
}
