using AgentLib;
using AgentLib.Coding;
using AgentLib.Logging.Sessions;
using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.Services.Chat;
using PrismCodingAgent.CodingShell.Services.Sessions;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("zh-Hans")]
public sealed class SendAutoScrollTests
{
    [TestMethod]
    public async Task SendingFromHistoryShouldScrollToBottomAndAllowScrollingUpAgain()
    {
        var manager = new CopilotChatManager();
        for (int i = 0; i < 35; i++)
            await manager.AppendMessageAsync(new CopilotChatMessage(ChatRole.User, $"History {i}\nsecond line"));
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(manager, new MemoryStore(), new StoppedRunner());
        using var model = new ChatViewModel(manager, controller, "test");
        var view = new ChatView { DataContext = model };
        var window = new Window { Width = 900, Height = 700, Content = view };
        try
        {
            window.Show();
            Drain(window);
            var scroll = view.FindControl<ScrollViewer>("MessagesScrollViewer")!;
            scroll.Offset = Vector.Zero;
            Drain(window);
            Assert.IsGreaterThan(100d, scroll.Extent.Height - scroll.Viewport.Height);
            model.InputText = "question";
            model.SendCommand.Execute(null);
            Drain(window);
            Assert.AreEqual(scroll.Extent.Height - scroll.Viewport.Height, scroll.Offset.Y, 1d);
            scroll.Offset = Vector.Zero;
            Drain(window);
            await manager.AppendMessageAsync(new CopilotChatMessage(ChatRole.Assistant, "new output"));
            Drain(window);
            Assert.AreEqual(0d, scroll.Offset.Y, 1d);
        }
        finally { window.Close(); }
    }

    private static void Drain(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class StoppedRunner : ICodingChatRunner
    {
        public Task<ICodingAgentRunResult> RunAsync(IReadOnlyList<AIContent> contents,
            string? workspacePath, CodingChatRunOptions options, CancellationToken cancellationToken)
            => Task.FromCanceled<ICodingAgentRunResult>(new CancellationToken(true));
    }

    private sealed class MemoryStore : ICodingChatSessionStore
    {
        public Task<IReadOnlyList<CopilotChatSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotChatSessionSummary>>([]);
        public Task<CopilotChatSession> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task SaveSessionAsync(CopilotChatSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
