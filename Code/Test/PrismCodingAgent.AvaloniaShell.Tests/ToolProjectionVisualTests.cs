using AgentLib.Model;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Converters;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;
using TKCode.Arc;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>
/// 工具调用、推理与子智能体现在由 Arc 的 ToolCall / ExpandableCard 承载，这里守护它们仍然按片段类型正确分派。
/// </summary>
[TestClass]
public sealed class ToolProjectionVisualTests
{
    [TestMethod]
    public async Task ToolItemShouldRenderThroughArcToolCall()
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            var tool = new CopilotChatToolItem("call", "read", "input", "returned");
            var view = new ChatView { Content = new ItemsControl { ItemsSource = new[] { tool } } };
            var window = new Window { Content = view };
            try
            {
                window.Show();
                window.UpdateLayout();

                ToolCall row = view.GetVisualDescendants().OfType<ToolCall>().Single();
                Assert.IsNotNull(row.Item);
                Assert.AreEqual("read", row.Item.ToolName);
                Assert.AreEqual(ToolCallStatus.Success, row.Item.Status);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task ToolProjectionShouldFollowSourceChanges()
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            var tool = new CopilotChatToolItem("call", "inspect", "input", "returned");
            ToolCallItem item = ToolCallProjection.Project(tool);
            Assert.AreEqual("input", item.Input.Single().Value);

            tool.Details = CopilotChatMessage.CreateAssistant("detail text", false);
            var view = new ChatView { Content = new ItemsControl { ItemsSource = new[] { tool } } };
            var window = new Window { Content = view };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.AreSame(item, ToolCallProjection.Project(tool), "同一片段必须复用同一个工具调用行，否则展开状态会丢失。");
                Assert.AreSame(tool, ToolCallProjection.SourceOf(item));
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task NestedDetailsShouldStillRenderInsideToolExpansion()
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            var view = new ChatView();
            var tool = new CopilotChatToolItem("call", "inspect", "input", "returned")
            {
                Details = CopilotChatMessage.CreateAssistant("detail text", false)
            };
            tool.Details.AppendReasoning("detail reasoning");
            tool.Details.AppendFunctionCall(new FunctionCallContent("inner", "read", new Dictionary<string, object?>()));
            var items = new ItemsControl { ItemsSource = new[] { tool } };
            view.Content = items;
            var window = new Window { Content = view };
            try
            {
                window.Show();
                window.UpdateLayout();

                // 外层工具一行，详情里嵌套的函数调用是第二行。
                Assert.IsGreaterThanOrEqualTo(2, items.GetVisualDescendants().OfType<ToolCall>().Count());
                Assert.IsGreaterThanOrEqualTo(1, items.GetVisualDescendants().OfType<ExpandableCard>().Count());
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task SubAgentShouldRenderNestedToolsAndSubAgents()
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            var subAgent = new CopilotChatSubAgentItem("call", "history", "input");
            var nested = new CopilotChatSubAgentItem("nested", "nested agent", "nested input");
            nested.MessageItems.Add(new CopilotChatTextItem("nested text"));
            subAgent.MessageItems.Add(nested);
            subAgent.MessageItems.Add(new CopilotChatToolItem("tool", "read", "tool input"));
            var view = new ChatView { Content = new ItemsControl { ItemsSource = new[] { subAgent } } };
            var window = new Window { Content = view };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.IsTrue(view.GetVisualDescendants().OfType<ExpandableCard>().Any());
                Assert.IsGreaterThanOrEqualTo(1, view.GetVisualDescendants().OfType<ToolCall>().Count());
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task ApprovalShouldMatchBeforeOrdinaryToolTemplate()
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            var view = new ChatView();
            var approval = new CopilotChatApprovalToolItem("approval", "write", "input");
            Control card = view.FindDataTemplate(approval)!.Build(approval)!;
            ToolCall toolCall = Assert.IsInstanceOfType<ToolCall>(card);
            ToolCallItem projection = ToolCallProjection.Project(approval);
            Assert.AreEqual(ToolCallStatus.Approval, projection.Status);
            Assert.IsNotNull(toolCall);
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task UserAndAssistantTextShouldKeepDifferentBubbles()
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            using var model = new ChatViewModel();
            model.Messages.Add(new MessageItemViewModel(new CopilotChatMessage(ChatRole.User, "user text")));
            model.Messages.Add(new MessageItemViewModel(new CopilotChatMessage(ChatRole.Assistant, "assistant text")));
            var view = new ChatView { DataContext = model };
            var window = new Window { Content = view, Width = 1000, Height = 800 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var bubbles = view.GetVisualDescendants().OfType<Border>()
                    .Where(border => border.Classes.Contains("MessageBubbleUser")
                                     || border.Classes.Contains("MessageBubbleAssistant"))
                    .ToArray();
                Assert.IsTrue(bubbles.Any(border => border.Classes.Contains("MessageBubbleUser")),
                    "用户消息应使用用户气泡。");
                Assert.IsTrue(bubbles.Any(border => border.Classes.Contains("MessageBubbleAssistant")),
                    "助手消息应使用助手气泡。");
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }
}
