using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfChatSettingsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("ChatRunSettingsTitle", "ChatAutomaticCompressionToggle,ChatResponsesApiToggle")]
    [DataRow("ChatToolSettingsTitle", "ChatDotNetRunToggle,ChatWebFetchToggle,ChatWebSearchToggle")]
    public async Task SettingsSectionsShouldContainOnlyOrderedOptionsAsync(string titleKey, string names)
        => await WpfTestClassAttribute.RunUiAsync(async () =>
        {
            using var model = new ChatViewModel();
            var settings = new ChatSettingsView { DataContext = model };
            var first = (CheckBox)settings.FindName(names.Split(',')[0]);
            var section = (StackPanel)((Grid)first.Parent).Parent;
            CollectionAssert.AreEqual(names.Split(','), section.Children.OfType<Grid>()
                .SelectMany(row => row.Children.OfType<CheckBox>()).Select(toggle => toggle.Name).ToArray());
            foreach (string language in new[] { "de", "en", "es", "fr", "it", "ja", "ko", "nl", "pt-BR", "ru", "zh-Hans", "zh-Hant" })
            {
                PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent(language);
                await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                string title = titleKey == "ChatRunSettingsTitle"
                    ? PrismCodingAgent.CodingShell.Localizations.Lang.Current.ChatRunSettingsTitle
                    : PrismCodingAgent.CodingShell.Localizations.Lang.Current.ChatToolSettingsTitle;
                Assert.IsFalse(string.IsNullOrWhiteSpace(title));
                Assert.AreEqual(title, section.Children.OfType<TextBlock>().Single().Text);
            }
        });

    [TestMethod]
    public async Task RedesignedSettingsRenderAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent("zh-Hans");
        var model = new MainViewModel();
        var view = new ChatSettingsView { DataContext = model.ChatViewModel, Background = System.Windows.Media.Brushes.White };
        var window = new Window { Content = view, Width = 920, Height = 760, Background = System.Windows.Media.Brushes.White };
        window.Closing += (_, args) => args.Cancel = false;
        using var session = new WpfScreenshotSession(window, TestContext);
        try
        {
            model.ChatViewModel.IsChatSettingsOpen = true;
            window.Show();
            await session.CaptureAsync("chat-settings-redesigned");
            window.Width = 1280;
            window.Height = 720;
            await session.CaptureAsync("chat-settings-compact");
        }
        finally { window.Close(); await model.DisposeAsync(); }
    });
    [TestMethod]
    [DataRow("ChatAutomaticCompressionToggle", "EnableAutomaticCompression")]
    [DataRow("ChatDotNetRunToggle", "EnableDotNetRun")]
    [DataRow("ChatWebSearchToggle", "EnableWebSearch")]
    [DataRow("ChatWebFetchToggle", "EnableWebFetch")]
    [DataRow("ChatResponsesApiToggle", nameof(ChatViewModel.IsResponsesApiEnabled))]
    public async Task SettingsToggleUpdatesViewModelAndReflectsViewModelAsync(string name, string propertyName)
        => await WpfTestClassAttribute.RunUiAsync(async () =>
        {
            var model = new MainViewModel();
            var window = new MainWindow { DataContext = model };
            window.Closing += (_, args) => args.Cancel = false;
            try
            {
                var settings = (ChatSettingsView)window.FindName("ChatSettingsPanel");
                var toggle = (CheckBox)settings.FindName(name);
                object settingsSource = propertyName == nameof(ChatViewModel.IsResponsesApiEnabled) ? model.ChatViewModel : model.ChatViewModel.RunOptions;
                var property = settingsSource.GetType().GetProperty(propertyName)!;
                model.ChatViewModel.IsChatSettingsOpen = true;
                window.Measure(new Size(1440, 900));
                window.Arrange(new Rect(0, 0, 1440, 900));
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                Assert.AreSame(model.ChatViewModel, settings.DataContext);
                Assert.AreEqual(BindingMode.TwoWay, BindingOperations.GetBinding(toggle, CheckBox.IsCheckedProperty)!.Mode);
                toggle.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                toggle.SetCurrentValue(CheckBox.IsCheckedProperty, true);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsTrue((bool)property.GetValue(settingsSource)!);
                toggle.ApplyTemplate();
                var track = (Border)toggle.Template.FindName("Track", toggle);
                var knob = (FrameworkElement)toggle.Template.FindName("Knob", toggle);
                Assert.AreEqual(44d, track.Width);
                Assert.AreEqual(HorizontalAlignment.Right, knob.HorizontalAlignment);
                property.SetValue(settingsSource, false);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsFalse(toggle.IsChecked);
                Assert.AreEqual(HorizontalAlignment.Left, knob.HorizontalAlignment);
            }
            finally
            {
                window.Close();
                await model.DisposeAsync();
            }
        });

    [TestMethod]
    [DataRow("LoopMaximumIterationsInput", "-1")]
    [DataRow("LoopMaximumIterationsInput", "invalid")]
    [DataRow("LoopMaximumIterationsInput", "2147483648")]
    [DataRow("LoopMaximumDurationInput", "-1")]
    [DataRow("LoopMaximumDurationInput", "2147483648")]
    [DataRow("LoopMaximumDurationInput", "1.5")]
    [DataRow("LoopMaximumIterationsInput", "1.5")]
    [DataRow("LoopMaximumDurationInput", "invalid")]
    public async Task InvalidLoopTextShouldPreserveSettingsAsync(string name, string text)
        => await WpfTestClassAttribute.RunUiAsync(async () =>
        {
            using var model = new ChatViewModel();
            model.LoopOptions.MaximumIterations = 12;
            model.LoopOptions.MaximumDurationMinutes = 12;
            var settings = new ChatSettingsView { DataContext = model };
            var input = (TextBox)settings.FindName(name);
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            input.SetCurrentValue(TextBox.TextProperty, text);
            input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.AreEqual(12, name == "LoopMaximumIterationsInput"
                ? model.LoopOptions.MaximumIterations : model.LoopOptions.MaximumDurationMinutes);
            Assert.IsTrue(Validation.GetHasError(input));
        });

    [TestMethod]
    [DataRow("LoopMaximumIterationsInput")]
    [DataRow("LoopMaximumDurationInput")]
    public async Task LoopNumbersShouldBindInBothDirectionsAsync(string name)
        => await WpfTestClassAttribute.RunUiAsync(async () =>
        {
            using var model = new ChatViewModel();
            var settings = new ChatSettingsView { DataContext = model };
            var input = (TextBox)settings.FindName(name);
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            input.SetCurrentValue(TextBox.TextProperty, "71583");
            input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.AreEqual(71583, name == "LoopMaximumIterationsInput"
                ? model.LoopOptions.MaximumIterations : model.LoopOptions.MaximumDurationMinutes);

            model.LoopOptions.MaximumIterations = 24;
            model.LoopOptions.MaximumDurationMinutes = 24;
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual("24", input.Text);
        });

    [TestMethod]
    public async Task LoopConversationShouldBindInBothDirectionsAsync()
        => await WpfTestClassAttribute.RunUiAsync(async () =>
        {
            using var model = new ChatViewModel();
            var settings = new ChatSettingsView { DataContext = model };
            var selector = (ComboBox)settings.FindName("LoopConversationSelector");
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            selector.SetCurrentValue(ComboBox.SelectedIndexProperty, 2);
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(PrismCodingAgent.CodingShell.Services.Chat.LoopConversationMode.Append,
                model.LoopOptions.ConversationMode);

            model.LoopOptions.ConversationMode = PrismCodingAgent.CodingShell.Services.Chat.LoopConversationMode.Restart;
            await settings.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(1, selector.SelectedIndex);
        });

    [TestMethod]
    public async Task ClosingChatSettingsPreservesComposerDraftAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Closing += (_, args) => args.Cancel = false;
        try
        {
            var composer = (ChatView)window.FindName("ChatComposer");
            var input = (TextBox)composer.FindName("MessageInputTextBox");
            var overlay = (Grid)window.FindName("ChatOverlayLayer");
            var settings = (ChatSettingsView)window.FindName("ChatSettingsPanel");
            var open = (Button)window.FindName("OpenChatSettingsButton");
            var close = (Button)settings.FindName("CloseChatSettingsButton");
            const string draft = "First draft line\nSecond draft line";
            model.ChatViewModel.InputText = draft;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(Visibility.Collapsed, overlay.Visibility);
            Assert.IsNotNull(overlay.Background);
            Assert.AreSame(model.ChatViewModel.ToggleChatSettingsCommand, open.Command);
            open.Command.Execute(open.CommandParameter);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.IsTrue(model.ChatViewModel.IsChatSettingsOpen);
            Assert.AreEqual(Visibility.Visible, settings.Visibility);
            Assert.AreEqual(0, Grid.GetRow(overlay));
            Assert.AreEqual(1, Grid.GetRowSpan(overlay));
            Assert.AreSame(((Border)window.FindName("ContentSurface")).Child, overlay.Parent);
            Assert.AreSame(open.Command, close.Command);
            close.Command.Execute(close.CommandParameter);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.IsFalse(model.ChatViewModel.IsChatSettingsOpen);
            Assert.AreEqual(Visibility.Collapsed, overlay.Visibility);
            Assert.AreEqual(draft, input.Text);
            Assert.AreEqual(draft, model.ChatViewModel.InputText);
        }
        finally
        {
            window.Close();
            await model.DisposeAsync();
        }
    });
}
