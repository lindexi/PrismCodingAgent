using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>按时间线条目语义选择外层布局。</summary>
public sealed class ChatEntryTemplateSelector : DataTemplateSelector
{
    /// <summary>用户消息模板。</summary>
    public DataTemplate? UserMessageTemplate { get; set; }
    /// <summary>助手消息模板。</summary>
    public DataTemplate? AssistantMessageTemplate { get; set; }
    /// <summary>协议系统消息模板。</summary>
    public DataTemplate? SystemMessageTemplate { get; set; }
    /// <summary>应用通知模板。</summary>
    public DataTemplate? NoticeTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        NoticeItemViewModel => NoticeTemplate,
        MessageItemViewModel { IsUserMessage: true } => UserMessageTemplate,
        MessageItemViewModel { IsAssistantMessage: true } => AssistantMessageTemplate,
        MessageItemViewModel => SystemMessageTemplate,
        _ => null,
    };
}
