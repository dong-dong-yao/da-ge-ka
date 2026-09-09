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
- 角色动画从允许的屏幕边缘随机出现，保持素材原始约 7.1 秒节奏，气泡提供“已打卡，闭嘴”按钮。白熊、黄色河马和蓝帽小猫支持上、下、左、右；持棒小狗和滑板绿恐龙只从左、右出现。
- 设置窗口采用左侧导航双页面：「设置」页可自由缩放，较小高度下可滚动访问全部内容；缩放或滚动时会暂缓角色预览并合并绘制，结束交互后自动恢复动画。「角色」页用网格卡片浏览全部角色，悬停卡片即可预览动画，确认后设置页同步显示当前角色。当前内置白熊、黄色河马、蓝帽小猫、持棒小狗和滑板绿恐龙，角色目录可继续扩展。
- 可选桌面宠物：开启后角色常驻桌面角落（默认右下角），透明置顶、鼠标穿透不挡操作。五个角色的鼠标手都会跟随指针并在点击时下压；白熊使用独立手臂图层，其余四个角色在用户提供的完整图片上做局部手臂形变，不带动身体和键盘。键盘方面，黄色河马与蓝帽小猫支持左、中、右三种姿势，持棒小狗与滑板绿恐龙使用左、右两种姿势（中区映射到右姿势）。短按也会显示，长按保持触键，松开后立即恢复待机图。托盘菜单可切换「调整宠物位置」模式拖动换位。默认关闭；在设置页打开开关后必须点击「保存设置」才会生效，也可从托盘菜单直接开启。
- 桌面宠物通过全局低级键盘和鼠标钩子获取瞬时输入状态。键盘事件仅在内存中瞬时读取 Windows virtual-key identity，只用于确定键盘手的位置；释放时立即清除该键的按下标记和次序，不保留已释放的键身份。通知时已映射的归一化目标坐标可短暂保留，用于一次可见的短按反馈，随后平滑回落。程序不会把键值转换为输入文字，也不会写入日志、保存、上传或传输。鼠标仅保留当前指针坐标、左右键按下状态及单次点击反馈位。程序本身不联网。
- 若安全软件拦截输入钩子安装，宠物会保留待机画面，托盘显示包含 Win32 错误码的不可点击状态，并在本次程序运行中弹出一次气泡提示；关闭后重新开启「桌面宠物」会重试安装，不影响其他提醒功能。若白熊原图已成功加载但动作切层失败，则保留透明静态原图并停止实时绘制。
- 晚间第二道确认显示“真的吗，那你为什么不走”，提供“真的 / 假的”两个选项；约 4.09 秒动画播完后从第一帧重新循环。
- 可配置打卡时间、提醒间隔、是否开机自启动和是否常驻桌面宠物。
- 可选久坐提醒，默认关闭；默认 `09:00–18:00`，可选每 `1 / 1.5 / 2 / 3` 小时提醒。久坐气泡没有按钮，只会自动消失。
- 打卡提醒优先；冲突时久坐提醒最多排队一次，未来提醒仍使用原固定时间节点。
- Windows 普通关机或重启时，在晚间尚未确认的情况下进行 best-effort 提醒。
- 单实例运行，不联网，不记录打卡历史。

## 一起把“打个卡”变得更可爱

打个卡已经有五个内置角色，也欢迎更多原创角色加入。

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

### 桌面宠物人工验证

桌面宠物依赖 Windows 全局输入钩子、透明分层窗口和单实例机制，这些行为需要在目标 Windows 10 或 Windows 11 电脑上人工确认。验证新版本前，请按顺序执行：

1. 从托盘菜单退出正在运行的旧版「打个卡」，确认旧进程已经结束。
2. 启动新的 `打个卡.exe`，打开设置页，开启「桌面宠物」，然后点击「保存设置」。只切换开关而不保存不会应用设置。
3. 移动鼠标，确认鼠标侧手臂会跟随指针；分别按下鼠标左键和右键，确认点击动作可见。
4. 打开 Windows 记事本，分别短按和长按 Q、G、P，确认右手切换左、中、右三种触键姿势，肩部不随下压移动，松开后回弹并恢复原待机抬手，同时记事本输入不受影响。
5. 在托盘菜单关闭「调整宠物位置」，确认宠物处于鼠标穿透状态，能够点击它后方的窗口；再开启该模式，确认可以拖动宠物换位。
6. 若托盘显示「输入监听不可用（错误 …）」并出现一次气泡提示，检查安全软件是否拦截了全局钩子；关闭后重新开启「桌面宠物」以重试。原图加载成功但动作切层失败时也会保留静态画面。提醒、设置和托盘仍可使用。

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
3. （可选）完整桌宠姿势图放入 `CheckInReminder/Assets/DesktopPet/Characters/{角色Id}/`：必须包含 `idle.png`（也可用 JPG）、`press-left.png` 和 `press-right.png`，可选 `press-center.png`。只有左右两张按键图时，中区自动映射到右图。旧式逐帧素材仍可使用 `{角色Id}-pet-idle/` 与 `{角色Id}-pet-tap/`；两种素材都没有时自动降级为占位显示。
4. 在 `CheckInReminder/AnimationCatalog.cs` 的 `BuildCharacters()` 里注册一行：`new("shiba", "柴犬", "shiba", TimeSpan.FromSeconds(时长), false)`。`SourceEdge` 要填写原视频实际从哪条边出现（默认右侧）；若角色只能从左右出现，再设置 `AllowedEdges = [ScreenEdge.Left, ScreenEdge.Right]`。
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

[第三方许可声明](THIRD_PARTY_NOTICES.md)已嵌入单文件 EXE，可从托盘菜单「第三方许可」打开只读窗口查看全文，无需额外许可文件。
