using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using TKCode.Arc;
using ArcSelect = TKCode.Arc.Select;
using ChatView = PrismCodingAgent.AvaloniaShell.Views.ChatView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>
/// 聊天正文改用 Arc 的 Markdown 渲染，这里守护"可选择、可换行、不溢出"这几个仍然成立的视觉契约。
/// </summary>
[AvaloniaTestClass]
public sealed class ChatOutputVisualTests
{
    [AssemblyCleanup]
    public static Task CleanupAvalonia() => AvaloniaTestClassAttribute.Session.DisposeAsync().AsTask();

    [TestMethod]
    public void AssistantMessageShouldRenderThroughArcMarkdown()
    {
        using var chat = new ChatViewModel();
        chat.Messages.Add(new MessageItemViewModel(
            CopilotChatMessage.CreateAssistant("## 标题\n\n这是 **加粗** 与 `代码`。", isPresetInfo: false)));

        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 900, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();

            var markdown = view.GetVisualDescendants().OfType<Markdown>().FirstOrDefault();
            Assert.IsNotNull(markdown, "助手消息应当由 Arc 的 Markdown 控件渲染。");
            Assert.IsGreaterThan(0, markdown!.Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void MarkdownShouldNotEmitFocusBorder()
    {
        using var chat = new ChatViewModel();
        chat.Messages.Add(new MessageItemViewModel(
            CopilotChatMessage.CreateAssistant("普通回复", isPresetInfo: false)));

        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 900, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();

            // 正文不再是可聚焦的 TextBox，因此不会出现输入框的蓝色外框。
            var messages = view.FindControl<ItemsControl>("MessagesItemsControl")
                ?? throw new InvalidOperationException("未找到消息列表。");
            Assert.IsEmpty(messages.GetVisualDescendants().OfType<TextBox>().ToArray(),
                "消息区不应再出现 TextBox。");
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void ModelAndReasoningSelectorsShouldRemainAdjacent()
    {
        var chatView = new ChatView { DataContext = new ChatViewModel() };
        var window = new Window
        {
            Width = 1150,
            Height = 700,
            Content = chatView,
        };

        window.Show();
        window.UpdateLayout();

        ArcSelect modelSelector = chatView.FindControl<ArcSelect>("ModelSelector")
            ?? throw new InvalidOperationException("未找到模型选择器。");
        ArcSelect reasoningSelector = chatView.FindControl<ArcSelect>("ReasoningEffortSelector")
            ?? throw new InvalidOperationException("未找到思考强度选择器。");
        Point modelOrigin = modelSelector.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("无法获取模型选择器坐标。");
        Point reasoningOrigin = reasoningSelector.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("无法获取思考强度选择器坐标。");
        double horizontalGap = reasoningOrigin.X - (modelOrigin.X + modelSelector.Bounds.Width);

        Assert.AreEqual(10, horizontalGap, 0.5);

        window.Close();
    }

    [TestMethod]
    public void UserMessageShouldUseSurfaceMutedBubble()
    {
        using var chat = new ChatViewModel();
        chat.Messages.Add(new MessageItemViewModel(new CopilotChatMessage(ChatRole.User, "总结对话")));

        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = 900, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();

            Border bubble = view.GetVisualDescendants()
                .OfType<Border>()
                .Single(item => item.Classes.Contains("MessageBubbleUser"));
            Assert.AreNotEqual(Colors.Transparent, ((ISolidColorBrush)bubble.Background!).Color);
            Assert.IsGreaterThan(0, bubble.Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }
}
