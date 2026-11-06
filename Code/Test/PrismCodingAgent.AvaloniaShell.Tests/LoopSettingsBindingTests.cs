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
        var input = view.FindControl<NumericUpDown>(name)!;

        input.Value = 12;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(12, name == "LoopMaximumIterationsInput"
            ? model.LoopOptions.MaximumIterations : model.LoopOptions.MaximumDurationMinutes);

        model.LoopOptions.MaximumIterations = 24;
        model.LoopOptions.MaximumDurationMinutes = 24;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(24m, input.Value);
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
    public void DurationControlShouldExposeSafeTimerRange()
    {
        using var model = new ChatViewModel();
        var view = new ChatSettingsView { DataContext = model };
        var input = view.FindControl<NumericUpDown>("LoopMaximumDurationInput")!;

        Assert.AreEqual(0m, input.Minimum);
        Assert.AreEqual(71582m, input.Maximum);
    }
}
