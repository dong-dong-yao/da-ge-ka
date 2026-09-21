# 打个卡 v1.1.0 历史验收记录

此文只记录 v1.1.0。当前 v1.2.0-preview.7 的试用验证见 [preview.7 记录](docs/verification-creator-preview7.md)，功能与构建说明以 [README](README.md) 为准。

日期：2026-09-12

## 当前版本

- 发布源码：GitHub `main`；版本下载见 [v1.1.0 Release](https://github.com/dong-dong-yao/da-ge-ka/releases/tag/v1.1.0)。
- 角色画廊采用充分入场的封面、固定取景范围和自适应卡片；设置页采用紧凑双栏、统一米白卡片及正确的开关首帧状态。
- 修复 150% 缩放下时间胶囊过大：窗口与外壳统一缩放，输入高 34 DIP、宽不超过 138 DIP。
- 包含桌宠鼠标联动、肩部连接和桌沿合成修复；提醒文案、时间锚点与十二项设置持久化语义保持既有约定。

## 已验证

- 本地 SDK 10.0.400，Release 全量测试 **229/229 通过**。覆盖时间计算、窗口、角色预览、桌宠渲染、设置保存和输入尺寸。
- 独立 `tools/SettingsUiProbe` 调用正式程序集的启动配置：本机 144 DPI（150%）输入高 51 像素，独立 96 DPI 模式输入高 34 像素，两次均通过尺寸检查。普通测试进程的 96 DPI 截图不作为 150% 的验证证据。
- 已检查 860×600、980×700、1280×900 客户区布局；正式截图见 [设置页](docs/media/settings-150dpi.png) 与 [角色页](docs/media/characters.png)。独立代码审查未发现重要回归。
- 已成功发布 Windows x64 自包含单文件 EXE，`PublishTrimmed=false`，程序版本 1.1.0。
- 文件：`DaGeKa-v1.1.0-win-x64.exe`；SHA256：`33C347110CF9F0ABDC3749DB9B0F4B62F9F98083FA3E954CE9B65B20A177AFE6`。

复现命令（本机以 `.\.dotnet\dotnet.exe` 替换 `dotnet`）：

```powershell
dotnet test CheckInReminder.slnx -c Release
dotnet run --project tools/SettingsUiProbe/SettingsUiProbe.csproj -c Release -- artifacts/settings-dashboard/native-after
dotnet run --project tools/SettingsUiProbe/SettingsUiProbe.csproj -c Release -- artifacts/settings-dashboard/native-100 --unaware
dotnet publish CheckInReminder/CheckInReminder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

GitHub 自动构建的最新结果以 [Actions](https://github.com/dong-dong-yao/da-ge-ka/actions/workflows/build.yml) 为准。本地最终 TRX、测试日志及 DPI 测量保存在主仓库 `release/verification-v1.1.0/`，可执行文件位于 `release/v1.1.0/`；这些产物不提交到源码仓库。

## 清理与保留

- 退役计划与重复阶段记录归入 Git 历史，删除无引用的 `AspectRatioPanel`；构建缓存和临时截图可按上述命令重新生成。
- 保留现役工作树、SDK、原始素材、宣传视频、许可、正式截图、验证工具和旧试用版。
- 本机根目录仍是旧分支，开发入口为 `.worktrees/bongocat-rig`；不要把工作树当作缓存删除。

## 待人工验证

Windows 10/11 的实际托盘操作、全局输入钩子、鼠标穿透与拖动、开机启动、锁屏/恢复、焦点行为、跨显示器 DPI 切换和真实关机拦截仍需实机验收。项目禁止无人值守执行真实关机或重启测试。
