using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>只实例化当前时间线条目所需的外层布局。</summary>
public sealed class ChatEntryTemplateSelector : IDataTemplate
{
    /// <summary>用户消息模板。</summary>
    public IDataTemplate? UserMessageTemplate { get; set; }
    /// <summary>助手消息模板。</summary>
    public IDataTemplate? AssistantMessageTemplate { get; set; }
    /// <summary>协议系统消息模板。</summary>
    public IDataTemplate? SystemMessageTemplate { get; set; }
    /// <summary>应用通知模板。</summary>
    public IDataTemplate? NoticeTemplate { get; set; }
    /// <summary>按条目语义选择模板。</summary>
    public IDataTemplate? SelectTemplate(object? item) => item switch
    {
        NoticeItemViewModel => NoticeTemplate,
        MessageItemViewModel { IsUserMessage: true } => UserMessageTemplate,
        MessageItemViewModel { IsAssistantMessage: true } => AssistantMessageTemplate,
        MessageItemViewModel => SystemMessageTemplate,
        _ => null,
    };
    /// <inheritdoc />
    public Control? Build(object? param) => SelectTemplate(param)?.Build(param);
    /// <inheritdoc />
    public bool Match(object? data) => data is ChatEntryViewModel;
}
