<div align="center">
  <img src="docs/media/dagaka-icon.png" alt="打个卡图标" width="160">
  <h1>打个卡</h1>
  <p>
    <a href="https://github.com/dong-dong-yao/da-ge-ka/actions/workflows/build.yml"><img src="https://github.com/dong-dong-yao/da-ge-ka/actions/workflows/build.yml/badge.svg" alt="自动测试"></a>
    <a href="https://github.com/dong-dong-yao/da-ge-ka/releases/latest"><img src="https://img.shields.io/github/v/release/dong-dong-yao/da-ge-ka?label=Release" alt="最新版本"></a>
    <a href="https://github.com/dong-dong-yao/da-ge-ka/releases"><img src="https://img.shields.io/github/downloads/dong-dong-yao/da-ge-ka/total?label=Downloads" alt="下载量"></a>
    <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows" alt="Windows 10 和 11">
    <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet" alt=".NET 10">
    <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-0A84FF" alt="MIT License"></a>
  </p>
  <p>
    <a href="https://github.com/dong-dong-yao/da-ge-ka/releases/latest"><strong>⬇️ 下载最新版</strong></a>
    ·
    <a href="CONTRIBUTING.md"><strong>🐾 一起共建</strong></a>
  </p>
</div>

由于作者总是忘记上下班打卡，于是，打个卡诞生了...

一个安静躺在 Windows 系统托盘的上下班打卡提醒工具。你喜欢的角色（可以自己设置）会从主屏幕四周随机探出并用气泡提醒你打卡了吗，不主动抢走正在输入的焦点；晚间确认以及关机、重启时会出现第二道确认。

## 功能

- 上班打卡前提醒：默认 `09:30–10:00`，每 5 分钟按固定时间节点提醒。（可根据实际上班时间修改）
- 下班打卡前提醒：默认从 `19:00` 开始，每 10 分钟按固定时间节点提醒。（可根据实际上班时间修改）
- 角色动画随机出现在屏幕上、下、左、右边缘，保持素材原始约 7.1 秒节奏，气泡提供“已打卡，闭嘴”按钮。
- 设置窗口采用左侧导航双页面：「设置」页可自由缩放，较小高度下可滚动访问全部内容；缩放或滚动时会暂缓角色预览并合并绘制，结束交互后自动恢复动画。「角色」页用网格卡片浏览全部角色，悬停卡片即可预览动画，确认后设置页同步显示当前角色。当前内置白熊，角色目录可继续扩展。
- 可选桌面宠物：开启后角色常驻桌面角落（默认右下角），透明置顶、鼠标穿透不挡操作；敲键盘时角色会交替产生左右敲击反馈，停止敲击约 0.4 秒后恢复待机。当前白熊直接使用 `Assets/DesktopPet/white-bear-typing.jpg` 原图，运行时仅做边缘连通背景透明化和程序化按压反馈，不生成或替换角色画面。托盘菜单可切换「调整宠物位置」模式拖动换位。默认关闭，可在设置页或托盘菜单开启。
- 桌面宠物通过全局键盘钩子感知"有键被按下"的瞬间来驱动动画，**只计数、从不读取键值**：不记录、不保存、不上传任何输入内容（程序本身不联网）。若安全软件拦截钩子安装，宠物会静默降级为静态显示，不影响其他功能。
- 晚间第二道确认显示“真的吗，那你为什么不走”，提供“真的 / 假的”两个选项；约 4.09 秒动画播完后从第一帧重新循环。
- 可配置打卡时间、提醒间隔、是否开机自启动和是否常驻桌面宠物。
- 可选久坐提醒，默认关闭；默认 `09:00–18:00`，可选每 `1 / 1.5 / 2 / 3` 小时提醒。久坐气泡没有按钮，只会自动消失。
- 打卡提醒优先；冲突时久坐提醒最多排队一次，未来提醒仍使用原固定时间节点。
- Windows 普通关机或重启时，在晚间尚未确认的情况下进行 best-effort 提醒。
- 单实例运行，不联网，不记录打卡历史。

## 一起把“打个卡”变得更可爱

