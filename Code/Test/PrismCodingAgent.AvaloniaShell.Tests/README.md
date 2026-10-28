# 上层无头界面截图测试

## 运行与查看

运行本项目中名称包含 `ScreenshotTests` 的测试即可执行当前截图测试集，例如使用 `dotnet test --filter FullyQualifiedName~ScreenshotTests`，目标选择本目录的 csproj。

测试沿用 `AvaloniaTestClassAttribute` 的共享无头会话：`UseSkia()` 和 `UseHeadlessDrawing = false` 均已启用。使用项目现有 Avalonia.Headless 12.1.0，不改变生产启动入口。

每次运行的图片与独立应用数据保存于测试程序集输出目录：

`ShellScreenshots/<测试方法名>/<独立运行GUID>/`

图片按 `01-步骤名称.png` 编号，绝对路径写入 TestContext，并通过 AddResultFile 注册为测试结果附件。目录下 AppData 是该用例真实的设置、会话及任务数据，不读取个人生产配置。结果不自动删除，便于回看。

调查界面时可直接用图片读取或分析工具打开 PNG。截图不是像素基线测试；断言只检查基本导航、状态和落盘结果，视觉检查仍应主动阅读图片。无头模式不模拟 Windows 原生合成器的 Mica 视觉效果。

## 已实现用例

| 测试类 | 用户路径及分支 |
|---|---|
| ShellScreenshotTests | 真实主窗口、无模型引导、进入设置 |
| ShellWorkTaskScreenshotTests | 创建任务、切换、搜索命中、无匹配、清空恢复 |
| ShellModelSettingsScreenshotTests | 编辑连接、手动添加模型、编辑显示名、保存、移除模型、恢复无模型状态 |
| ShellExitConfirmationScreenshotTests | 系统关闭入口、鼠标取消、Escape 取消、鼠标确认退出 |
| ShellHistoryScreenshotTests | 启动会话列表、搜索无匹配、清空恢复、任务历史、存档空状态、返回聊天 |
| ShellArchiveScreenshotTests | 菜单归档、存档列表、搜索命中/无匹配/清空、恢复任务 |
| ShellLanguageScreenshotTests | 简中改英语、保存生效、主界面往返、改回简中 |
| ShellOptionsScreenshotTests | 代理默认禁用、启用编辑、保存、重进保留、恢复默认 |
| ShellComposerScreenshotTests | 配置离线模型、多行草稿编辑、本地选项切换、设置往返保留 |
| ShellTaskMenuScreenshotTests | 菜单重命名、保存、置顶、列表顺序 |

## 语言隔离

`AvaloniaTestClassAttribute` 在共享会话的 UI 线程内、执行 MSTest 的 `inner.ExecuteAsync` 前，将 `Lang.Current`、`CultureInfo.CurrentCulture` 和 `CultureInfo.CurrentUICulture` 统一设为 `zh-Hans`。测试初始化、执行和清理结束后，即使发生异常，也会在 `finally` 恢复入口保存的外部状态；原有 dispatch 和程序集 cleanup 的线程调度保持不变。

普通 UI/截图测试不再自行保存、初始化或恢复语言，也不会默认按中英双语重复运行。控件优先通过 Name、类、命令引用或 `Lang.Current` 资源定位；默认中文文案断言和真实文本输入可以保留。

专门语言用例负责触发并验证业务语言切换，例如 `ShellLanguageScreenshotTests` 通过设置页选择语言、保存并验证界面和持久化结果；初始语言与最终恢复仍由框架负责。非 UI 的 `LocalizationSettingsTests` 独立管理其用于验证系统语言默认值的 Culture 生命周期。当前没有同一截图用例按多语言重复执行，故不增加语言输出目录层级；语言切换场景在步骤名称中标注语言。

## 编写约定

- 使用真实 MainWindow、运行时、磁盘服务及界面控件，不以 Fake/Mock 或设计期主视图模型替代应用。
- 设置测试初始环境可以直接调用初始化方法；用户操作使用无头鼠标、键盘，不直接 Execute 命令或赋值业务视图模型来代替交互。
- 复用 ShellScreenshotSession，关键操作后调用 Capture，可随时增加调查步骤截图。
- 异步操作等待完成信号或有明确截止时间的状态变化，不以固定休眠猜测完成时间。
- 一个业务场景一个用例；新增用例串行实施，读码审查并独立复跑后再接受。

## 覆盖边界

这组测试覆盖可离线执行的主要页面与上述交互分支，不等于所有功能分支已覆盖。模型发现、真实聊天/工具输出及重试、图片系统选择器、沙盒连接、保存故障等未纳入这组真实输入截图场景；现有其他测试不能被算作这些上层截图场景的替代证明。配置离线模型只解除引导遮罩，不发送请求。
