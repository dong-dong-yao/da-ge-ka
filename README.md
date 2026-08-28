# 打个卡

一个安静驻留在 Windows 系统托盘的上下班打卡提醒工具。提醒采用低透明度淡入淡出，不主动抢走正在输入的焦点；晚间确认以及关机、重启场景会增加第二道确认。

## 功能

- 上午提醒：默认 `09:30–10:00`，每 5 分钟按固定时间节点提醒。
- 晚上提醒：默认从 `19:00` 开始，每 10 分钟按固定时间节点提醒。
- 顶部提醒显示表情包和“已打卡，闭嘴”按钮，5 秒后自动淡出。
- 晚间第二道确认显示“真的吗，那你为什么不走”，提供“真的 / 假的”两个选项。
- 可配置提醒时间、提醒间隔和开机自启动。
- Windows 普通关机或重启时，在晚间尚未确认的情况下进行 best-effort 提醒。
- 单实例运行，不联网，不记录打卡历史。

## 系统要求

- Windows 10 或 Windows 11 x64
- 从源码构建需要 .NET 10 SDK

## 使用发布版

1. 下载或构建 `打个卡.exe`，放在固定目录。
2. 双击启动；程序不会打开主窗口，而是驻留系统托盘。
3. 双击托盘图标，或右键选择“设置”，即可修改提醒参数。
4. 托盘右键选择“退出程序”可直接退出。

配置保存在 `%LOCALAPPDATA%\CheckInReminder\config.json`。配置目录沿用内部名称以兼容已有用户数据；程序不会保存上午或晚上的完成状态，重新启动后状态会重置。

## 从源码构建

```powershell
dotnet restore .\CheckInReminder.slnx
dotnet test .\CheckInReminder.slnx -c Release
dotnet build .\CheckInReminder.slnx -c Release
dotnet publish .\CheckInReminder\CheckInReminder.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

不要开启 trimming。WinForms 与本项目使用的运行时能力不适合在没有专项验证的情况下裁剪。

## 项目结构

- `CheckInReminder/`：WinForms 程序、提醒调度、设置持久化、系统托盘和视觉资源。
- `CheckInReminder.Tests/`：时间节点、窗口边界和视觉主题契约测试。
- `VERIFICATION.md`：自动化验证结果与仍需人工检查的 Windows 行为。

## 已知边界

- Windows 的普通 shutdown/restart 参数无法可靠区分关机和重启，因此晚间未完成时两者使用相同提醒流程。
- Windows 仍可能显示自己的 shutdown blocker 界面，用户也可以选择强制结束程序。
- 物理断电、长按电源键和任务管理器强制结束不在处理范围内。
- 当前不处理跨午夜状态。
- 自编译程序没有代码签名，可能触发 SmartScreen、Defender 或企业安全策略。
- 不要使用自动化脚本触发真实关机或重启测试。

Win32 行为参考：

- [WM_QUERYENDSESSION](https://learn.microsoft.com/windows/win32/shutdown/wm-queryendsession)
- [ShutdownBlockReasonCreate](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-shutdownblockreasoncreate)
- [ShutdownBlockReasonDestroy](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-shutdownblockreasondestroy)
- [Windows Vista 之后的关机行为](https://learn.microsoft.com/windows/win32/shutdown/shutdown-changes-for-windows-vista)

## 验证状态

自动化测试覆盖时间计算与主题契约。托盘交互、NoActivate 焦点行为、开机启动、睡眠恢复和关机拦截必须在真实 Windows 环境人工验证，具体清单见 [VERIFICATION.md](VERIFICATION.md)。

## 许可证

本项目使用 [MIT License](LICENSE)。
