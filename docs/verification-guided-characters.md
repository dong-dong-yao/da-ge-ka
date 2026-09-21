# 图文素材引导 preview.2 验证 · 2026-09-19

## 交付

- EXE：G:\打卡项目\release\v1.2.0-preview.2\DaGeKa-v1.2.0-preview.2-win-x64.exe
- win-x64、self-contained、single-file、PublishTrimmed=false。
- 250480802 字节。
- SHA256：C0C22E438D4AC281A6379C1F03D2EAB7379F1DE69E1A8F391FEFCFD53553A28D。
- 保留 preview.1 和 v1.1.0，未提交或发布到 GitHub。

## 改动

创建器内嵌四个素材制作阶段，使用用户提供的原始三视图示例、白熊动作参考和提示词。动作参考支持复制和保存，提示词支持一键复制；已有素材可折叠引导。

去除正常流程的背景算法、独立手臂上传、连接点和缩放设置。视频自动处理透明/绿幕/蓝幕。桌宠要求四张同画布、完整透明 PNG，不去除图片背景。方向说明与选项分组，独立方向默认折叠。

完整图按现有固定参考模板进行鼠标局部分层。启发式构图检查失败时显示键盘降级选项；即使通过检查，用户也可以在预览页选择鼠标效果不合适，返回改为仅键盘互动。旧的分层角色包仍支持。

## 验证

- 最终完整 Release 测试：249 通过，0 失败，0 跳过，3分19秒。
- 新增透明 PNG 验证、四个模板角色的鼠标响应、向导字段约束、错误构图拒绝及键盘模式测试。
- 媒体集成测试从原始嵌入姿势图准备透明测试素材（尚未提取鼠标层），经过 PrepareAsync → Commit → Load → Render，确认模板标记保留且键盘/鼠标响应；错误构图不留下半成品，键盘降级可保存重载；缺中间图明确拒绝。
- 实际 MP4 导入及向导截图：96 DPI 与当前系统 DPI，检查四页、教程两个标签、独立方向展开、完整图片上传区。
- 自包含单文件验收工具在 PATH 为空、DOTNET_ROOT 指向不存在目录时完成视频导入、界面渲染和桌宠加载。工具不启动提醒调度，不开启全局输入钩子。
- 只读独立复审未发现新的高信心重大缺陷，复审建议的完整导入和降级链路已补测试。
- git diff --check 通过。

证据目录：G:\打卡项目\release\verification-guided-characters

- tests/guided-final.trx
- system-dpi/creator-step-1-guide-2.png
- system-dpi/creator-step-3.png
- system-dpi/creator-pet-uploads.png
- system-dpi/creator-directions-expanded.png
- standalone/：独立单文件验收截图与样例素材

## 边界

未验证任意外部生成器产出的任意角色图；固定模板范围和颜色检查不能保证每次自动分层正确，必须在最后一步预览。绿色角色改用蓝幕视频。PNG 扩展名不等于真正透明。

未运行真实关机/重启，未更改用户的实际角色、提醒设置或开机启动。自动化截图不能替代真实桌面的键鼠手感和多显示器验收。旧版验证文档保留为 preview.1 历史记录，当前使用说明以 README 为准。
