# 项目规则

## 定位

“打个卡”是仅面向 Windows 10/11 x64 的本地 WinForms 打卡提醒工具。

## 常用命令

```powershell
dotnet test .\CheckInReminder.slnx -c Release
dotnet build .\CheckInReminder.slnx -c Release
dotnet publish .\CheckInReminder\CheckInReminder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

## 技术栈

- C# / .NET 10 / WinForms
- MSTest
- HKCU Run 开机启动与 Win32 结束会话 API

## 目录与约定

- `CheckInReminder/`：程序源码和嵌入式视觉资源。
- `CheckInReminder.Tests/`：时间计算与主题契约测试。
- `README.md`：公开使用、构建及边界说明的权威入口。
- 不持久化完成状态、日期或历史，只保存十项用户设置。
- 保留提醒文案和固定时间锚点语义；更改行为时先补测试。
- 发布必须是 `win-x64`、self-contained、single-file，禁止 trimming。
- 不运行无人值守的真实关机或重启测试。

## 当前状态

截至 2026-08-30，本地测试为 35/35 通过，Release 构建为 0 警告、0 错误。透明动画、托盘、焦点、自启动和关机拦截仍需在真实 Windows 10/11 环境人工验证。
