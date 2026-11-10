# 用途模型设置绑定契约

WPF/Avalonia 共用 `PrismCodingAgent.CodingShell.ViewModels`，不需要各自的用途 VM。

## SettingsViewModel

- `PurposeModels`：`ObservableCollection<PurposeModelSettingsViewModel>`。
- `AddPurposeModelCommand`：无参数，添加键为 `CustomPurpose` 的草稿，可直接编辑；重复时会显示错误。
- `RemovePurposeModelCommand`：参数为当前行，仅允许删除自定义用途。
- `SaveCommand`：沿用现有统一保存、应用及通知流程；存在行错误时不保存。

## 行绑定

- `PurposeKey`：自定义用途键，双向绑定输入框；PascalCase 示例，不限制用户已有自定义键格式。
- `DisplayName`：内置用途的本地化名称，只读显示。
- `IsBuiltIn`、`CanEditPurpose`、`CanRemove`：控制名称编辑与删除入口。
- `ModelOptions`：下拉 ItemsSource。
- `SelectedModel`：下拉 SelectedItem，双向绑定。
- `PurposeError`、`ModelError`：对应输入控件下方的就地错误，null 表示无错误。
- `HasErrors`：行错误状态。

下拉选项类型为 `PurposeModelOptionViewModel`，显示 `DisplayName`，另有 `ModelReference`、`IsAvailable`。必须允许显示当前不可用选项，以便用户识别并修正旧引用。`SelectedModel` 的未指定选项（或 null）不生成映射，不自动使用主模型。

所有选择、添加、删除、改名均为草稿。模型草稿被删除或改名时，旧选择不会被替换，显示为不可用并阻止保存；恢复相同完整模型引用可以解除错误。用途键按 Trim 后的 Ordinal 比较校验重复，与保存字典一致。

## 界面契约

- WPF/Avalonia 用途区必须嵌入首选模型所在卡片，在首选模型输入下方、服务模型编辑器上方，不单独创建卡片。
- `PurposeModelsExpander` 默认收起，标题复用用途模型文案；`HasPurposeErrors` 为真时标题显示现有校验提示，即使收起也能发现加载或保存时的用途错误。
- 展开后每项为用途名称及模型下拉的紧凑行，无重复用途键/模型标签、无常驻帮助。内置名称只读，自定义名称可编辑并保留删除。
- WrapPanel 每项首选432 DIP（含右间距12、下间距8），最大宽度绑定折叠区实际宽度；内容最大864 DIP。1120宽两项一行，720宽自动一项；更窄时整项随容器收缩，不以固定420 DIP撑出边界，也不把下拉拉满页面。内置项显示“用途名: [下拉框]”，名称列90 DIP，模型列使用剩余宽度。
- 长模型名省略并提供完整tooltip；保留控件键盘交互和自动化名称。新增入口位于展开内容底部。行错误就地显示，无错误不占空间。
- 所有编辑仍为草稿，校验与统一保存流程不变。加载旧配置中的 null 映射后，如仍未指定任何用途模型，保存时保留 null；新建草稿及显式空字典保持空映射。

## 布局回归验证

- Avalonia `PurposeModelControlTests` 与 WPF `WpfPurposeLayoutTests` 覆盖480、720、1120 DIP，验证默认折叠、展开后列数及下拉右边界。
- Avalonia 布局测试输出 `purpose-expanded-{width}.png` 并作为测试附件保存；1120与720展开截图已检查，两列/一列符合预期，未见用途区溢出或重叠。
- WPF 用途布局独立验证通过；现有 `WpfPrimaryModelLayoutTests` 截图流程仍可能因截图表面不可见或无尺寸失败，不应把该流程计为已通过。

## 本地化规范

Shell 用户可见的新文案必须放入 `Localizations/*.toml`，通过现有生成的 `Lang.Current` 使用，不新增 resx，也不使用 AgentLib 的 resx 作为 Shell 界面文本来源。新增键必须同步到所有语言，保持 `EnsureKeysIdentical = true`。

AgentLib 不得自行引入 resx、ResourceManager 或其他本地化机制。在基础库尚无既定本地化方案时，使用清晰的普通异常信息，不建立另一套本地化实现。

界面标签键：`PurposeModelsTitle`、`PurposeModelsHelp`、`PurposeAdd`、`PurposeRemove`、`PurposeKeyLabel`、`PurposeKeyHint`、`PurposeModelLabel`。内置用途名、未指定、不可用和错误消息也都使用 `Purpose*` TOML 键。非中英文语言当前使用英文占位翻译，后续翻译应保留键名及 `{0}` 占位参数。

用途键与模型引用是持久化标识，不能本地化。内置用途使用库中的 PascalCase 常量；下拉模型引用统一为 `Provider/ModelName`。
