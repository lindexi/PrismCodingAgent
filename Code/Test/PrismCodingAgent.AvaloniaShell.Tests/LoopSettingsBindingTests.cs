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
