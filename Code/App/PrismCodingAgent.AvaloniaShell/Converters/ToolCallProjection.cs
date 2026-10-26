using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AgentLib.Model;
using Avalonia.Data.Converters;
using Avalonia.Threading;
using TKCode.Arc;

namespace PrismCodingAgent.AvaloniaShell.Converters;

/// <summary>
/// 把 AgentLib 的聊天片段投影为 TKCode.Arc 的 <see cref="ToolCallItem"/>，让 <c>arc:ToolCall</c> 可以直接显示工具调用。
/// 投影结果按源对象缓存并跟随源对象的变化更新，因此行内展开状态不会因为重新绑定而丢失。
/// </summary>
public sealed class ToolCallProjection : IValueConverter
{
    private static readonly ConditionalWeakTable<object, ToolCallItem> s_items = new();
    private static readonly Dictionary<ToolCallItem, object> s_sources =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 供 XAML 绑定使用：把片段转换为 <see cref="ToolCallItem"/>。
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? null : Project(value);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>
    /// 取得或创建片段对应的工具调用行。
    /// </summary>
    public static ToolCallItem Project(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!s_items.TryGetValue(source, out ToolCallItem? item))
        {
            item = new ToolCallItem();
            s_items.Add(source, item);
            s_sources[item] = source;

            if (source is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += OnSourcePropertyChanged;
            }
        }

        Update(item, source);
        return item;
    }

    /// <summary>
    /// 由工具调用行反查原始片段，用于把审批事件交回视图模型。
    /// </summary>
    public static object? SourceOf(ToolCallItem? item)
        => item is not null && s_sources.TryGetValue(item, out object? source) ? source : null;

    private static void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is null)
        {
            return;
        }

        // 片段由后台线程推进流式内容，投影只在 UI 线程更新。
        Dispatcher.UIThread.Post(() =>
        {
            if (s_items.TryGetValue(sender, out ToolCallItem? item))
            {
                Update(item, sender);
            }
        }, DispatcherPriority.Background);
    }

    private static void Update(ToolCallItem item, object source)
    {
        switch (source)
        {
            case CopilotChatToolItem tool:
                UpdateFromTool(item, tool);
                break;
            case CopilotChatApprovalToolItem approval:
                UpdateFromApproval(item, approval);
                break;
            case CopilotChatSubAgentItem subAgent:
                UpdateFromSubAgent(item, subAgent);
                break;
        }
    }

    private static void UpdateFromTool(ToolCallItem item, CopilotChatToolItem tool)
    {
        item.Id = tool.CallId;
        item.ToolName = tool.DisplayName;
        item.Title = string.IsNullOrWhiteSpace(tool.PrimaryText) ? tool.DisplayName : tool.PrimaryText;
        item.Summary = tool.HasSecondaryText ? tool.SecondaryText : null;
        item.Message = tool.HasPrimaryText && tool.HasSecondaryText ? tool.SecondaryText : null;
        item.Status = ToolCallStatus.Success;
        item.ToolMark = ArcIcons.Wrench;

        SyncArguments(item, tool.HasInputText ? tool.InputText : null);
        item.Output = tool.HasOutputText ? new ToolCallTextOutput(tool.OutputText) : null;
    }

    private static void UpdateFromApproval(ToolCallItem item, CopilotChatApprovalToolItem approval)
    {
        item.Id = approval.CallId;
        item.ToolName = approval.DisplayName;
        item.Title = approval.DisplayName;
        item.ApprovalTitle = approval.ApprovalDescription ?? approval.DisplayName;
        item.Message = approval.HasApprovalDescription ? approval.ApprovalDescription : null;
        item.ToolMark = approval.ApprovalState == CopilotToolApprovalState.Pending
            ? ArcIcons.CircleHelp
            : ArcIcons.Wrench;
        item.Status = approval.ApprovalState switch
        {
            CopilotToolApprovalState.Pending => ToolCallStatus.Approval,
            CopilotToolApprovalState.Approved => ToolCallStatus.Success,
            _ => ToolCallStatus.Denied,
        };

        SyncArguments(item, approval.HasInputText ? approval.InputText : null);
        item.Output = approval.HasOutputText ? new ToolCallTextOutput(approval.OutputText) : null;
    }

    private static void UpdateFromSubAgent(ToolCallItem item, CopilotChatSubAgentItem subAgent)
    {
        item.Id = subAgent.CallId;
        item.ToolName = subAgent.DisplayName;
        item.Title = subAgent.DisplayName;
        item.Message = subAgent.HasInputText ? subAgent.InputText : null;
        item.Status = ToolCallStatus.Success;
        item.ToolMark = ArcIcons.Sparkles;

        SyncArguments(item, null);
        item.Output = subAgent.HasOutputText ? new ToolCallTextOutput(subAgent.OutputText) : null;
    }

    private static void SyncArguments(ToolCallItem item, string? inputText)
    {
        item.Input.Clear();
        if (!string.IsNullOrWhiteSpace(inputText))
        {
            item.Input.Add(new ToolCallArgument { Key = "input", Value = inputText });
        }
    }
}
