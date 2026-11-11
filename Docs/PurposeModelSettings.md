# 用途模型设置契约

WPF 与 Avalonia 共用 `PrismCodingAgent.CodingShell.ViewModels`，具有相同的草稿、校验、持久化和本地化语义，不需要各自实现用途 VM。

## 功能边界

用途设置目前用于保存配置。具体功能接入用途模型解析后才能使用对应配置；保存成功不代表文档生成、图片分析、推理等功能已经按用途切换模型，自定义用途也不会自动创建功能。

“未指定”表示不写入该用途映射，不承诺运行时必定使用主模型。实际模型解析、回退和能力限制由接入的具体功能决定，本设置页不改变 AgentLib 的解析语义。

## 共享绑定与校验

- `SettingsViewModel.PurposeModels`：用途草稿行集合，始终列出九个内置用途。
- `AddPurposeModelCommand`：添加空名称草稿；输入框使用本地化的用途名称 placeholder，空名立即显示校验错误，不生成默认技术名称。
- `RemovePurposeModelCommand`：参数为当前行，仅允许删除自定义用途。
- `SaveCommand`：统一保存入口；空白名称、重复名称或不可用模型阻止保存，不覆盖已保存配置。
- `HasPurposeErrors`：供折叠标题显示用途校验提示。

行绑定：

- `PurposeKey`：自定义用途名称的双向输入；内置标识不可编辑。用户名称可以使用自然语言，不强制 PascalCase。
- `DisplayName`：内置用途的本地化显示名称，或用户自行输入的名称。
- `IsBuiltIn`、`CanEditPurpose`、`CanRemove`：控制只读、编辑和删除入口。
- `ModelOptions` / `SelectedModel`：模型下拉集合及双向选择。
- `PurposeError` / `ModelError` / `HasErrors`：就地校验消息及错误状态。

下拉项 `PurposeModelOptionViewModel` 显示 `DisplayName`，以 `ModelReference` 保存完整 `Provider/ModelName` 引用，`IsAvailable` 表示可用性。保留失效旧引用并显示本地化“不可用”提示，不静默替换选择。模型删除或改名会阻止保存，恢复相同完整引用可解除错误。

用途名称按 Trim 后的 Ordinal 比较校验重复，保存时同样 Trim。选择“未指定”（或 null）不生成映射。加载旧配置的 null 映射后，若仍无用途选择则保留 null；新草稿或显式空字典保持空映射。未选择模型的合法自定义名称不单独持久化。

所有选择、添加、删除及改名均为草稿；切换语言只刷新显示名称、下拉提示与校验消息，不翻译或修改标识、用户名称及模型引用。VM 释放时解除语言通知订阅。

## 两套界面共同要求

用途区位于首选模型所在卡片内，首选模型输入下方、服务模型编辑器上方。默认折叠，错误在折叠标题中仍可发现。展开后以紧凑的用途名称与模型下拉行呈现；内置名称只读，自定义名称可编辑并可删除。新增入口在列表底部，错误就地显示，无错误不占空间。

两套界面均应使用共享 VM 与 `Lang.Current`，不得在视图中维护另一套用途翻译或映射。长模型名应提供完整提示，保留键盘操作与自动化名称；窄窗口不应溢出。具体布局和截图由各 UI 的布局测试验证，本契约不把历史截图记录作为当前测试通过证明。

## 标识与 TOML 规范

Shell 用户可见文案放在 `Localizations/*.toml`，通过现有源生成器的 `Lang.Current` 使用，不新增 resx 或基础库本地化机制。`Lang.cs` 保持 `EnsureKeysIdentical = true`，所有语言必须同步资源键及参数占位符。

支持语言共 12 种：`zh-Hans`、`en`、`zh-Hant`、`de`、`es`、`fr`、`it`、`ja`、`ko`、`nl`、`pt-BR`、`ru`。共享 `SettingsViewModel.LanguageTags` 直接使用生成的 `Lang.SupportedLanguageTags`，两套设置视图绑定同一入口，全部语言已可选择。`LanguageTag` 是草稿，保存成功后才应用到 `Lang.Current`，并写入 Shell 设置；重建设置服务和 VM 可恢复保存值。

共享 `ShellLanguage` 统一规范标签大小写；已保存的 null、空串、空白或不支持标签回退 `zh-Hans`。首次使用或旧配置未包含语言字段时，按系统 UI Culture 及其父 Culture 查找支持语言，找不到则回退中文。启动在创建任务显示文案前读取并应用语言。这里的“恢复”测试覆盖落盘和启动读取入口，不等同于两应用完整进程重启验收。