打个卡现在还只有一只白熊，但它不应该永远只有一只白熊。

特别欢迎你一起参与：不会写代码也完全没关系。你可以画一个原创小角色，制作 GIF、视频或透明 PNG 动画，想一句更可爱的提醒文案，分享功能点子；也可以帮助改进代码、界面，或者在不同的 Windows 电脑上试用并反馈问题。

被项目采用的角色和素材，会在仓库中标注作者名字与个人主页。为了保护创作者和使用者，请只投稿自己的原创作品，或你明确拥有公开授权的素材；不要直接提交未经许可的动漫、游戏、影视或表情包角色。

- [查看参与贡献说明](CONTRIBUTING.md)
- [投稿一个新角色](https://github.com/dong-dong-yao/da-ge-ka/issues/new?template=character-submission.yml)
- [提出问题或新点子](https://github.com/dong-dong-yao/da-ge-ka/issues/new/choose)

## 系统要求

- Windows 10 或 Windows 11 x64
- 从源码构建需要 .NET 10 SDK

## 快速安装

1. 前往 [下载最新版](https://github.com/dong-dong-yao/da-ge-ka/releases/latest)，下载 Release 中的 `DaGeKa-v1.0.0-win-x64.exe`，放在固定目录；程序启动后显示的名称仍然是“打个卡”。
2. 双击启动；程序不会打开主窗口，而是驻留系统托盘。
3. 双击托盘图标，或右键选择“设置”，即可修改提醒参数。
4. 托盘右键选择“退出程序”可直接退出。

配置保存在 `%LOCALAPPDATA%\CheckInReminder\config.json`。配置目录沿用内部名称以兼容已有用户数据；旧配置会自动补上久坐提醒默认值和默认角色 `white-bear`。程序不会保存上午、晚上或久坐的完成状态与历史，重新启动后状态会重置。

## 从源码构建

> 本机的裸 `dotnet` 没有安装 SDK（`global.json` 锁定 10.0.400），请使用本地 SDK：把下列命令里的 `dotnet` 换成 `.\.dotnet\dotnet.exe`。

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
- `tools/convert_green_screen.py`：开发期将绿幕 MP4 转为软边透明 PNG 序列；发布后的 EXE 不依赖该脚本或 FFmpeg。
- `VERIFICATION.md`：自动化验证结果与仍需人工检查的 Windows 行为。

## 添加一个新角色

1. 用 `tools/convert_green_screen.py` 把角色的绿幕 MP4 转成透明 PNG 序列。
2. 在 `CheckInReminder/Assets/Animations/` 下新建以角色 Id（kebab-case，如 `shiba`）命名的目录，帧文件按 `frame_0000.png` 起的四位序号命名，把序列放进去（csproj 已按通配符嵌入，无需改动工程文件）。
3. （可选）桌面宠物素材放入 `{角色Id}-pet-idle/`（待机）和 `{角色Id}-pet-tap/`（敲击）目录；未提供时宠物自动降级为占位显示。
4. 在 `CheckInReminder/AnimationCatalog.cs` 的 `BuildCharacters()` 里注册一行：`new("shiba", "柴犬", "shiba", TimeSpan.FromSeconds(时长), false)`。
5. 运行 `dotnet test`（用本地 SDK `.\.dotnet\dotnet.exe`）：目录规范、资源完整性和默认角色断言会校验新角色是否接好。

## 已知边界

- Windows 的普通 shutdown/restart 参数无法可靠区分关机和重启，因此晚间未完成时两者使用相同提醒流程。
- Windows 仍可能显示自己的 shutdown blocker 界面，用户也可以选择强制结束程序。
- 物理断电、长按电源键和任务管理器强制结束不在处理范围内。
- 当前不处理跨午夜状态。
- 当前只按主屏幕工作区定位，不专门适配多显示器和全屏应用。
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

程序代码使用 [MIT License](LICENSE)。角色、美术和动画素材可能使用各自单独标注的授权；仓库中由外部素材转换而来的角色动画不因 MIT 许可证而改变其原有权利归属，再次公开分发或商用前应自行确认素材授权。
