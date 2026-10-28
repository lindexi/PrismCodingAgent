using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Infrastructure;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过主界面的真实输入保存模型配置，再删除模型并恢复无模型状态。</summary>
[AvaloniaTestClass]
public sealed class ShellModelSettingsScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ModelSettingsShouldSaveAndRemoveModelThroughUserInput()
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        Lang.SetCurrent("zh-Hans");
        try
        {
            await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
            session.Capture("main-without-model");
            Assert.IsEmpty(session.Model.ChatViewModel.AvailableModels);

            Button navigation = session.Window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "SettingsButton");
            await session.ClickAndWaitAsync(navigation);
            session.Capture("settings-opened");
            var settings = session.Window.GetVisualDescendants().OfType<SettingsView>().Single();
            var editor = settings.GetVisualDescendants().OfType<ModelSettingsView>().Single();
            var scroll = settings.GetVisualDescendants().OfType<ScrollViewer>().First();
            var connection = editor.GetVisualDescendants().OfType<ModelServiceConnectionView>().Single();
            var catalog = editor.GetVisualDescendants().OfType<OpenAIModelCatalogView>().Single();
            var save = settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SaveSettings"));
            var back = settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SettingsBack"));

            void ScrollTo(Control control)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    session.Window.UpdateLayout();
                    Point position = control.TranslatePoint(default, scroll)
                        ?? throw new InvalidOperationException("Settings control is not attached.");
                    if (position.Y >= 0 && position.Y + control.Bounds.Height <= scroll.Bounds.Height)
                    {
                        return;
                    }

                    Point wheelPoint = scroll.TranslatePoint(new Point(scroll.Bounds.Width - 30,
                        scroll.Bounds.Height / 2), session.Window)
                        ?? throw new InvalidOperationException("Settings scroll viewer is not attached.");
                    session.Window.MouseMove(wheelPoint);
                    session.Window.MouseWheel(wheelPoint, new Vector(0, position.Y < 0 ? 3 : -3));
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.Fail("Could not scroll the settings control into view using the mouse wheel.");
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

            ReplaceText(connection.GetVisualDescendants().OfType<TextBox>()
                .Single(input => input.PlaceholderText == "https://api.openai.com/v1"), "https://model-settings.invalid/v1");
            ReplaceText(connection.GetVisualDescendants().OfType<SecretInput>().Single(), "screenshot-only-key");
            session.Capture("service-connection-edited");

            const string modelId = "screenshot-offline-model";
            ReplaceText(catalog.GetVisualDescendants().OfType<TextBox>().Single(), modelId);
            Button add = catalog.GetVisualDescendants().OfType<Button>()
                .Single(button => Equals(button.Content, Lang.Current.SetupAddModel));
            ScrollTo(add);
            session.Click(add);
            session.Capture("model-added");
            var modelCard = editor.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Classes.Contains("ModelCard"));
            ReplaceText(modelCard.GetVisualDescendants().OfType<TextBox>()
                .Single(input => input.Parent is StackPanel panel && panel.Children.OfType<TextBlock>()
                    .Any(label => label.Text == Lang.Current.ModelDisplayNameText)), "Screenshot Model");
            session.Capture("model-edited");

            await session.ClickAndWaitAsync(save);
            session.Capture("model-saved");
            Assert.HasCount(1, session.Model.ChatViewModel.AvailableModels);
            var paths = CodingChatRoomPaths.Create(Path.Combine(session.DirectoryPath, "AppData"));
            StringAssert.Contains(await File.ReadAllTextAsync(paths.ConfigurationFile.FullName), modelId);
            session.Click(back);
            session.Capture("chat-with-model");

            await session.ClickAndWaitAsync(navigation);
            Button remove = editor.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("IconDanger"));
            ScrollTo(remove);
            session.Click(remove);
            session.Capture("model-removed");
            await session.ClickAndWaitAsync(save);
            session.Capture("empty-settings-saved");
            Assert.IsFalse((await File.ReadAllTextAsync(paths.ConfigurationFile.FullName)).Contains(modelId, StringComparison.Ordinal));
            session.Click(back);
            session.Capture("main-without-model-restored");
            Assert.IsEmpty(session.Model.ChatViewModel.AvailableModels);
            Assert.IsTrue(session.Window.GetVisualDescendants().OfType<TextBlock>()
                .Any(block => block.Text == Lang.Current.ModelSetupRequiredText && block.IsEffectivelyVisible));
        }
        finally
        {
            Lang.SetCurrent(originalLanguage);
        }
    }
}
