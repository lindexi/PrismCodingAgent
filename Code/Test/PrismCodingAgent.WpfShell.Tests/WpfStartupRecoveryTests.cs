using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfStartupRecoveryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RealStartupRestoresTaskConfigurationAndMultipleSessions() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = await CreateRootAsync();
        string workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"}]}]}""");
        await File.WriteAllTextAsync(Path.Combine(root, "WorkTasks.json"), JsonSerializer.Serialize(new[]
        {
            new { Id = first, DisplayName = "Configured task", WorkspacePath = workspace, ModelReference = "test/demo", ReasoningEffort = ReasoningEffort.High, IsArchived = false },
            new { Id = second, DisplayName = "Independent task", WorkspacePath = workspace, ModelReference = "test/demo", ReasoningEffort = ReasoningEffort.Low, IsArchived = false },
        }));
        var store = new AgentLib.Logging.FileCopilotChatSessionStore(Path.Combine(root, "Sessions"));
        var sessions = new List<CopilotChatSession>();
        foreach (var (taskId, title) in new[] { (first, "First conversation"), (first, "Second conversation"), (second, "Other task conversation") })
        {
            var session = new CopilotChatSession
            {
                WorkTaskId = taskId,
                WorkTaskName = taskId == first ? "Configured task" : "Independent task",
            };
            session.SetTitle(title);
            await session.AddEntryAsync(new CopilotChatMessage(ChatRole.User, title + " question"));
            await session.AddEntryAsync(new CopilotChatMessage(ChatRole.Assistant, title + " answer"));
            await store.SaveSessionAsync(session, null);
            sessions.Add(session);
        }
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        try
        {
            CollectionAssert.AreEqual(new[] { first, second }, model.WorkTasks.Select(task => task.Id).ToArray());
            foreach (var task in model.WorkTasks)
            {
                Assert.AreEqual(workspace, task.Chat.NextRunWorkspacePath);
                Assert.AreEqual("test/demo", task.Chat.SelectedModel?.DisplayName);
                Assert.AreEqual(task.Id == first ? ReasoningEffort.High : ReasoningEffort.Low, task.Chat.SelectedReasoningEffort?.Value);
                model.ActivateWorkTaskCommand.Execute(task);
                await task.Sessions.LoadAsync();
                Assert.IsNull(task.Sessions.ErrorMessage);
                var current = task.Sessions.SelectedSession;
                Assert.IsNotNull(current);
                Assert.IsFalse(sessions.Any(session => session.SessionId == current.SessionId));
                Assert.AreEqual(task.Id, current.WorkTaskId);
                Assert.AreEqual(task.DisplayName, current.WorkTaskName);
                Assert.AreEqual(0, current.MessageCount);
                Assert.IsTrue(task.Chat.ShowWelcome);
                Assert.AreEqual(current.SessionId, task.Chat.CurrentSessionId);
                CollectionAssert.AreEquivalent(
                    sessions.Select(session => session.SessionId).Append(current.SessionId).ToArray(),
                    task.Sessions.Sessions.Select(item => item.SessionId).ToArray());
                foreach (var session in sessions)
                {
                    var item = task.Sessions.Sessions.Single(item => item.SessionId == session.SessionId);
                    Assert.AreEqual(session.WorkTaskId, item.WorkTaskId);
                    Assert.AreEqual(session.WorkTaskName, item.WorkTaskName);
                    Assert.AreEqual(session.Title, item.Title);
                    Assert.IsNull(item.MessageCount);
                    model.OpenSessionCommand.Execute(item);
                    await WpfInput.WaitAsync(() => task.Chat.Messages.Count == 2
                        && task.Chat.Messages[0].Content == session.Title + " question"
                        && model.OpenSessionCommand.CanExecute(item), "Stored session open operation did not complete.");
                    CollectionAssert.AreEqual(new[] { session.Title + " question", session.Title + " answer" }, task.Chat.Messages.Select(message => message.Content).ToArray());
                    Assert.AreEqual(session.SessionId, task.Sessions.SelectedSession?.SessionId);
                    Assert.IsTrue(item.IsCurrent);
                    Assert.IsFalse(task.Sessions.Sessions.Any(other => other.SessionId != session.SessionId && other.IsCurrent));
                    Assert.AreEqual(task.Id, item.WorkTaskId);
                    Assert.AreEqual(task.DisplayName, item.WorkTaskName);
                    Assert.AreEqual(session.SessionId, task.Chat.CurrentSessionId);
                    Assert.AreEqual(task.Id, model.ActiveWorkTask?.Id);
                }
            }
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("multiple-sessions-restored");
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "phase3-multiple-recovery-evidence.txt"),
                $"language={Lang.Current.IetfLanguageTag}; root={root}; tasks=2; sessions=3; workspaceModelReasoningVerified=True; contentsVerified=True; taskIsolationVerified=True{Environment.NewLine}");
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Recovery window did not close.");
            await model.DisposeAsync();
        }
    });

    private static async Task<string> CreateRootAsync()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "StartupData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        return root;
    }

}
