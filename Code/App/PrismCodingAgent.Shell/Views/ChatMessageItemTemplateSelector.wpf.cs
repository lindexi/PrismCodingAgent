using System.Windows;
using System.Windows.Controls;
using AgentLib.Model;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>按消息片段类型选择布局。</summary>
public sealed class ChatMessageItemTemplateSelector : DataTemplateSelector
{
    /// <summary>文本模板。</summary>
    public DataTemplate? TextItemTemplate { get; set; }
    /// <summary>思考模板。</summary>
    public DataTemplate? ReasoningItemTemplate { get; set; }
    /// <summary>图片模板。</summary>
    public DataTemplate? ImageItemTemplate { get; set; }
    /// <summary>审批模板。</summary>
    public DataTemplate? ApprovalToolItemTemplate { get; set; }
    /// <summary>工具模板。</summary>
    public DataTemplate? ToolItemTemplate { get; set; }
    /// <summary>子代理模板。</summary>
    public DataTemplate? SubAgentItemTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        CopilotChatTextItem => TextItemTemplate,
        CopilotChatReasoningItem => ReasoningItemTemplate,
        CopilotChatImageItem => ImageItemTemplate,
        CopilotChatApprovalToolItem => ApprovalToolItemTemplate,
        CopilotChatToolItem => ToolItemTemplate,
        CopilotChatSubAgentItem => SubAgentItemTemplate,
        _ => null,
    };
}
