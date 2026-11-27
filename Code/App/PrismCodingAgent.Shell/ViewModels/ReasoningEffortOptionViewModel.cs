using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.ViewModels;

/// <summary>
/// 表示可选择的思考强度。
/// </summary>
public sealed class ReasoningEffortOptionViewModel : ViewModelBase
{
    private string _displayName;

    /// <summary>初始化思考强度选项。</summary>
    public ReasoningEffortOptionViewModel(string displayName, ReasoningEffort? value)
    {
        _displayName = displayName;
        Value = value;
    }

    /// <summary>获取当前语言下的显示名称。</summary>
    public string DisplayName
    {
        get => _displayName;
        internal set => SetField(ref _displayName, value);
    }

    /// <summary>获取发送请求使用的思考强度。</summary>
    public ReasoningEffort? Value { get; }

    internal static ReasoningEffortOptionViewModel[] CreateOptions() =>
    [
        new(GetDisplayName(null), null),
        new(GetDisplayName(ReasoningEffort.Low), ReasoningEffort.Low),
        new(GetDisplayName(ReasoningEffort.Medium), ReasoningEffort.Medium),
        new(GetDisplayName(ReasoningEffort.High), ReasoningEffort.High),
    ];

    internal void RefreshDisplayName() => DisplayName = GetDisplayName(Value);

    private static string GetDisplayName(ReasoningEffort? effort) => effort switch
    {
        ReasoningEffort.Low => Lang.Current.ReasoningLow,
        ReasoningEffort.Medium => Lang.Current.ReasoningMedium,
        ReasoningEffort.High => Lang.Current.ReasoningHigh,
        _ => Lang.Current.ReasoningDefault,
    };
}
