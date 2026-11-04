using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using ChatView = PrismCodingAgent.CodingShell.Views.ChatView;
using SettingsView = PrismCodingAgent.CodingShell.Views.SettingsView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实输入编辑未发送草稿、切换本地选项并验证设置往返保留。</summary>
[AvaloniaTestClass]
public sealed class ShellComposerScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>通过真实输入验证空草稿、多行草稿、选项选择和开关状态，不发送请求。</summary>
    [TestMethod]
    public async Task ComposerPreservesMultilineDraftAndRunOptionsThroughUserInputAsync()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(Lang.Current.IetfLanguageTag == "en" ? "en" : "zh-CN");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.CurrentCulture;
        const string configuration = """{"PrimaryModel":"Offline composer model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Offline composer model","ModelId":"offline-composer","Provider":"openai"},{"ModelName":"Alternate composer model","ModelId":"alternate-composer","Provider":"openai"}]}]}""";
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext, configuration);
        session.Window.Width = 1440;
        session.Window.Height = 900;
        session.Window.UpdateLayout();
        var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
        var model = (ChatViewModel)chat.DataContext!;
        void Press(PhysicalKey key)
        {
            session.Window.KeyPressQwerty(key, RawInputModifiers.None);
            session.Window.KeyReleaseQwerty(key, RawInputModifiers.None);
        }
        foreach (string name in new[] { "ReasoningEffortSelector", "AbilitySelector" })
        {
            var selector = chat.FindControl<ComboBox>(name)!;
            session.Click(selector);
            Press(PhysicalKey.Home);
            Press(PhysicalKey.Enter);
            Assert.AreEqual(0, selector.SelectedIndex);
        }
        foreach (string name in new[] { "LoopIterationCheckBox", "AutomaticCompressionCheckBox", "EnableDotNetRunCheckBox", "ResponsesApiToggleButton" })
        {
            var toggle = chat.FindControl<ToggleButton>(name)!;
            if (toggle.IsChecked == true) session.Click(toggle);
            Assert.IsFalse(toggle.IsChecked.GetValueOrDefault());
        }
        Assert.AreEqual(1440d, session.Window.ClientSize.Width);
        Assert.AreEqual(900d, session.Window.ClientSize.Height);
        Assert.AreEqual(1d, session.Window.RenderScaling);
        Assert.AreEqual(0, model.PendingImages.Count);
        Assert.IsNotNull(model.SelectedModel);
        TestContext.WriteLine($"Composer state: language={Lang.Current.IetfLanguageTag}; client=1440x900; dpi=96; model={model.SelectedModel.DisplayName}; reasoning={model.SelectedReasoningEffort?.DisplayName}; ability={model.SelectedAbility?.Id}; switches=false,false,false,false; attachments=0");
        var input = chat.FindControl<TextBox>("MessageInputTextBox")!;
        session.Click(input);
        Assert.AreEqual(string.Empty, input.Text ?? string.Empty);
        session.Capture("composer-empty-draft");
        session.Window.KeyTextInput("Offline composer draft");
        Press(PhysicalKey.Enter);
        session.Window.KeyTextInput("Second line - no request");
        Assert.AreEqual("Offline composer draft\nSecond line - no request", input.Text?.Replace("\r\n", "\n"));
        Assert.AreEqual(0, model.PendingImages.Count);
        session.Capture("composer-multiline-draft");
        var ability = chat.FindControl<ComboBox>("AbilitySelector")!;
        session.Click(ability);
        Assert.IsTrue(ability.IsDropDownOpen);
        foreach (var button in chat.GetVisualDescendants().OfType<Button>().Where(button => button.Name is "ApplyWorkspaceButton" or "SendButton" or "AddImageButton" or "TaskHistoryButton"))
            TestContext.WriteLine($"Composer button {button.Name}: background={button.Background}; foreground={button.Foreground}; enabled={button.IsEnabled}");
        foreach (var item in ability.GetRealizedContainers().OfType<ComboBoxItem>())
        {
            TestContext.WriteLine($"Ability dropdown item: bounds={item.Bounds}; padding={item.Padding}; background={item.Background}; foreground={item.Foreground}; selected={item.IsSelected}; hover={item.IsPointerOver}; focus={item.IsFocused}");
            foreach (var surface in item.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
                TestContext.WriteLine($"Ability dropdown item surface {surface.Name}: background={surface.Background}; opacity={surface.Opacity}; border={surface.BorderBrush}; radius={surface.CornerRadius}");
        }
        TestContext.WriteLine($"Composer culture={System.Globalization.CultureInfo.CurrentUICulture.Name}; abilities={string.Join("|", model.AvailableAbilities.Select(item => item.DisplayName))}");
        foreach (var border in session.Window.GetVisualDescendants().OfType<Border>().Where(border => border.Background is not null && border.TemplatedParent is Button))
            TestContext.WriteLine($"Composer button surface {((Button)border.TemplatedParent!).Name}: {border.Background}");
        foreach (string key in new[] { "SystemAccentColor", "ComboBoxItemBackgroundSelected", "ComboBoxItemBackgroundSelectedPointerOver", "ComboBoxPopupBackground", "ComboBoxPopupBorderBrush" })
            if (Avalonia.Application.Current!.TryGetResource(key, session.Window.ActualThemeVariant, out object? value)) TestContext.WriteLine($"Composer resource {key}={value}");
        session.Capture("composer-ability-dropdown-open");
        Press(PhysicalKey.End);
        Press(PhysicalKey.Enter);
        Assert.IsFalse(ability.IsDropDownOpen);
        Assert.AreEqual(ability.ItemCount - 1, ability.SelectedIndex);
        session.Capture("composer-ability-selected");
        session.Click(ability);
        Press(PhysicalKey.Home);
        Press(PhysicalKey.Enter);
        Assert.AreEqual(0, ability.SelectedIndex);
        foreach (string name in new[] { "ModelSelector", "ReasoningEffortSelector" })
        {
            var selector = chat.FindControl<ComboBox>(name)!;
            session.Click(selector);
            Assert.IsTrue(selector.IsDropDownOpen);
            Press(PhysicalKey.Home);
            session.Capture($"{name}-focus");
            var last = selector.GetRealizedContainers().OfType<ComboBoxItem>().Last();
            var popupRoot = TopLevel.GetTopLevel(last)
                ?? throw new InvalidOperationException("Composer selector popup root is missing.");
            var point = last.TranslatePoint(new Avalonia.Point(last.Bounds.Width / 2, last.Bounds.Height / 2), popupRoot)
                ?? throw new InvalidOperationException("Composer selector popup item is detached.");
            popupRoot.MouseMove(point);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(last.IsPointerOver);
            session.Capture($"{name}-hover-anchor");
            popupRoot.UpdateLayout();
            using (var frame = popupRoot.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("Composer selector popup frame is missing."))
            {
                string path = Path.Combine(session.DirectoryPath, $"{name}-hover-root.png");
                frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                TestContext.AddResultFile(path);
                TestContext.WriteLine($"Composer selector popup root={popupRoot.GetType().Name}; origin={popupRoot.PointToScreen(default)}; size={popupRoot.ClientSize}; selector={selector.PointToScreen(default)}; item={last.PointToScreen(default)}; selected={last.IsSelected}; focus={last.IsFocused}; hover={last.IsPointerOver}; image={path}");
            }
            Press(PhysicalKey.End);
            Press(PhysicalKey.Enter);
            Assert.IsFalse(selector.IsDropDownOpen);
            Assert.AreEqual(selector.ItemCount - 1, selector.SelectedIndex);
            session.Capture($"{name}-selected");
        }
        foreach (string name in new[] { "LoopIterationCheckBox", "AutomaticCompressionCheckBox", "EnableDotNetRunCheckBox", "ResponsesApiToggleButton" })
        {
            var toggle = chat.FindControl<ToggleButton>(name)!;
            session.Click(toggle);
            Assert.IsTrue(toggle.IsChecked.GetValueOrDefault());
            Assert.IsTrue(toggle.IsPointerOver);
            session.Capture($"{name}-checked-hover");
        }
        session.Click(input);
        session.Capture("composer-all-options-checked");
    }

    [TestMethod]
    public async Task MultilineDraftAndLocalOptionsShouldSurviveSettingsNavigationThroughUserInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        await session.ConfigureOfflineModelAsync();
        var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
        var chatModel = (ChatViewModel)chat.DataContext!;
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
}
