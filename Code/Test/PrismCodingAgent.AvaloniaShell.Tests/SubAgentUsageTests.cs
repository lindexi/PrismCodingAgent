using AgentLib.Model;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class SubAgentUsageTests
{
    [TestMethod]
    public void ToolCardShowsChildModelAndUpdatesCumulativeUsage()
    {
        var child = CopilotChatMessage.CreateAssistant(string.Empty, false);
        var parent = CopilotChatMessage.CreateAssistant(string.Empty, false);
        parent.MessageItems.Add(new CopilotChatToolItem("call", "InvokeCodingSubAgent", "task",
            presentation: new ToolCallPresentation("worker", "child-model")) { Details = child });
        using var projection = new MessageItemViewModel(parent);
        var view = new ChatView();
        var content = new ContentControl
        {
            Content = projection,
            ContentTemplate = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate,
        };
        view.Content = content;
        var window = new Window { Width = 800, Height = 440, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var expander = content.GetVisualDescendants().OfType<Expander>().Single();
            Assert.IsTrue(expander.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == "child-model"));
            expander.IsExpanded = true;
            window.UpdateLayout();
            var summary = expander.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "ToolUsageSummary");
            Assert.IsFalse(summary.IsEffectivelyVisible);

            child.AppendUsageDetails(new UsageDetails { InputTokenCount = 7, OutputTokenCount = 3 });
            child.AppendUsageDetails(new UsageDetails { InputTokenCount = 5, OutputTokenCount = 2 });
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.AreEqual((true, $"{Lang.Current.UsageInput.ToString(12)} | {Lang.Current.UsageOutput.ToString(5)}"),
                (summary.IsEffectivelyVisible, summary.Text));
        }
        finally
        {
            window.Close();
        }
    }
}
