# 项目规则

## 定位

“打个卡”是仅面向 Windows 10/11 x64 的本地 WinForms 打卡提醒工具。

## 常用命令

本机裸 `dotnet` 无 SDK（`global.json` 锁定 10.0.400），请用本地 SDK：`.\.dotnet\dotnet.exe` 替代下列 `dotnet`。

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

## 当前状态

截至 2026-09-03，本地测试为 62/62 通过。设置窗口改为左侧导航双页面（设置 / 角色），设置页右列显示当前角色只读预览，角色页为网格卡片选择器（同一时间只播一张预览）；新增桌面宠物窗口骨架（透明置顶、默认鼠标穿透、托盘可切拖动模式，开关为第十二项设置，默认关闭，当前为静帧占位）。设置窗口缩放/滚动、同列开关对齐、页面切换与隐藏页暂停、交互期间动画暂停与滚动双缓冲、关机守护窗体生命周期已有自动化覆盖；透明动画、完整设置页交互、托盘、焦点、自启动、桌面宠物显示与真实关机拦截仍需在 Windows 10/11 环境人工验证。
