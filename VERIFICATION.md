# 打个卡最终验收与维护说明

当前源码与本地试用版本：`1.2.0-preview.7`。收尾日期：2026-09-21。

## 当前交付

- GitHub `main` 保存角色创建、分步素材教程、鼠标手臂校准、点选鼠标垫、放大镜与创建界面修复。
- 完整程序位于本机 `release/v1.2.0-preview.7/DaGeKa-v1.2.0-preview.7-win-x64.exe`；Windows x64、自包含、单文件、禁止 trimming，包含离线视频组件。
- EXE SHA256：`C0EFD146CEB8AEB59808904301FD8BFE0A6BA65C89E7E1C22A3830538717FEC3`。
- GitHub Release 的正式下载与源码更新独立；本次没有创建新 Release。v1.1.0 正式版保留作回退。

## 验证结果

- 2026-09-21 Release 全量测试：263 通过、0 失败、0 跳过，耗时2分3秒，包含完整视频组件的媒体集成测试。
- 最终记录：主仓库 `release/verification-preview7/final-tests/final.trx` 和 `final-tests.log`。
- 创建向导探针覆盖四页截图、方向上传展开后无需调整窗口即可滚到底，以及40轮展开/收起/缩放。preview.6 同时检查正式系统 DPI 与96 DPI；preview.7 专门复查立即滚动。
- 鼠标回归覆盖连续灰垫识别、不同姿势垫面偏移、无效点击保护、肩部固定、键盘不动、分层存储重载及失败回滚。修复窗检查工具排列、底部操作栏与涂抹放大镜。
- 界面证据保留在 `release/verification-preview6/` 与 `release/verification-preview7/`，不提交截图或测试 EXE 到源码仓库。
- 原 v1.1.0 验收记录及开发阶段诊断可从提交 `0b0a098` 的 Git 历史查阅；阶段计划和重复文档不再作为现役说明。

## 复现与维护

以 [README](README.md) 为使用、素材要求和构建说明的权威入口；界面探针命令见 [探针说明](tools/SettingsUiProbe/README.md)。本机使用 `.\.dotnet\dotnet.exe`，其他机器需安装 `global.json` 指定的 SDK。

```powershell
dotnet test CheckInReminder.slnx -c Release
dotnet build CheckInReminder.slnx -c Release
```

源码不包含视频组件 ZIP，普通构建可以运行，媒体集成测试会明确跳过；完整发布前按 README 打包组件。GitHub Actions 只验证源码测试与构建，不代表完整 EXE 已发布。

## 保留边界

鼠标背景修补仅针对纯色、近似凸形灰垫，不恢复复杂花纹。四张图构图明显漂移时仍需重新制作图片；当前创建入口不支持旧角色就地编辑。

实际 Windows 全局输入钩子、鼠标穿透、开机启动、跨屏 DPI、锁屏恢复和关机拦截仍属于实机人工验收范围；没有运行无人值守的真实关机或重启。没有修改用户角色包、提醒设置或开机启动项。

开发阶段结束，后续按 GitHub Issue 反馈维护。本机有效源码仍在 `.worktrees/bongocat-rig`，根目录旧分支不作为现役源码。保留 SDK、原始素材、视频组件、宣传工程、正式版和最终试用版；构建缓存可按上述命令重新生成。
