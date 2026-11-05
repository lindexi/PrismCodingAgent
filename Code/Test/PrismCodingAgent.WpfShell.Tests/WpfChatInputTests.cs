using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfChatInputTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>通过真实输入验证空草稿、多行草稿、选项选择和开关状态，不发送请求。</summary>
    [TestMethod]
    public async Task ComposerPreservesMultilineDraftAndRunOptionsThroughUserInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(Lang.Current.IetfLanguageTag == "en" ? "en" : "zh-CN");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.CurrentCulture;
        string root = Path.Combine(AppContext.BaseDirectory, "ComposerInputData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"Offline composer model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Offline composer model","ModelId":"offline-composer","Provider":"openai"},{"ModelName":"Alternate composer model","ModelId":"alternate-composer","Provider":"openai"}]}]}""");
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var content = (FrameworkElement)window.Content;
        window.UpdateLayout();
        window.Width += 1440 - content.ActualWidth;
        window.Height += 900 - content.ActualHeight;
        await window.Dispatcher.InvokeAsync(content.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var chat = (ChatView)window.FindName("ChatComposer");
        var model = ((MainViewModel)window.DataContext).ChatViewModel;
        foreach (string name in new[] { "ReasoningEffortSelector", "AbilitySelector" })
        {
            var selector = (ComboBox)(name == "AbilitySelector" ? chat.FindName(name) : window.FindName(name));
            await WpfInput.ClickAsync(selector);
            WpfInput.Key(0x24);
            WpfInput.Key(0x0D);
            await WpfInput.WaitAsync(() => selector.SelectedIndex == 0, "Composer option selection failed.");
        }
        var loop = (CheckBox)chat.FindName("LoopIterationCheckBox");
        if (loop.IsChecked == true) await WpfInput.ClickAsync(loop);
        Assert.IsFalse(loop.IsChecked.GetValueOrDefault());
        await WpfInput.ClickAsync((Button)chat.FindName("OpenChatSettingsButton"));
        await WpfInput.WaitAsync(() => model.IsChatSettingsOpen, "Chat settings did not open.");
        var settings = (ChatSettingsView)window.FindName("ChatSettingsPanel");
        foreach (string name in new[] { "ChatAutomaticCompressionToggle", "ChatDotNetRunToggle", "ChatResponsesApiToggle" })
        {
            var toggle = (CheckBox)settings.FindName(name);
            if (toggle.IsChecked == true) await WpfInput.ClickAsync(toggle);
            Assert.IsFalse(toggle.IsChecked.GetValueOrDefault());
        }
        await WpfInput.ClickAsync((Button)settings.FindName("CloseChatSettingsButton"));
        await WpfInput.WaitAsync(() => !model.IsChatSettingsOpen, "Chat settings did not close.");
        Assert.AreEqual(1440d, content.ActualWidth);
        Assert.AreEqual(900d, content.ActualHeight);
        Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchX);
        Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchY);
        Assert.IsEmpty(model.PendingImages);
        var card = (Border)chat.FindName("ComposerPanel");
        Assert.IsTrue(card.ActualHeight >= 110 && card.ActualHeight <= 140, $"Composer card height: {card.ActualHeight}");
        double cardTop = card.TranslatePoint(new Point(), content).Y;
        var status = (TextBlock)window.FindName("ConversationStatus");
        Assert.AreEqual((string)Lang.Current.ModelText, status.Text);
        Assert.AreEqual(model.StatusText, status.ToolTip);
        Assert.IsTrue(status.IsVisible && status.TranslatePoint(new Point(), content).Y < 150, "Model and run feedback must stay in the top configuration panel.");
        Assert.IsNull(window.FindName("ReadOnlyConversationNotice"));
        Assert.IsNull(window.FindName("CurrentSessionId"));
        Assert.IsTrue(window.Title.Contains(((MainViewModel)window.DataContext).ActiveWorkTask.DisplayName, StringComparison.Ordinal));
        var runOptions = (WrapPanel)chat.FindName("RunOptionsPanel");
        var openSettings = (Button)chat.FindName("OpenChatSettingsButton");
        CollectionAssert.AreEqual(new UIElement[] { loop, openSettings }, runOptions.Children.Cast<UIElement>().ToArray());
        Assert.AreSame(model.ToggleChatSettingsCommand, openSettings.Command);
        Assert.IsGreaterThanOrEqualTo(loop.ActualWidth, openSettings.TranslatePoint(new Point(), runOptions).X);
        Assert.IsLessThanOrEqualTo(1, Math.Abs(openSettings.TranslatePoint(new Point(0, openSettings.ActualHeight / 2), runOptions).Y
            - loop.TranslatePoint(new Point(0, loop.ActualHeight / 2), runOptions).Y), "Loop and settings entry must share one row.");
        var auxiliary = (StackPanel)((Button)chat.FindName("CompressConversationButton")).Parent;
        CollectionAssert.AreEqual(new[] { "CompressConversationButton", "NewSessionButton" },
            auxiliary.Children.Cast<FrameworkElement>().Select(entry => entry.Name).ToArray());
        foreach (string name in new[] { "WorkspaceInput", "ApplyWorkspaceButton", "ModelSelector", "ReasoningEffortSelector", "TaskHistoryButton", "OpenLogDirectoryButton" })
        {
            var entry = (FrameworkElement)window.FindName(name);
            Assert.IsTrue(entry.IsVisible && entry.ActualWidth > 0 && entry.ActualHeight > 0, name);
            Assert.IsLessThan(cardTop, entry.TranslatePoint(new Point(0, entry.ActualHeight), content).Y, name);
        }
        foreach (string name in new[] { "AbilitySelector", "AddImageButton", "SendButton" })
        {
            var entry = (FrameworkElement)chat.FindName(name);
            Assert.IsTrue(entry.IsVisible && entry.ActualWidth > 0 && entry.ActualHeight > 0, name);
            double top = entry.TranslatePoint(new Point(), card).Y;
            Assert.IsTrue(top >= 0 && top + entry.ActualHeight <= card.ActualHeight, name);
        }
        foreach (string name in new[] { "LoopIterationCheckBox", "OpenChatSettingsButton", "CompressConversationButton", "NewSessionButton" })
        {
            var entry = (FrameworkElement)chat.FindName(name);
            Assert.IsTrue(entry.IsVisible && entry.ActualWidth > 0 && entry.ActualHeight > 0, name);
            double top = entry.TranslatePoint(new Point(), card).Y;
            Assert.IsGreaterThanOrEqualTo(card.ActualHeight, top, name);
            Assert.IsLessThanOrEqualTo(content.ActualHeight, entry.TranslatePoint(new Point(entry.ActualWidth, entry.ActualHeight), content).Y, name);
        }
        Assert.IsNotNull(model.SelectedModel);
        TestContext.WriteLine($"Composer state: language={Lang.Current.IetfLanguageTag}; client=1440x900; dpi=96; model={model.SelectedModel.DisplayName}; reasoning={model.SelectedReasoningEffort?.DisplayName}; ability={model.SelectedAbility?.Id}; switches=false,false,false,false; attachments=0");
        var input = (TextBox)chat.FindName("MessageInputTextBox");
        await WpfInput.ClickAsync(input);
        Assert.AreEqual(string.Empty, input.Text);
        await screenshots.CaptureAsync("composer-empty-draft");
        WpfInput.Text("Offline composer draft");
        WpfInput.Key(0x0D);
        WpfInput.Text("Second line - no request");
        await WpfInput.WaitAsync(() => input.Text.Replace("\r\n", "\n") == "Offline composer draft\nSecond line - no request", "Composer draft input failed.");
        Assert.IsEmpty(model.PendingImages);
        await screenshots.CaptureAsync("composer-multiline-draft");
        var ability = (ComboBox)chat.FindName("AbilitySelector");
        await WpfInput.ClickAsync(ability);
        await WpfInput.WaitAsync(() => ability.IsDropDownOpen, "Ability dropdown did not open.");
        var popup = (System.Windows.Controls.Primitives.Popup)ability.Template.FindName("PART_Popup", ability);
        TestContext.WriteLine($"Composer culture={System.Globalization.CultureInfo.CurrentUICulture.Name}; abilities={string.Join("|", model.AvailableAbilities.Select(item => item.DisplayName))}");
        foreach (var item in ability.Items.Cast<object>().Select(value => ability.ItemContainerGenerator.ContainerFromItem(value)).OfType<ComboBoxItem>())
            TestContext.WriteLine($"Ability dropdown item: selected={item.IsSelected}; hover={item.IsMouseOver}; focus={item.IsKeyboardFocusWithin}; highlighted={item.IsHighlighted}; size={item.ActualWidth}x{item.ActualHeight}");
        await screenshots.CaptureSurfaceAsync((FrameworkElement)popup.Child, "composer-ability-dropdown-open");
        WpfInput.Key(0x23);
        WpfInput.Key(0x0D);
        await WpfInput.WaitAsync(() => !ability.IsDropDownOpen, "Ability dropdown did not close.");
        Assert.AreEqual(ability.Items.Count - 1, ability.SelectedIndex);
        await screenshots.CaptureAsync("composer-ability-selected");
        await WpfInput.ClickAsync(ability);
        WpfInput.Key(0x24);
        WpfInput.Key(0x0D);
        await WpfInput.WaitAsync(() => !ability.IsDropDownOpen && ability.SelectedIndex == 0, "Restore coding ability.");
        foreach (string name in new[] { "ModelSelector", "ReasoningEffortSelector" })
        {
            var selector = (ComboBox)window.FindName(name);
            await WpfInput.ClickAsync(selector);
            await WpfInput.WaitAsync(() => selector.IsDropDownOpen, name);
            WpfInput.Key(0x24);
            await screenshots.CaptureAsync($"{name}-open-anchor");
            var selectorPopup = (System.Windows.Controls.Primitives.Popup)selector.Template.FindName("PART_Popup", selector);
            await screenshots.CaptureSurfaceAsync((FrameworkElement)selectorPopup.Child, $"{name}-focus");
            var last = (ComboBoxItem)selector.ItemContainerGenerator.ContainerFromIndex(selector.Items.Count - 1);
            await WpfInput.HoverAsync(last);
            Assert.IsTrue(last.IsMouseOver);
            await screenshots.CaptureSurfaceAsync((FrameworkElement)selectorPopup.Child, $"{name}-hover");
            WpfInput.Key(0x23);
            WpfInput.Key(0x0D);
            await WpfInput.WaitAsync(() => !selector.IsDropDownOpen, name);
            Assert.AreEqual(selector.Items.Count - 1, selector.SelectedIndex);
            await screenshots.CaptureAsync($"{name}-selected");
        }
        await WpfInput.ClickAsync(loop);
        await WpfInput.WaitAsync(() => loop.IsChecked == true, "Loop option did not check.");
        await WpfInput.ClickAsync(openSettings);
        await WpfInput.WaitAsync(() => model.IsChatSettingsOpen, "Chat settings did not open.");
        foreach (string name in new[] { "ChatAutomaticCompressionToggle", "ChatDotNetRunToggle", "ChatResponsesApiToggle" })
        {
            var toggle = (CheckBox)settings.FindName(name);
            await WpfInput.ClickAsync(toggle);
            await WpfInput.WaitAsync(() => toggle.IsChecked == true, $"Checkbox did not check: {name}; enabled={toggle.IsEnabled}");
            Assert.IsTrue(toggle.IsChecked.GetValueOrDefault());
            Assert.IsTrue(toggle.IsMouseOver);
            await screenshots.CaptureAsync($"{name}-checked-hover");
        }
        await WpfInput.ClickAsync((Button)settings.FindName("CloseChatSettingsButton"));
        await WpfInput.WaitAsync(() => !model.IsChatSettingsOpen, "Chat settings did not close.");
        await WpfInput.HoverAsync(input);
        await screenshots.CaptureAsync("composer-all-options-checked");
        window.Close();
        await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !window.IsVisible, "Composer window did not close.");
        await ((MainViewModel)window.DataContext).DisposeAsync();
    });

    [TestMethod]
    public async Task MultilineDraftSurvivesSettingsAndTaskSwitchThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "MultilineDraftData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"},{"ModelName":"alternate","ModelId":"alternate-id","Provider":"test"}]}]}""");
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        IDataObject? previousClipboard = Clipboard.GetDataObject();
        try
        {
            Guid original = model.ActiveWorkTask!.Id;
            var composer = (ChatView)window.FindName("ChatComposer");
            var input = (TextBox)composer.FindName("MessageInputTextBox");
            string first = Lang.Current.IetfLanguageTag == "en" ? "First draft" : "第一份草稿";
            await WpfInput.ClickAsync(input);
            WpfInput.Text(first);
            WpfInput.Key(0x0D);
            WpfInput.Text("second line");
            await WpfInput.WaitAsync(() => model.ChatViewModel.InputText.Contains("second line"), "Multiline draft did not reach runtime.");
            string draft = model.ChatViewModel.InputText;
            Assert.Contains('\n', draft);
            var reasoning = (ComboBox)window.FindName("ReasoningEffortSelector");
            await WpfInput.ClickAsync(reasoning);
            WpfInput.Key(0x23);
            WpfInput.Key(0x0D);
            await WpfInput.WaitAsync(() => reasoning.SelectedIndex == reasoning.Items.Count - 1, "Reasoning selection failed.");
            string? reasoningName = model.ChatViewModel.SelectedReasoningEffort?.DisplayName;
            var models = (ComboBox)window.FindName("ModelSelector");
            await WpfInput.ClickAsync(models);
            WpfInput.Key(0x23);
            WpfInput.Key(0x0D);
            await WpfInput.WaitAsync(() => models.SelectedIndex == models.Items.Count - 1, "Model selection failed.");
            string? modelName = model.ChatViewModel.SelectedModel?.DisplayName;
            var abilities = (ComboBox)composer.FindName("AbilitySelector");
            await WpfInput.ClickAsync(abilities);
            WpfInput.Key(0x23);
            WpfInput.Key(0x0D);
            await WpfInput.WaitAsync(() => abilities.SelectedIndex == abilities.Items.Count - 1, "Ability selection failed.");
            string? abilityId = model.ChatViewModel.SelectedAbility?.Id;
            string workspace = Path.Combine(root, "Workspace");
            Directory.CreateDirectory(workspace);
            await WpfInput.ClickAsync((TextBox)window.FindName("WorkspaceInput"));
            WpfInput.Text(workspace);
            await WpfInput.ClickAsync((Button)window.FindName("ApplyWorkspaceButton"));
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsChangingWorkspace
                && model.ChatViewModel.NextRunWorkspacePath == workspace, "Workspace was not applied.");
            var loop = (CheckBox)composer.FindName("LoopIterationCheckBox");
            bool? originalLoop = loop.IsChecked;
            await WpfInput.ClickAsync(loop);
            await WpfInput.WaitAsync(() => loop.IsChecked == !originalLoop, "Loop option did not toggle.");
            await WpfInput.ClickAsync((Button)composer.FindName("OpenChatSettingsButton"));
            await WpfInput.WaitAsync(() => model.ChatViewModel.IsChatSettingsOpen, "Chat settings did not open.");
            var settings = (ChatSettingsView)window.FindName("ChatSettingsPanel");
            var toggles = new[] { "ChatAutomaticCompressionToggle", "ChatDotNetRunToggle", "ChatResponsesApiToggle" }
                .Select(name => (CheckBox)settings.FindName(name)).ToArray();
            bool?[] originalOptions = toggles.Select(toggle => toggle.IsChecked).ToArray();
            foreach (var toggle in toggles)
            {
                await WpfInput.ClickAsync(toggle);
            }
            await WpfInput.WaitAsync(() => toggles.Select(toggle => toggle.IsChecked).SequenceEqual(originalOptions.Select(value => !value)), "Run options did not toggle.");
            Assert.AreEqual(loop.IsChecked, model.ChatViewModel.IsLoopIterationEnabled);
            Assert.AreEqual(toggles[0].IsChecked, model.ChatViewModel.RunOptions.EnableAutomaticCompression);
            Assert.AreEqual(toggles[1].IsChecked, model.ChatViewModel.RunOptions.EnableDotNetRun);
            Assert.AreEqual(toggles[2].IsChecked, model.ChatViewModel.IsResponsesApiEnabled);
            await WpfInput.ClickAsync((Button)settings.FindName("CloseChatSettingsButton"));
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsChatSettingsOpen, "Chat settings did not close.");
            Assert.AreEqual(draft, input.Text);
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(48, 32, 96, 96, PixelFormats.Bgr32, null,
                Enumerable.Repeat((byte)128, 48 * 32 * 4).ToArray(), 48 * 4);
            Clipboard.SetImage(bitmap);
            await WpfInput.ClickAsync(input);
            WpfInput.ControlKey(0x56);
            await WpfInput.WaitAsync(() => model.ChatViewModel.PendingImages.Count == 1
                && Descendants(composer).OfType<BinaryImage>().Any(image => image.Source is not null), "Clipboard image preview did not appear.");
            byte[] attachment = model.ChatViewModel.PendingImages.Single().Data.ToArray();
            Assert.AreEqual(draft, input.Text);
            await screenshots.CaptureAsync("multiline-options");
            await WpfInput.ClickAsync(Descendants(composer).OfType<Button>().Single(button => button.Name == "PreviewImageButton"));
            await WpfInput.WaitAsync(() => window.OwnedWindows.Cast<Window>().Any(), "Attachment viewer did not open.");
            Window viewer = window.OwnedWindows.Cast<Window>().Single();
            Assert.AreEqual(model.ChatViewModel.PendingImages.Single().FileName, viewer.Title);
            Assert.IsNotNull(((BinaryImage)viewer.Content).Source);
            await screenshots.CaptureSurfaceAsync((FrameworkElement)viewer.Content, "attachment-viewer");
            WpfInput.Key(0x1B);
            await WpfInput.WaitAsync(() => !viewer.IsVisible, "Attachment viewer did not close with Escape.");
            CollectionAssert.AreEqual(attachment, model.ChatViewModel.PendingImages.Single().Data.ToArray());
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
            await WpfInput.ClickAsync(Descendants(window).OfType<Button>().Single(button => button.Name == "BackButton" && button.IsVisible));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "Settings did not return to chat.");
            Assert.AreEqual(draft, input.Text);
            CollectionAssert.AreEqual(attachment, model.ChatViewModel.PendingImages.Single().Data.ToArray());
            await WpfInput.ClickAsync((Button)window.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => model.WorkTasks.Count == 2 && model.ActiveWorkTask!.Id != original, "Second task did not activate.");
            Assert.AreEqual(string.Empty, input.Text);
            Assert.IsEmpty(model.ChatViewModel.PendingImages);
            await WpfInput.ClickAsync(input);
            WpfInput.Text("Other task draft");
            await WpfInput.WaitAsync(() => model.ChatViewModel.InputText == "Other task draft", "Second task draft missing.");
            Button firstTask = Descendants((ItemsControl)window.FindName("TaskItems")).OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, model.ActivateWorkTaskCommand)
                    && button.DataContext is WorkTaskItemViewModel task && task.Id == original);
            await WpfInput.ClickAsync(firstTask);
            await WpfInput.WaitAsync(() => model.ActiveWorkTask!.Id == original, "Original task did not activate.");
            Assert.AreEqual(draft, input.Text);
            Assert.AreEqual(reasoningName, model.ChatViewModel.SelectedReasoningEffort?.DisplayName);
            Assert.AreEqual(modelName, model.ChatViewModel.SelectedModel?.DisplayName);
            Assert.AreEqual(abilityId, model.ChatViewModel.SelectedAbility?.Id);
            Assert.AreEqual(workspace, model.ChatViewModel.NextRunWorkspacePath);
            CollectionAssert.AreEqual(attachment, model.ChatViewModel.PendingImages.Single().Data.ToArray());
            await screenshots.CaptureAsync("restored-draft");
            await WpfInput.ClickAsync(Descendants(composer).OfType<Button>().Single(button => button.Name == "RemoveImageButton"));
            await WpfInput.WaitAsync(() => !model.ChatViewModel.HasPendingImages, "Image removal failed.");
            Assert.AreEqual(draft, input.Text);
            Clipboard.SetText(" plain clipboard text");
            await WpfInput.ClickAsync(input);
            WpfInput.Key(0x23);
            WpfInput.ControlKey(0x56);
            await WpfInput.WaitAsync(() => input.Text.Contains("plain clipboard text"), "Text paste fallback failed.");
            await screenshots.CaptureAsync("image-removed");
        }
        finally
        {
            if (previousClipboard is null) Clipboard.Clear();
            else Clipboard.SetDataObject(previousClipboard, true);
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Chat window did not close.");
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task LocalHttpSendAndCancelThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        string root = Path.Combine(AppContext.BaseDirectory, "HttpSendAndCancelData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string endpoint = $"http://127.0.0.1:{((System.Net.IPEndPoint)listener.LocalEndpoint).Port}/v1";
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"), JsonSerializer.Serialize(new
        {
            PrimaryModel = "demo", OpenAIConfigurationList = new[] { new
            {
                EndPoint = endpoint, Key = "offline-test", ModelDefinitions = new[] { new { ModelName = "demo", ModelId = "model-id", Provider = "test" } }
            } }
        }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            var composer = (ChatView)window.FindName("ChatComposer");
            var input = (TextBox)composer.FindName("MessageInputTextBox");
            var send = (Button)composer.FindName("SendButton");
            var compact = (Button)composer.FindName("CompressConversationButton");
            await WpfInput.ClickAsync((Button)composer.FindName("OpenChatSettingsButton"));
            await WpfInput.WaitAsync(() => model.ChatViewModel.IsChatSettingsOpen, "Chat settings did not open.");
            var settings = (ChatSettingsView)window.FindName("ChatSettingsPanel");
            var stopLsp = (Button)settings.FindName("StopLanguageServerButton");
            Assert.AreSame(model.ChatViewModel.CompressConversationCommand, compact.Command);
            Assert.AreSame(model.ChatViewModel.StopLanguageServerCommand, stopLsp.Command);
            Assert.AreEqual((string)Lang.Current.CompressConversationText, compact.Content?.ToString());
            Assert.AreEqual((string)Lang.Current.StopLanguageServerText, stopLsp.Content?.ToString());
            Assert.IsTrue(compact.IsEnabled);
            Assert.IsTrue(stopLsp.IsEnabled);
            await WpfInput.ClickAsync(stopLsp);
            await WpfInput.ClickAsync((Button)settings.FindName("CloseChatSettingsButton"));
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsChatSettingsOpen, "Chat settings did not close.");
            await WpfInput.WaitAsync(() => model.ChatViewModel.StatusText == (string)Lang.Current.LspNotRunning
                && Descendants((ItemsControl)window.FindName("HistoryMessages")).OfType<TextBlock>()
                    .Any(text => text.Text.Contains((string)Lang.Current.LspNotRunning)), "No-service LSP notice did not reach the UI.");
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            await screenshots.CaptureAsync("auxiliary-no-lsp-minimum-window");
            await WpfInput.ClickAsync(input);
            string prompt = Lang.Current.IetfLanguageTag == "en" ? "Local HTTP question\nSecond line 中文" : "本地HTTP问题\n第二行 English";
            WpfInput.Text(prompt.Split('\n')[0]);
            WpfInput.Key(0x0D);
            WpfInput.Text(prompt.Split('\n')[1]);
            IDataObject? previousClipboard = Clipboard.GetDataObject();
            try
            {
                Clipboard.SetImage(System.Windows.Media.Imaging.BitmapSource.Create(48, 32, 96, 96, PixelFormats.Bgr32, null,
                    Enumerable.Repeat((byte)128, 48 * 32 * 4).ToArray(), 48 * 4));
                WpfInput.ControlKey(0x56);
                await WpfInput.WaitAsync(() => model.ChatViewModel.PendingImages.Count == 1, "HTTP image paste failed.");
            }
            finally
            {
                if (previousClipboard is null) Clipboard.Clear();
                else Clipboard.SetDataObject(previousClipboard, true);
            }
            byte[] sentImage = model.ChatViewModel.PendingImages.Single().Data.ToArray();
            await WpfInput.WaitAsync(() => send.IsEnabled, "Send did not enable.");
            WpfInput.ControlKey(0x0D);
            using (System.Net.Sockets.TcpClient client = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation))
            {
                string request = await ReadHttpRequestAsync(client);
                StringAssert.StartsWith(request, "POST /v1/chat/completions HTTP/1.1");
                using JsonDocument body = JsonDocument.Parse(request[(request.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]);
                JsonElement content = body.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content");
                Assert.AreEqual(prompt.Replace("\n", "\r\n", StringComparison.Ordinal), content.EnumerateArray().Single(part => part.GetProperty("type").GetString() == "text").GetProperty("text").GetString());
                string imageUrl = content.EnumerateArray().Single(part => part.GetProperty("type").GetString() == "image_url")
                    .GetProperty("image_url").GetProperty("url").GetString()!;
                StringAssert.StartsWith(imageUrl, "data:image/png;base64,");
                CollectionAssert.AreEqual(sentImage, Convert.FromBase64String(imageUrl[(imageUrl.IndexOf(',') + 1)..]));
                string response = "data: {\"id\":\"local-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"model-id\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Controlled local answer\"},\"finish_reason\":null}]}\n\ndata: {\"id\":\"local-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"model-id\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(response);
                byte[] header = System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                await client.GetStream().WriteAsync(header, WpfTestSession.Cancellation);
                int firstEventLength = response.IndexOf("\n\n", StringComparison.Ordinal) + 2;
                int firstEventBytes = System.Text.Encoding.UTF8.GetByteCount(response[..firstEventLength]);
                await client.GetStream().WriteAsync(bytes.AsMemory(0, firstEventBytes), WpfTestSession.Cancellation);
                await WpfInput.WaitAsync(() => model.ChatViewModel.IsRunning
                    && Descendants((ItemsControl)window.FindName("HistoryMessages")).OfType<TextBox>().Any(text => text.Text.Contains("Controlled local answer")), "Streaming text was not rendered before completion.");
                await screenshots.CaptureAsync("streaming-answer-before-completion");
                await client.GetStream().WriteAsync(bytes.AsMemory(firstEventBytes), WpfTestSession.Cancellation);
            }
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsRunning && !model.ChatViewModel.IsFinalizing
                && Descendants((ItemsControl)window.FindName("HistoryMessages")).OfType<TextBox>().Any(text => text.Text.Contains("Controlled local answer")), "Real HTTP response did not reach conversation.");
            Assert.AreEqual(string.Empty, input.Text);
            Assert.IsEmpty(model.ChatViewModel.PendingImages);
            window.Width = 1440;
            window.Height = 900;
            var history = (ItemsControl)window.FindName("HistoryMessages");
            DependencyObject ancestor = history;
            while (ancestor is not ScrollViewer) ancestor = VisualTreeHelper.GetParent(ancestor);
            var scroll = (ScrollViewer)ancestor;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            for (int attempt = 0; attempt < 8 && scroll.VerticalOffset < scroll.ScrollableHeight - 1; attempt++)
            {
                await WpfInput.WheelAsync(scroll, -1200);
                await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            await WpfInput.WaitAsync(() => scroll.VerticalOffset >= scroll.ScrollableHeight - 1, $"HTTP answer did not scroll into view through mouse input: {scroll.VerticalOffset}/{scroll.ScrollableHeight}.");
            var answer = Descendants(history).OfType<TextBox>().Single(text => text.Text.Contains("Controlled local answer"));
            Rect bounds = answer.TransformToAncestor(scroll).TransformBounds(new Rect(answer.RenderSize));
            Assert.IsTrue(bounds.Top >= 0 && bounds.Bottom <= scroll.ViewportHeight && bounds.Left >= 0 && bounds.Right <= scroll.ActualWidth,
                $"HTTP answer was clipped: {bounds}; viewport={scroll.ActualWidth}x{scroll.ViewportHeight}.");
            await screenshots.CaptureAsync("local-http-answer");
            await WpfInput.WaitAsync(() => compact.IsEnabled, "Manual compaction did not enable after chat.");
            await WpfInput.ClickAsync(compact);
            using (System.Net.Sockets.TcpClient compression = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation))
            {
                string request = await ReadHttpRequestAsync(compression);
                StringAssert.StartsWith(request, "POST /v1/chat/completions HTTP/1.1");
                await WpfInput.WaitAsync(() => model.ChatViewModel.IsCompressing, "Compaction busy state did not appear.");
                Assert.IsFalse(compact.IsEnabled);
                Assert.IsFalse(stopLsp.IsEnabled);
                Assert.AreEqual((string)Lang.Current.Compacting, model.ChatViewModel.StatusText);
                await screenshots.CaptureAsync("manual-compaction-busy");
                byte[] failure = System.Text.Encoding.UTF8.GetBytes("{\"error\":{\"message\":\"Controlled offline compaction rejection\",\"type\":\"invalid_request_error\",\"code\":\"offline_test\"}}");
                byte[] header = System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 400 Bad Request\r\nContent-Type: application/json\r\nContent-Length: {failure.Length}\r\nConnection: close\r\n\r\n");
                await compression.GetStream().WriteAsync(header, WpfTestSession.Cancellation);
                await compression.GetStream().WriteAsync(failure, WpfTestSession.Cancellation);
            }
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsCompressing && !model.ChatViewModel.IsFinalizing
                && compact.IsEnabled && stopLsp.IsEnabled
                && model.ChatViewModel.StatusText.Contains("Controlled offline compaction rejection"), "Compaction failure did not restore auxiliary actions.");
            var visibleStatus = (TextBlock)window.FindName("ConversationStatus");
            await WpfInput.WaitAsync(() => visibleStatus.IsVisible && visibleStatus.Text.Contains("Controlled offline compaction rejection"), "Compaction error is not visible.");
            await screenshots.CaptureAsync("manual-compaction-offline-failure");
            await WpfInput.ClickAsync(input);
            WpfInput.Text("Wait for cancellation");
            await WpfInput.WaitAsync(() => input.Text == "Wait for cancellation" && send.IsEnabled, "Second prompt did not reach send command.");
            await WpfInput.ClickAsync(send);
            using System.Net.Sockets.TcpClient pending = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation);
            await ReadHttpRequestAsync(pending);
            await WpfInput.WaitAsync(() => model.ChatViewModel.IsRunning, "Busy state did not appear.");
            Assert.IsFalse(((ComboBox)window.FindName("ModelSelector")).IsEnabled);
            Assert.IsFalse(((ComboBox)window.FindName("ReasoningEffortSelector")).IsEnabled);
            Assert.IsTrue(((ComboBox)composer.FindName("AbilitySelector")).IsEnabled);
            Assert.IsFalse(compact.IsEnabled);
            Assert.IsFalse(stopLsp.IsEnabled);
            Assert.IsTrue(((Button)composer.FindName("StopButton")).IsVisible);
            await WpfInput.ClickAsync(input);
            WpfInput.Text("Interruption draft");
            await WpfInput.WaitAsync(() => send.IsEnabled, "Busy interruption send did not enable.");
            Assert.AreEqual((string)Lang.Current.InterruptText, send.Content);
            Assert.IsTrue(visibleStatus.IsVisible);
            Assert.AreEqual(model.ChatViewModel.StatusText, visibleStatus.Text);
            await screenshots.CaptureAsync("local-http-busy");
            await WpfInput.ClickAsync((Button)composer.FindName("StopButton"));
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsRunning && !model.ChatViewModel.IsFinalizing, "Cancel did not stop real runtime.");
            Assert.IsTrue(((ComboBox)window.FindName("ModelSelector")).IsEnabled);
            await screenshots.CaptureAsync("local-http-canceled");
        }
        finally
        {
            listener.Stop();
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "HTTP chat window did not close.");
            await model.DisposeAsync();
        }
    });

    private static async Task<string> ReadHttpRequestAsync(System.Net.Sockets.TcpClient client)
    {
        using var data = new MemoryStream();
        byte[] single = new byte[1];
        string header;
        do
        {
            if (await client.GetStream().ReadAsync(single, WpfTestSession.Cancellation) == 0) throw new EndOfStreamException();
            data.WriteByte(single[0]);
            header = System.Text.Encoding.ASCII.GetString(data.ToArray());
        } while (!header.EndsWith("\r\n\r\n", StringComparison.Ordinal));
        int length = int.Parse(header.Split("\r\n").Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        byte[] body = new byte[length];
        await client.GetStream().ReadExactlyAsync(body, WpfTestSession.Cancellation);
        return header + System.Text.Encoding.UTF8.GetString(body);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
