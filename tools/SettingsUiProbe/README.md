# 设置页实机 DPI 验证

在工作树根目录使用本地 SDK 运行（主仓库 SDK 位于 `../../.dotnet/dotnet.exe`）：

```powershell
../../.dotnet/dotnet.exe run --project tools/SettingsUiProbe/SettingsUiProbe.csproj -c Release -- artifacts/settings-dashboard/native-after
../../.dotnet/dotnet.exe run --project tools/SettingsUiProbe/SettingsUiProbe.csproj -c Release -- artifacts/settings-dashboard/native-100 --unaware
```

默认直接反射调用正式程序生成的 `ApplicationConfiguration.Initialize()`；`--unaware` 在独立进程中验证 100% 坐标模式。每次进程都单独启动，避免 WinForms 的全局 DPI 缓存使不同模式的测试实际都落在 96 DPI。

工具只创建屏幕外的 SettingsForm，保存截图、输入控件的像素和逻辑尺寸到 `measurements.json`。不启动提醒上下文、不写用户设置、不操作关机。输入框超出 140×38 逻辑像素上限或高度不足 30 时返回非零退出码。更严格的 138×34 尺寸契约另由 SettingsDashboardTests 检查。
