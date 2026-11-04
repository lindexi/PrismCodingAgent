using AgentLib.Model;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class ToolProjectionTests
{
    [TestMethod]
    public void MessageViewModelShouldExposeOnlyOriginalCollection()
    {
        var message = CopilotChatMessage.CreateAssistant(string.Empty, false);
        using var viewModel = new MessageItemViewModel(message);

        Assert.AreSame(message.MessageItems, viewModel.MessageItems);
    }
}
