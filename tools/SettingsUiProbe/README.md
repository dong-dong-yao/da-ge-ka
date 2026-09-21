# 设置页实机 DPI 验证

在工作树根目录使用本地 SDK 运行（主仓库 SDK 位于 `../../.dotnet/dotnet.exe`）：

```powershell
../../.dotnet/dotnet.exe run --project tools/SettingsUiProbe/SettingsUiProbe.csproj -c Release -- artifacts/settings-dashboard/native-after
../../.dotnet/dotnet.exe run --project tools/SettingsUiProbe/SettingsUiProbe.csproj -c Release -- artifacts/settings-dashboard/native-100 --unaware
```

默认直接反射调用正式程序生成的 `ApplicationConfiguration.Initialize()`；`--unaware` 在独立进程中验证 100% 坐标模式。每次进程都单独启动，避免 WinForms 的全局 DPI 缓存使不同模式的测试实际都落在 96 DPI。

工具只创建屏幕外的 SettingsForm，保存截图、输入控件的像素和逻辑尺寸到 `measurements.json`。不启动提醒上下文、不写用户设置、不操作关机。输入框超出 140×38 逻辑像素上限或高度不足 30 时返回非零退出码。更严格的 138×34 尺寸契约另由 SettingsDashboardTests 检查。

## 角色创建与鼠标修复回归

在上述命令的输出目录之后附加 `--creator`，可验证四步创建向导、方向视频展开后立即滚动，以及40轮展开/收起/缩放；会生成测试素材和截图，需要先打包视频组件。附加 `--gallery` 检查角色画廊。两者均可再附加 `--unaware` 检查100%坐标模式。

`--selection <角色包目录>` 检查四张原图的鼠标垫选取与修复窗，`--mouse-diagnosis <角色包目录>` 导出鼠标分层诊断。输出目录必须放在这些参数之前；这些模式读取角色包，不修改用户保存的角色。
