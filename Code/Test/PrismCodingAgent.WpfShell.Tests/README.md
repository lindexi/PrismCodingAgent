# WPF 界面测试基础设施

## 当前范围

本项目使用 net10.0-windows、WPF、MSTest 4.0.2，引用 WPF Shell，不引用 Avalonia Shell，也不包含具体测试方法。未移植业务界面。下方清单为后续实现目标，不代表已覆盖。

## 运行环境

- 在 Windows 上运行，需要可用的交互桌面；WPF 没有使用 Avalonia.Headless。
- `[WpfTestClass]` 将 MSTest 完整生命周期调度到共享 STA 线程，线程创建 WPF Application 并运行 Dispatcher 消息循环。
- 禁止测试并行；按 Lang.SupportedLanguageTags 分别运行中文和英文，保留参数行显示名并追加语言，结束恢复语言和 Culture。
- AssemblyCleanup 从测试线程关闭 Application，结束 Dispatcher 并等待 STA 线程退出。
- 当前 Application 仅提供调度环境，不初始化生产业务运行时；后续用例负责初始化实际需要的应用资源、窗口和独立数据服务。

## 截图支持

`WpfScreenshotSession` 接收真实 Window 和 TestContext；用例负责在 UI 线程创建并显示窗口、初始化真实运行时，并使用 AppDataDirectory 隔离数据。

`CaptureAsync` 等待 Dispatcher 的 ApplicationIdle 调度、更新布局，使用 RenderTargetBitmap 按实际 DPI 渲染窗口内容，并用 PngBitmapEncoder 保存 PNG。截图不包含原生标题栏、系统菜单及独立 Popup 窗口；这些表面需要后续专项支持，不能被计为已截图。

输出目录：`ShellScreenshots/<测试方法名>/<语言>/<GUID>/`。图片按步骤编号，注册为 TestContext 附件；AppData 和图片均保留，不自动删除。截图用于人工查看，不是像素基线测试。

截图基础设施不模拟鼠标键盘，不把直接调用命令或修改 ViewModel 当成真实用户输入。后续交互测试需要单独增加对应驱动。

构建：`dotnet build Code/Test/PrismCodingAgent.WpfShell.Tests/PrismCodingAgent.WpfShell.Tests.csproj`

后续有测试用例后运行：`dotnet test Code/Test/PrismCodingAgent.WpfShell.Tests/PrismCodingAgent.WpfShell.Tests.csproj`

## 待实现截图用例清单

清单来自 Avalonia Shell 测试 README、ShellScreenshotSession 和现有截图测试；目前均未在 WPF 实现。

| Avalonia 参考类 | 后续 WPF 场景 |
|---|---|
| ShellScreenshotTests | 主窗口、无模型引导、通过鼠标进入设置 |
| ShellWorkTaskScreenshotTests | 创建任务、切换任务、搜索命中、无匹配、清空恢复 |
| ShellModelSettingsScreenshotTests | 编辑服务连接、手动添加模型、编辑显示名、保存、删除模型、恢复无模型状态 |
| ShellExitConfirmationScreenshotTests | 系统关闭入口、鼠标取消、Escape 取消、确认退出 |
| ShellHistoryScreenshotTests | 启动会话列表、搜索无匹配、清空恢复、任务历史、存档空状态、返回聊天 |
| ShellArchiveScreenshotTests | 菜单归档、存档列表、搜索命中与无匹配、清空、恢复任务 |
| ShellLanguageScreenshotTests | 切换另一语言、保存、主界面往返、切回初始语言 |
| ShellOptionsScreenshotTests | 代理默认禁用、启用与编辑、保存、重进保留、恢复默认 |
| ShellComposerScreenshotTests | 配置离线模型、多行草稿、本地选项切换、设置往返保留 |
| ShellTaskMenuScreenshotTests | 菜单重命名、保存、置顶、列表顺序 |

## 后续用例约定

- 使用真实 WPF 界面、运行时与服务，不用空白模板截图代表业务界面覆盖。
- 用户操作通过实际输入完成，关键步骤截图，断言导航状态和持久化结果。
- 每个用例独立数据目录，不读取个人生产配置，不发起在线模型请求。
- 等待完成信号或带截止时间的状态变化，不用固定休眠判断完成。
- 模型发现、在线聊天、工具输出、图片原生选择器、沙盒连接和保存故障不属于上述截图清单的已有覆盖。
- Responses 在线压缩测试不纳入本次基础设施验证。