- `Purpose*` 共 22 键，包含标题、帮助、增删、名称标签与 placeholder、模型标签、未指定、不可用、三类校验与九个内置用途名。
- 使用 UTF-8 TOML 字符串；转义双引号、反斜杠和换行须符合 TOML 语法。键名不翻译，不能重复；新键必须同步到全部语言。
- `PurposeUnavailable` 必须保留 `{0}`，参数为原始模型引用；不得删除、改号或翻译参数内容。
- placeholder 使用自然语言“用途名称”，不展示技术键示例。错误中的空白、重复、不可用及“未指定”用语与控件一致。
- 不保留英文占位翻译；不同语言中拼写相同的合法词语可以相同。
- 内置稳定 PascalCase 标识 `DocumentGeneration`、`ImageAnalysis`、`UserCommunication`、`Reasoning`、`CodeDesign`、`CodeReview`、`CodeExploration`、`SessionTitle`、`HistoryCompression` 不本地化，仅翻译其显示名称。
- `Provider/ModelName` 和用户自定义名称原样保留，不随界面语言转换。

帮助统一表达“配置仅保存；具体功能接入后使用”，不得宣称保存即启用全部用途，也不得宣称未指定必定回退主模型。

## 回归测试

`PurposeLocalizationTests` 嵌入原始 TOML，逐语言验证全资源键一致、用途占位符一致、空翻译及英文占位残留、技术名称泄漏；通过真实 `Lang.SetCurrent` 验证既有用途 VM 的显示名称、未指定、不可用、空白和重复校验刷新，以及标识不变和跨语言持久化。

`PurposeModelSettingsTests` / `SettingsModelsByPurposeTests` 覆盖空名称新增、草稿隔离、删除、无效引用、保存重载和 null/空映射。`PurposeModelControlTests` 及 WPF 对应测试分别负责控件绑定与布局。运行 Avalonia 相关测试时会重新编译 Shell 并执行本地化生成器，验证 TOML 语法与 `EnsureKeysIdentical`。

## 本轮收尾验证与产品化状态

本轮仅修改 Avalonia 测试及本文档，没有修改产品代码、WPF 视图或多语言资源；现有共享实现未发现需要修正的问题。修正了旧测试把已支持的 `fr-FR` 仍预期为中文的问题，并补充繁体中文、巴西葡语和不支持的系统语言检测。`LanguageSettingsTests` 对全部 12 种语言检查真实保存 JSON、保存后当前语言、全新服务的启动读取及全新 VM 加载；旧 null/空白/无效/不支持值检查 VM 和启动读取回退，另检查大小写规范化。

本机实测结果：

- Avalonia headless 定向集合：`LanguageSettingsTests`、`LocalizationSettingsTests`、`PurposeLocalizationTests`、`PurposeModelSettingsTests`、`SettingsModelsByPurposeTests`、`LocalizationTests`、`ChatOptionLocalizationTests`、`PurposeModelControlTests`，**408 通过、0 失败、0 跳过**。TRX：`Code/Test/PrismCodingAgent.AvaloniaShell.Tests/TestResults/lindexi_DESKTOP-51A5UGG_2026-11-10_23_17_39_net10.0.trx`。
- 用途刷新覆盖全部 12 种语言的内置名称、未指定、不可用提示、空白/重复/失效模型校验，以及用户名称和模型引用保持不变。控件与布局验证使用 Avalonia headless，不创建原生桌面窗口。
- Avalonia 与 WPF 应用各自 Debug 构建成功，均 **0 警告、0 错误**。测试项目首次编译仍有 MSTest 分析器提示（如既有 `DataTestMethod` 过时及断言建议）；不将应用构建零警告表述为测试项目零警告。

验证边界：本机禁止运行任何 WPF 测试、WPF 应用或 WPF 截图流程，本轮未执行这些流程。WPF 运行、交互、截图及完整重启验收另由远程 Windows 沙盒执行；本轮没有新的远程 WPF 执行结果，不能据共享 headless 测试或编译成功宣称 WPF UI 验收通过。没有运行全仓测试、真实模型调用或验证所有语言的人工翻译质量；用途配置仍仅保存，具体功能的运行时接入不在本轮范围。
