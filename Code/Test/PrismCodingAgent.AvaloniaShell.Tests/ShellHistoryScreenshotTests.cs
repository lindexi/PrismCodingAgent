using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实主界面输入验证历史和存档的空状态导航，并保留各状态截图。</summary>
[AvaloniaTestClass]
public sealed class ShellHistoryScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task EmptyHistoryAndArchiveShouldNavigateThroughMouseInput()
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        Lang.SetCurrent("zh-Hans");
        try
        {
            await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
            session.Capture("main-model-setup");
            var main = session.Window.GetVisualDescendants().OfType<MainView>().Single();
            var setup = main.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(control => control.Name == "ModelSetupScrollViewer");
            var editor = setup.GetVisualDescendants().OfType<ModelSettingsView>().Single();
            var connection = editor.GetVisualDescendants().OfType<ModelServiceConnectionView>().Single();
            var catalog = editor.GetVisualDescendants().OfType<OpenAIModelCatalogView>().Single();

            void ScrollTo(Control control)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    session.Window.UpdateLayout();
                    Point position = control.TranslatePoint(default, setup)
                        ?? throw new InvalidOperationException("Setup control is not attached.");
                    if (position.Y >= 0 && position.Y + control.Bounds.Height <= setup.Bounds.Height)
                    {
                        return;
                    }

                    Point wheelPoint = setup.TranslatePoint(new Point(setup.Bounds.Width - 30,
                        setup.Bounds.Height / 2), session.Window)
                        ?? throw new InvalidOperationException("Setup scroll viewer is not attached.");
                    session.Window.MouseMove(wheelPoint);
                    session.Window.MouseWheel(wheelPoint, new Vector(0, position.Y < 0 ? 3 : -3));
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.Fail("Could not scroll the setup control into view using the mouse wheel.");
            }

            void ReplaceText(TextBox input, string value)
            {
                ScrollTo(input);
                session.Click(input);
                session.Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
                session.Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
                session.Window.KeyTextInput(value);
                Dispatcher.UIThread.RunJobs();
            }

            // 仅手动配置模型以解除聊天遮罩，不刷新远程模型或发送消息。
            ReplaceText(connection.GetVisualDescendants().OfType<TextBox>()
                .Single(input => input.PlaceholderText == "https://api.openai.com/v1"), "https://history-screenshot.invalid/v1");
            ReplaceText(connection.GetVisualDescendants().OfType<SecretInput>().Single(), "screenshot-only-key");
            ReplaceText(catalog.GetVisualDescendants().OfType<TextBox>().Single(), "history-offline-model");
            Button add = catalog.GetVisualDescendants().OfType<Button>()
                .Single(button => Equals(button.Content, Lang.Current.SetupAddModel));
            ScrollTo(add);
            session.Click(add);
            Button save = main.GetVisualDescendants().OfType<Button>()
                .Single(button => button.IsEffectivelyVisible && button.Classes.Contains("SaveSettings"));
            await session.ClickAndWaitAsync(save);
            session.Capture("chat-ready");
            Assert.IsTrue(session.Model.IsChatOpen);
            var chat = main.GetVisualDescendants().OfType<ChatView>().Single();
            Button taskHistory = chat.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "TaskHistoryButton");
            Assert.IsTrue(taskHistory.IsEffectivelyEnabled);

            Button BackButton() => main.GetVisualDescendants().OfType<Button>()
                .Single(button => button.IsEffectivelyVisible && Equals(button.Content, Lang.Current.BackToChatText));
            async Task WaitForHistoryAsync()
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (session.Model.SessionListViewModel.IsLoading)
                {
                    await Task.Delay(20, timeout.Token);
                    Dispatcher.UIThread.RunJobs();
                }
            }

            void AssertEmptyHistory(string expectedSearch)
            {
                Assert.IsTrue(session.Model.IsHistoryOpen);
                var history = main.GetVisualDescendants().OfType<SessionListView>().Single();
                Assert.IsTrue(history.IsEffectivelyVisible);
                Assert.IsTrue(history.GetVisualDescendants().OfType<TextBlock>()
                    .Single(block => block.Text == Lang.Current.EmptySessionListText).IsEffectivelyVisible);
                Assert.IsEmpty(session.Model.SessionListViewModel.Sessions);
                Assert.AreEqual(expectedSearch, history.GetVisualDescendants().OfType<TextBox>()
                    .Single(box => box.PlaceholderText == Lang.Current.HistorySearchText).Text);
            }

            await session.ClickAndWaitAsync(main.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "HistoryButton"));
            await WaitForHistoryAsync();
            session.Capture("all-history-initial-session");
            var historyView = main.GetVisualDescendants().OfType<SessionListView>().Single();
            session.Click(historyView.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("Danger") && button.IsEffectivelyVisible));
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                while (session.Model.SessionListViewModel.Sessions.Count != 0)
                {
                    await Task.Delay(20, timeout.Token);
                    Dispatcher.UIThread.RunJobs();
                }
            }
            session.Capture("all-history-empty");
            AssertEmptyHistory(string.Empty);
            session.Click(BackButton());
            session.Capture("chat-after-all-history");
            Assert.IsTrue(session.Model.IsChatOpen);
            Assert.IsTrue(chat.IsEffectivelyVisible);

            await session.ClickAndWaitAsync(taskHistory);
            await WaitForHistoryAsync();
            session.Capture("current-task-history-empty");
            AssertEmptyHistory(session.Model.ActiveWorkTask.Id.ToString());
            session.Click(BackButton());
            session.Capture("chat-after-task-history");
            Assert.IsTrue(session.Model.IsChatOpen);
            Assert.IsTrue(chat.IsEffectivelyVisible);

            session.Click(main.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "ArchiveButton"));
            session.Capture("archive-empty");
            Assert.IsTrue(session.Model.IsArchiveOpen);
            var archive = main.GetVisualDescendants().OfType<ArchivedWorkTasksView>().Single();
            Assert.IsTrue(archive.IsEffectivelyVisible);
            Assert.IsTrue(archive.GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.Text == Lang.Current.EmptyArchivedWorkTasksText).IsEffectivelyVisible);
            Assert.IsEmpty(session.Model.ArchivedWorkTasks);
            session.Click(BackButton());
            session.Capture("chat-after-archive");
            Assert.IsTrue(session.Model.IsChatOpen);
            Assert.IsTrue(chat.IsEffectivelyVisible);
        }
        finally
        {
            Lang.SetCurrent(originalLanguage);
        }
    }
}
