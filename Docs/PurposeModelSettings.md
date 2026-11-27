# 用途模型设置契约

WPF 与 Avalonia 共用 `PrismCodingAgent.CodingShell.ViewModels`，具有相同的草稿、校验、持久化和本地化语义，不需要各自实现用途 VM。

## 功能边界

`ModelsByPurpose` 以用途为键，每项同时保存模型引用和思考强度。未指定模型时仍可保存强度；只有用途模型成功解析时才应用该强度，回退行为由调用方决定。

## 共享绑定与校验

- `SettingsViewModel.PurposeModels`：用途草稿行集合，始终列出内置用途。
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

用途名称按 Trim 后的 Ordinal 比较校验重复，保存时同样 Trim。模型和思考强度均未设置时不生成用途条目。

所有选择、添加、删除及改名均为草稿；切换语言只刷新显示名称、下拉提示与校验消息，不翻译或修改标识、用户名称及模型引用。VM 释放时解除语言通知订阅。

## 两套界面共同要求

用途区位于首选模型所在卡片内，首选模型输入下方、服务模型编辑器上方。默认折叠，错误在折叠标题中仍可发现。展开后以紧凑的用途名称、模型和思考强度下拉行呈现；内置名称只读，自定义名称可编辑并可删除。新增入口在列表底部，错误就地显示，无错误不占空间。

两套界面均应使用共享 VM 与 `Lang.Current`，不得在视图中维护另一套用途翻译或映射。长模型名应提供完整提示，保留键盘操作与自动化名称；窄窗口不应溢出。具体布局和截图由各 UI 的布局测试验证，本契约不把历史截图记录作为当前测试通过证明。

## 标识与 TOML 规范

Shell 用户可见文案放在 `Localizations/*.toml`，通过现有源生成器的 `Lang.Current` 使用，不新增 resx 或基础库本地化机制。`Lang.cs` 保持 `EnsureKeysIdentical = true`，所有语言必须同步资源键及参数占位符。

支持语言共 12 种：`zh-Hans`、`en`、`zh-Hant`、`de`、`es`、`fr`、`it`、`ja`、`ko`、`nl`、`pt-BR`、`ru`。共享 `SettingsViewModel.LanguageTags` 直接使用生成的 `Lang.SupportedLanguageTags`，两套设置视图绑定同一入口，全部语言已可选择。`LanguageTag` 是草稿，保存成功后才应用到 `Lang.Current`，并写入 Shell 设置；重建设置服务和 VM 可恢复保存值。

共享 `ShellLanguage` 统一规范标签大小写；已保存的 null、空串、空白或不支持标签回退 `zh-Hans`。首次使用或旧配置未包含语言字段时，按系统 UI Culture 及其父 Culture 查找支持语言，找不到则回退中文。启动在创建任务显示文案前读取并应用语言。这里的“恢复”测试覆盖落盘和启动读取入口，不等同于两应用完整进程重启验收。

- `Purpose*` 包含标题、增删、控件标签、状态、校验文案及内置用途名。
- 使用 UTF-8 TOML 字符串；转义双引号、反斜杠和换行须符合 TOML 语法。键名不翻译，不能重复；新键必须同步到全部语言。
- `PurposeUnavailable` 必须保留 `{0}`，参数为原始模型引用；不得删除、改号或翻译参数内容。
- placeholder 使用自然语言“用途名称”，不展示技术键示例。错误中的空白、重复、不可用及“未指定”用语与控件一致。
- 不保留英文占位翻译；不同语言中拼写相同的合法词语可以相同。
- 内置稳定 PascalCase 标识 `DocumentGeneration`、`ImageAnalysis`、`UserCommunication`、`Reasoning`、`CodeDesign`、`CodeReview`、`CodeExploration`、`SessionTitle`、`HistoryCompression` 不本地化，仅翻译其显示名称。
- `Provider/ModelName` 和用户自定义名称原样保留，不随界面语言转换。

## 回归测试

`PurposeLocalizationTests` 嵌入原始 TOML，逐语言验证全资源键一致、用途占位符一致、空翻译及英文占位残留、技术名称泄漏；通过真实 `Lang.SetCurrent` 验证既有用途 VM 的显示名称、未指定、不可用、空白和重复校验刷新，以及标识不变和跨语言持久化。

`PurposeModelSettingsTests` / `SettingsModelsByPurposeTests` 覆盖空名称新增、草稿隔离、删除、无效引用、保存重载和 null/空映射。`PurposeModelControlTests` 及 WPF 对应测试分别负责控件绑定与布局。运行 Avalonia 相关测试时会重新编译 Shell 并执行本地化生成器，验证 TOML 语法与 `EnsureKeysIdentical`。

