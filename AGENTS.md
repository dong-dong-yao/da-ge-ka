# 项目规则

## 定位

“打个卡”是仅面向 Windows 10/11 x64 的本地 WinForms 打卡提醒工具。

## 常用命令

`global.json` 锁定 SDK 10.0.400。装有该 SDK 的电脑可直接用 `dotnet`；本机请用 `.\.dotnet\dotnet.exe`（当前工作树通过目录链接共用主仓库 SDK）。

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
- 不持久化完成状态、日期或历史，只保存十二项用户设置（原十项加角色 ID 与桌面宠物开关）。
- 保留提醒文案和固定时间锚点语义；更改行为时先补测试。
- 发布必须是 `win-x64`、self-contained、single-file，禁止 trimming。
- 不运行无人值守的真实关机或重启测试。
- 新增角色只需：`Assets/Animations/{角色Id}/` 放帧序列（`frame_0000.png` 起），在 `AnimationCatalog.BuildCharacters()` 注册一行；桌面宠物素材目录约定为 `{角色Id}-pet-idle` / `{角色Id}-pet-tap`（可选，缺失时自动降级为占位帧）。详细 SOP 见 README。

## 当前状态（2026-09-21）

当前版本为 v1.2.0，后续按 GitHub Issue 反馈维护。功能和构建边界见 README，最终验收与发布状态见 VERIFICATION.md。
本机开发入口仍为 `.worktrees/bongocat-rig`；根目录保留旧分支和宣传素材，勿当作最新源码使用。保留有效工作树、`.dotnet` SDK、原始素材和回退版。源码仓库不含视频组件 ZIP，完整组件随 Release 提供，GitHub Actions 不自动发布完整 EXE。
发布必须同时完成：更新版本与文档、测试、构建完整 EXE、上传非预发布 GitHub Release 并设为 latest、从公开下载入口回下载并核对 SHA256。只推送 main 不算面向用户发布完成。使用声明与组件许可统一维护于 THIRD_PARTY_NOTICES.md，随 EXE 嵌入。
