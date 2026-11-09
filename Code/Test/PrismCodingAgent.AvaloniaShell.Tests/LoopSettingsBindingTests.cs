using Avalonia.Controls;
using Avalonia.Threading;
using PrismCodingAgent.CodingShell.Services.Chat;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class LoopSettingsBindingTests
{
    [TestMethod]
    [DataRow("ChatRunSettingsTitle", "ChatAutomaticCompressionToggle,ChatResponsesApiToggle")]
    [DataRow("ChatToolSettingsTitle", "ChatDotNetRunToggle,ChatWebFetchToggle,ChatWebSearchToggle")]
    public void SettingsSectionsShouldContainOnlyOrderedOptions(string titleKey, string names)
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var first = view.FindControl<ToggleSwitch>(names.Split(',')[0])!;
        var section = (StackPanel)first.Parent!.Parent!;
        CollectionAssert.AreEqual(names.Split(','), section.Children.OfType<Grid>()
            .SelectMany(row => row.Children.OfType<ToggleSwitch>()).Select(toggle => toggle.Name).ToArray());
        foreach (string language in new[] { "de", "en", "es", "fr", "it", "ja", "ko", "nl", "pt-BR", "ru", "zh-Hans", "zh-Hant" })
        {
            PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent(language);
            Dispatcher.UIThread.RunJobs();
            string title = titleKey == "ChatRunSettingsTitle"
                ? PrismCodingAgent.CodingShell.Localizations.Lang.Current.ChatRunSettingsTitle
                : PrismCodingAgent.CodingShell.Localizations.Lang.Current.ChatToolSettingsTitle;
            Assert.IsFalse(string.IsNullOrWhiteSpace(title));
            Assert.AreEqual(title, section.Children.OfType<TextBlock>().Single().Text);
        }
    }

    [TestMethod]
    [DataRow("LoopMaximumIterationsInput")]
    [DataRow("LoopMaximumDurationInput")]
    public void NumericSettingsShouldBindInBothDirections(string name)
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var input = view.FindControl<TextBox>(name)!;
        input.Text = "71583";
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(71583, name == "LoopMaximumIterationsInput"
            ? model.LoopOptions.MaximumIterations : model.LoopOptions.MaximumDurationMinutes);
        model.LoopOptions.MaximumIterations = int.MaxValue;
        model.LoopOptions.MaximumDurationMinutes = int.MaxValue;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual("2147483647", input.Text);
        input.Text = "0";
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(0, name == "LoopMaximumIterationsInput"
            ? model.LoopOptions.MaximumIterations : model.LoopOptions.MaximumDurationMinutes);
    }

    [TestMethod]
    [DataRow("LoopMaximumIterationsInput", "invalid")]
    [DataRow("LoopMaximumIterationsInput", "-1")]
    [DataRow("LoopMaximumIterationsInput", "2147483648")]
    [DataRow("LoopMaximumIterationsInput", "1.5")]
    [DataRow("LoopMaximumDurationInput", "invalid")]
    [DataRow("LoopMaximumDurationInput", "-1")]
    [DataRow("LoopMaximumDurationInput", "2147483648")]
    [DataRow("LoopMaximumDurationInput", "1.5")]
    public void InvalidTextShouldPreserveSettingsAndShowValidation(string name, string text)
    {
        using var model = new ChatViewModel();
        model.LoopOptions.MaximumIterations = 12;
        model.LoopOptions.MaximumDurationMinutes = 12;
        var view = new ChatSettingsView { DataContext = model };
        var input = view.FindControl<TextBox>(name)!;
        input.Text = text;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(12, name == "LoopMaximumIterationsInput"
            ? model.LoopOptions.MaximumIterations : model.LoopOptions.MaximumDurationMinutes);
        Assert.IsTrue(DataValidationErrors.GetHasErrors(input));
    }

    [TestMethod]
    [DataRow("ChatWebSearchToggle", "EnableWebSearch", "EnableWebSearchText")]
    [DataRow("ChatWebFetchToggle", "EnableWebFetch", "EnableWebFetchText")]
    public void WebSwitchesShouldBindAndLocalize(string name, string propertyName, string labelName)
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var toggle = view.FindControl<ToggleSwitch>(name)!;
        var property = typeof(AgentLib.Coding.CodingChatRunOptions).GetProperty(propertyName)!;
        Assert.IsTrue(toggle.IsChecked);
        toggle.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(false, property.GetValue(model.RunOptions));
        property.SetValue(model.RunOptions, true);
        Dispatcher.UIThread.RunJobs();
        Assert.IsTrue(toggle.IsChecked);
        string originalLanguage = PrismCodingAgent.CodingShell.Localizations.Lang.Current.IetfLanguageTag;
        try
        {
            PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent("en");
            Dispatcher.UIThread.RunJobs();
            string? english = Avalonia.Automation.AutomationProperties.GetName(toggle);
            PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent("zh-Hans");
            Dispatcher.UIThread.RunJobs();
            Assert.AreNotEqual(english, Avalonia.Automation.AutomationProperties.GetName(toggle));
            Assert.AreEqual(PrismCodingAgent.CodingShell.Localizations.Lang.Current.GetType().GetProperty(labelName)!
                .GetValue(PrismCodingAgent.CodingShell.Localizations.Lang.Current)?.ToString(),
                Avalonia.Automation.AutomationProperties.GetName(toggle));
        }
        finally
        {
            PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent(originalLanguage);
        }
    }

    [TestMethod]
    public void ConversationModeShouldBindInBothDirections()
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var selector = view.FindControl<ComboBox>("LoopConversationSelector")!;
        selector.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(LoopConversationMode.Append, model.LoopOptions.ConversationMode);
        model.LoopOptions.ConversationMode = LoopConversationMode.Restart;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(1, selector.SelectedIndex);
    }

    [TestMethod]
    [DataRow("LoopMaximumIterationsInput")]
    [DataRow("LoopMaximumDurationInput")]
    public void LimitsShouldRemainInsideCompactLoopGroup(string name)
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var group = view.FindControl<Border>("LoopSettingsGroup")!;
        var input = view.FindControl<TextBox>(name)!;
        Assert.AreSame(group.Child, input.Parent!.Parent);
        Assert.AreEqual(32d, input.Height);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void OnlySelectedModeExplanationShouldBeDisplayed(int index)
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var selector = view.FindControl<ComboBox>("LoopConversationSelector")!;
        selector.SelectedIndex = index;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(((ComboBoxItem)selector.SelectedItem!).DataContext?.ToString(),
            view.FindControl<TextBlock>("LoopConversationHint")!.Text);
    }
}
