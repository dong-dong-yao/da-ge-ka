> 历史阶段记录：可能包含已被后续版本替代的界面、限制和路径。当前使用与验收以 [README](../README.md) 和 [VERIFICATION](../VERIFICATION.md) 为准。

# preview.7：方向上传展开后的滚动修复

只修复创建向导内容尺寸更新后的滚动范围。`ResizePage` 原先在修改页面高度后使用 `ResumeLayout(false)`，导致宿主保留旧滚动范围，直到调整窗口大小才重算。改为恢复并立即执行宿主布局，保留重入保护。

新增 `CreatorScrollTests`：不调整窗口，连续三次展开、滚到底、收起，检查滚动范围、最下面上传区域可达及收起后页面仍可见。修复前在展开范围断言失败，修复后通过。与 `GuidedCharacterTests` 共5项针对性测试通过。

实际窗口探针增加无需resize的滚到底检查，并继续执行40轮展开/缩放及四页截图检查，全部通过。证据 `G:\打卡项目\release\verification-preview7\directions-bottom-without-resize.png`。

Release发布成功（win-x64、自包含、单文件、关闭trimming）。程序：`G:\打卡项目\release\v1.2.0-preview.7\DaGeKa-v1.2.0-preview.7-win-x64.exe`。SHA256：`C0EFD146CEB8AEB59808904301FD8BFE0A6BA65C89E7E1C22A3830538717FEC3`。

2026-09-21 收尾复验：Release 全量测试 263 项通过、0 失败、0 跳过，耗时 2 分 3 秒；包含完整视频组件的媒体集成测试。TRX 与日志保留在主仓库 `release/verification-preview7/final-tests/` 和 `final-tests.log`。Windows 实际全局钩子、跨屏 DPI、开机启动和关机拦截仍需实机人工验收，未运行真实关机或重启。
