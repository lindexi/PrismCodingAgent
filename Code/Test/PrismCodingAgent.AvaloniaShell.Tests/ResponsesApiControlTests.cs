using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.ViewModels;
using ChatView = PrismCodingAgent.CodingShell.Views.ChatView;
using ChatSettingsView = PrismCodingAgent.CodingShell.Views.ChatSettingsView;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ResponsesApiControlTests
{
    [TestMethod]
    [DataRow(false, Avalonia.Layout.HorizontalAlignment.Left)]
    [DataRow(true, Avalonia.Layout.HorizontalAlignment.Right)]
    public void ResponsesApiSwitchShouldReflectSelection(bool selected, Avalonia.Layout.HorizontalAlignment expectedAlignment)
    {
        using var viewModel = new ChatViewModel { IsResponsesApiEnabled = selected };
        var settings = new ChatSettingsView { DataContext = viewModel };
        var window = new Window { Width = 1016, Height = 620, Content = settings };
        try
        {
            window.Show();
            window.UpdateLayout();
            var toggle = settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!;
            var knob = toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
                .Single(item => item.Name == "SwitchKnob");

            Assert.AreEqual(expectedAlignment, knob.HorizontalAlignment);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ResponsesApiShouldBeDisabledByDefault()
    {
        using var viewModel = new ChatViewModel();

        Assert.IsFalse(viewModel.IsResponsesApiEnabled);
    }

    [TestMethod]
    public void ChangingResponsesApiShouldNotifyOnce()
    {
        using var viewModel = new ChatViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.IsResponsesApiEnabled = true;
        viewModel.IsResponsesApiEnabled = true;

        CollectionAssert.AreEqual(new[] { nameof(ChatViewModel.IsResponsesApiEnabled) }, changedProperties);
    }

    [TestMethod]
    public void SelectingResponsesApiShouldUpdateViewModel()
    {
        using var viewModel = new ChatViewModel();
        var settings = new ChatSettingsView { DataContext = viewModel };
        var toggle = settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!;

        toggle.IsChecked = true;

        Assert.IsTrue(viewModel.IsResponsesApiEnabled);
    }

    [TestMethod]
    public void ChangingViewModelShouldUpdateResponsesApiToggle()
    {
        using var viewModel = new ChatViewModel();
        var settings = new ChatSettingsView { DataContext = viewModel };
        var toggle = settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!;

        viewModel.IsResponsesApiEnabled = true;

        Assert.IsTrue(toggle.IsChecked);
    }

    [TestMethod]
    public void ResponsesApiShouldHaveMatchingAccessibleName()
    {
        using var viewModel = new ChatViewModel();
        var settings = new ChatSettingsView { DataContext = viewModel };
        var toggle = settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!;

        Assert.AreEqual(Lang.Current.ResponsesApiText, AutomationProperties.GetName(toggle));
    }

    [TestMethod]
    public void ResponsesApiShouldAcceptKeyboardNavigationFocus()
    {
        using var viewModel = new ChatViewModel();
        var settings = new ChatSettingsView { DataContext = viewModel };
        var window = new Window { Width = 1016, Height = 620, Content = settings };
        try
        {
            window.Show();
            var toggle = settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!;

            toggle.Focus(NavigationMethod.Tab);

            Assert.IsTrue(toggle.IsFocused);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow("ChatAutomaticCompressionToggle", "EnableAutomaticCompression")]
    [DataRow("ChatDotNetRunToggle", "EnableDotNetRun")]
    [DataRow("ChatResponsesApiToggle", nameof(ChatViewModel.IsResponsesApiEnabled))]
    public void ChatSettingsOptionShouldBindBothWays(string name, string propertyName)
    {
        using var viewModel = new ChatViewModel();
        var settings = new ChatSettingsView { DataContext = viewModel };
        var toggle = settings.FindControl<ToggleSwitch>(name);
        Assert.IsNotNull(toggle);
        object settingsSource = propertyName == nameof(ChatViewModel.IsResponsesApiEnabled) ? viewModel : viewModel.RunOptions;
        var property = settingsSource.GetType().GetProperty(propertyName)!;

        toggle.IsChecked = true;
        Assert.IsTrue((bool)property.GetValue(settingsSource)!);
        property.SetValue(settingsSource, false);
        Assert.IsFalse(toggle.IsChecked);
    }

    [TestMethod]
    public void LoopIterationCheckBoxShouldBindBothWays()
    {
        using var viewModel = new ChatViewModel();
        var chat = new ChatView { DataContext = viewModel };
        var loop = chat.FindControl<CheckBox>("LoopIterationCheckBox");
        Assert.IsNotNull(loop);

        loop.IsChecked = true;
        Assert.IsTrue(viewModel.LoopOptions.IsEnabled);
        viewModel.LoopOptions.IsEnabled = false;
        Assert.IsFalse(loop.IsChecked);
    }

    [TestMethod]
    [DataRow(640)]
    [DataRow(760)]
    [DataRow(1016)]
    public void ChatSettingsOptionsShouldAlignVerticallyWithoutOverlap(int width)
    {
        using var viewModel = new ChatViewModel();
        var settings = new ChatSettingsView { DataContext = viewModel };
        var window = new Window { Width = width, Height = 620, Content = settings };
        try
        {
            window.Show();
            window.UpdateLayout();
            var toggles = new[] { "ChatAutomaticCompressionToggle", "ChatDotNetRunToggle", "ChatResponsesApiToggle" }
                .Select(name => settings.FindControl<ToggleSwitch>(name)).ToArray();
            CollectionAssert.DoesNotContain(toggles, null);
            CollectionAssert.AreEquivalent(toggles, settings.GetVisualDescendants().OfType<ToggleSwitch>().ToArray());
            var bounds = toggles.Select(toggle =>
            {
                Assert.IsNotNull(toggle);
                return new Rect(toggle.TranslatePoint(default, settings)!.Value, toggle.Bounds.Size);
            }).ToArray();

            Assert.IsTrue(bounds.All(rect => new Rect(settings.Bounds.Size).Contains(rect)));
            Assert.IsTrue(bounds.Zip(bounds.Skip(1), (previous, next) =>
                previous.Bottom <= next.Top && Math.Abs(previous.Left - next.Left) <= 0.5).All(aligned => aligned));
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow(640)]
    [DataRow(760)]
    [DataRow(1016)]
    public void ChatSettingsEntryShouldStayInToolbar(int width)
    {
        using var viewModel = new ChatViewModel();
        var view = new ChatView { DataContext = viewModel };
        var window = new Window { Width = width, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var entry = view.FindControl<Button>("OpenChatSettingsButton")!;
            var loop = view.FindControl<CheckBox>("LoopIterationCheckBox")!;
            Assert.IsNull(view.FindControl<WrapPanel>("RunOptionsPanel"));
            Assert.AreSame(viewModel.ToggleChatSettingsCommand, entry.Command);
            var send = view.FindControl<Button>("SendButton")!;
            Assert.AreSame(loop.Parent, send.Parent);
            var compress = view.FindControl<Button>("CompressConversationButton")!;
            var entryOrigin = entry.TranslatePoint(default, view)!.Value;
            var compressOrigin = compress.TranslatePoint(default, view)!.Value;
            var loopOrigin = loop.TranslatePoint(default, view)!.Value;

            var sendOrigin = send.TranslatePoint(default, view)!.Value;
            Assert.IsTrue(loopOrigin.X >= 0 && loopOrigin.X + loop.Bounds.Width <= sendOrigin.X);
            Assert.AreEqual(sendOrigin.Y + send.Bounds.Height / 2, loopOrigin.Y + loop.Bounds.Height / 2, 0.5);
            Assert.IsTrue(entryOrigin.Y < compressOrigin.Y);
            Assert.AreEqual(32d, entry.Width);
        }
        finally { window.Close(); }
    }
}
