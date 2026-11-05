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
    [TestMethod]
    [DataRow("ChatAutomaticCompressionToggle", nameof(ChatViewModel.IsAutomaticCompressionEnabled))]
    [DataRow("ChatDotNetRunToggle", nameof(ChatViewModel.IsDotNetRunEnabled))]
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
                var property = typeof(ChatViewModel).GetProperty(propertyName)!;
                model.ChatViewModel.IsChatSettingsOpen = true;
                window.Measure(new Size(1440, 900));
                window.Arrange(new Rect(0, 0, 1440, 900));
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                Assert.AreSame(model.ChatViewModel, settings.DataContext);
                Assert.AreEqual(BindingMode.TwoWay, BindingOperations.GetBinding(toggle, CheckBox.IsCheckedProperty)!.Mode);
                toggle.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                toggle.SetCurrentValue(CheckBox.IsCheckedProperty, true);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsTrue((bool)property.GetValue(model.ChatViewModel)!);
                toggle.ApplyTemplate();
                var track = (Border)toggle.Template.FindName("Track", toggle);
                var knob = (FrameworkElement)toggle.Template.FindName("Knob", toggle);
                Assert.AreEqual(44d, track.Width);
                Assert.AreEqual(HorizontalAlignment.Right, knob.HorizontalAlignment);
                property.SetValue(model.ChatViewModel, false);
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
            var open = (Button)composer.FindName("OpenChatSettingsButton");
            var close = (Button)settings.FindName("CloseChatSettingsButton");
            const string draft = "First draft line\nSecond draft line";
            model.ChatViewModel.InputText = draft;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(Visibility.Collapsed, settings.Visibility);
            Assert.IsNull(overlay.Background);
            Assert.AreEqual(Visibility.Visible, overlay.Visibility);
            Assert.AreSame(model.ChatViewModel.ToggleChatSettingsCommand, open.Command);
            open.Command.Execute(open.CommandParameter);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.IsTrue(model.ChatViewModel.IsChatSettingsOpen);
            Assert.AreEqual(Visibility.Visible, settings.Visibility);
            Assert.AreEqual(2, Grid.GetRow(overlay));
            Assert.AreEqual(2, Grid.GetRowSpan(overlay));
            Assert.AreSame(open.Command, close.Command);
            close.Command.Execute(close.CommandParameter);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.IsFalse(model.ChatViewModel.IsChatSettingsOpen);
            Assert.AreEqual(Visibility.Collapsed, settings.Visibility);
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
