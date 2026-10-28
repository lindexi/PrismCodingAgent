using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实输入编辑未发送草稿、切换本地选项并验证设置往返保留。</summary>
[AvaloniaTestClass]
public sealed class ShellComposerScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MultilineDraftAndLocalOptionsShouldSurviveSettingsNavigationThroughUserInput()
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        Lang.SetCurrent("zh-Hans");
        try
        {
            await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
            await session.ConfigureOfflineModelAsync();
            var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
            var chatModel = (PrismCodingAgent.AvaloniaShell.ViewModels.ChatViewModel)chat.DataContext!;
            var initialMessages = chatModel.Messages.ToArray();
            var input = chat.FindControl<TextBox>("MessageInputTextBox")!;
            var send = chat.FindControl<Button>("SendButton")!;
            var responses = chat.FindControl<ToggleButton>("ResponsesApiToggleButton")!;
            var compression = chat.FindControl<CheckBox>("AutomaticCompressionCheckBox")!;
            var loop = chat.FindControl<CheckBox>("LoopIterationCheckBox")!;
            var reasoning = chat.FindControl<ComboBox>("ReasoningEffortSelector")!;
            Assert.IsTrue(input.IsEffectivelyVisible);
            Assert.AreEqual(string.Empty, input.Text ?? string.Empty);
            Assert.IsFalse(send.IsEffectivelyEnabled);

            void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
            {
                session.Window.KeyPressQwerty(key, modifiers);
                session.Window.KeyReleaseQwerty(key, modifiers);
            }

            session.Click(input);
            session.Window.KeyTextInput("离线草稿第一行");
            Press(PhysicalKey.Enter);
            session.Window.KeyTextInput("第二行待修改x");
            session.Capture("multiline-draft");
            Assert.AreEqual("离线草稿第一行\n第二行待修改x", input.Text?.Replace("\r\n", "\n"));

            Press(PhysicalKey.Backspace);
            session.Window.KeyTextInput("，仅本地编辑");
            const string draft = "离线草稿第一行\n第二行待修改，仅本地编辑";
            session.Capture("draft-edited-with-keyboard");
            Assert.AreEqual(draft, input.Text?.Replace("\r\n", "\n"));
            Assert.IsTrue(send.IsEffectivelyEnabled);

            bool? originalResponses = responses.IsChecked;
            session.Click(responses);
            session.Capture("responses-toggled");
            Assert.AreEqual(!originalResponses.GetValueOrDefault(), responses.IsChecked);
            bool? originalCompression = compression.IsChecked;
            session.Click(compression);
            session.Capture("automatic-compression-toggled");
            Assert.AreEqual(!originalCompression.GetValueOrDefault(), compression.IsChecked);
            bool? originalLoop = loop.IsChecked;
            session.Click(loop);
            session.Capture("loop-toggled");
            Assert.AreEqual(!originalLoop.GetValueOrDefault(), loop.IsChecked);

            int originalReasoning = reasoning.SelectedIndex;
            Assert.IsGreaterThan(1, reasoning.ItemCount);
            session.Click(reasoning);
            Press(PhysicalKey.ArrowDown);
            Press(PhysicalKey.Enter);
            session.Capture("reasoning-selected");
            Assert.AreNotEqual(originalReasoning, reasoning.SelectedIndex);
            int selectedReasoning = reasoning.SelectedIndex;

            await session.ClickAndWaitAsync(session.Window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "SettingsButton"));
            session.Capture("settings-open-with-unsent-draft");
            var settings = session.Window.GetVisualDescendants().OfType<SettingsView>().Single();
            Assert.IsTrue(settings.IsEffectivelyVisible);
            session.Click(settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SettingsBack")));
            session.Capture("chat-return-draft-preserved");
            Assert.IsTrue(chat.IsEffectivelyVisible);
            Assert.AreEqual(draft, input.Text?.Replace("\r\n", "\n"));
            Assert.AreEqual(!originalResponses.GetValueOrDefault(), responses.IsChecked);
            Assert.AreEqual(!originalCompression.GetValueOrDefault(), compression.IsChecked);
            Assert.AreEqual(!originalLoop.GetValueOrDefault(), loop.IsChecked);
            Assert.AreEqual(selectedReasoning, reasoning.SelectedIndex);
            CollectionAssert.AreEqual(initialMessages, chatModel.Messages.ToArray());
            Assert.IsFalse(chatModel.IsRunning);
        }
        finally
        {
            Lang.SetCurrent(originalLanguage);
        }
    }
}
