using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using Lang = PrismCodingAgent.AvaloniaShell.Localizations.Lang;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfMessageUsageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublicProjectionShowsAndUpdatesLocalizedUsageAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Show();
        resources.Close();
        await WpfInput.ClickAsync((Button)resources.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !resources.IsVisible, "Resource window did not close.");
        var message = new CopilotChatMessage(ChatRole.Assistant, "Usage projection / 用量公开投影");
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24) };
        var surface = new Grid();
        surface.Children.Add(content);
        var window = new Window { Width = 800, Height = 360, Content = surface, Background = Brushes.White };
        try
        {
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("usage-absent-public-projection");
            var summary = Descendants(content).OfType<TextBlock>().Single(text => text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path == nameof(MessageItemViewModel.UsageSummaryText));
            Assert.IsFalse(summary.IsVisible);
            message.AppendUsageDetails(new UsageDetails { InputTokenCount = 12, OutputTokenCount = 8, TotalTokenCount = 20 });
            await WpfInput.WaitAsync(() => summary.IsVisible && summary.Text == projection.UsageSummaryText, "Usage did not appear.");
            string expected = $"{Lang.Current.UsageCurrent.ToString(20)} {Lang.Current.UsageTotal.ToString(20)} | {Lang.Current.UsageInput.ToString(12)} | {Lang.Current.UsageOutput.ToString(8)}";
            Assert.AreEqual(expected, summary.Text);
            await screenshots.CaptureAsync("usage-first-response-public-projection");
            message.AppendUsageDetails(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 });
            string updated = $"{Lang.Current.UsageCurrent.ToString(5)} {Lang.Current.UsageTotal.ToString(25)} | {Lang.Current.UsageInput.ToString(15)} | {Lang.Current.UsageOutput.ToString(10)}";
            await WpfInput.WaitAsync(() => summary.Text == updated, "Usage did not update.");
            Assert.AreEqual(updated, summary.Text);
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Rect bounds = summary.TransformToAncestor(surface).TransformBounds(new Rect(summary.RenderSize));
            Assert.IsTrue(bounds.Left >= 24 && bounds.Right <= surface.ActualWidth - 24 && bounds.Bottom <= surface.ActualHeight - 24, $"Usage bounds exceed surface: {bounds}.");
            await screenshots.CaptureAsync("usage-updated-public-projection-not-model-execution");
        }
        finally
        {
            window.Close();
        }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
