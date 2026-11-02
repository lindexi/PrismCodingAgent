using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfChatScrollTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublicProjectionFollowsPausesAndResumesThroughRealWheelAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Show();
        resources.Close();
        await WpfInput.ClickAsync((Button)resources.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !resources.IsVisible, "Resource window did not close.");
        var viewer = (ScrollViewer)resources.FindName("MessagesScrollViewer");
        ((Panel)viewer.Parent).Children.Remove(viewer);
        viewer.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) =>
            TestContext.WriteLine($"BringIntoView source={e.OriginalSource.GetType().Name}; target={e.TargetObject.GetType().Name}; rect={e.TargetRect}; offset={viewer.VerticalOffset}; focus={System.Windows.Input.Keyboard.FocusedElement?.GetType().Name}")), true);
        viewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) =>
            TestContext.WriteLine($"Scroll source={e.OriginalSource.GetType().Name}; outer={ReferenceEquals(e.OriginalSource, viewer)}; delta={e.VerticalChange}; extentDelta={e.ExtentHeightChange}; viewportDelta={e.ViewportHeightChange}; offset={viewer.VerticalOffset}; extent={viewer.ExtentHeight}")), true);
        var items = (ItemsControl)resources.FindName("HistoryMessages");
        var message = new CopilotChatMessage(ChatRole.Assistant, Lines(60));
        using var projection = new MessageItemViewModel(message);
        items.ItemsSource = new ObservableCollection<ChatEntryViewModel> { projection };
        var surface = new Grid { Background = Brushes.White };
        surface.Children.Add(viewer);
        var window = new Window { Width = 820, Height = 520, Content = surface };
        try
        {
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("initial-layout-diagnostic");
            TestContext.WriteLine($"Initial extent={viewer.ExtentHeight}; viewport={viewer.ViewportHeight}; offset={viewer.VerticalOffset}; items={items.Items.Count}; visible={viewer.IsVisible}; size={viewer.ActualWidth}x{viewer.ActualHeight}");
            await WpfInput.WaitAsync(() => viewer.ScrollableHeight > 300 && AtBottom(viewer), "Initial long message did not follow tail.");
            await screenshots.CaptureAsync("scroll-before-public-projection-tail");
            double initialExtent = viewer.ExtentHeight;
            message.AppendText(Lines(6));
            await WpfInput.WaitAsync(() => viewer.ExtentHeight > initialExtent && AtBottom(viewer), "Appended text did not follow tail.");
            for (int notch = 0; notch < 4; notch++)
            {
                double beforeWheel = viewer.VerticalOffset;
                await WpfInput.WheelAsync(viewer, 120);
                await WpfInput.WaitAsync(() => viewer.VerticalOffset < beforeWheel, "Real wheel did not scroll up.");
            }
            double pausedOffset = viewer.VerticalOffset;
            double pausedExtent = viewer.ExtentHeight;
            message.AppendText(Lines(6));
            await WpfInput.WaitAsync(() => viewer.ExtentHeight > pausedExtent, "Paused append did not reach layout.");
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(pausedOffset, viewer.VerticalOffset, 1, "Appending pulled the user away from older text.");
            await screenshots.CaptureAsync("scroll-user-up-append-keeps-position");
            while (!AtBottom(viewer))
            {
                double beforeWheel = viewer.VerticalOffset;
                await WpfInput.WheelAsync(viewer, -120);
                await WpfInput.WaitAsync(() => viewer.VerticalOffset > beforeWheel, "Real wheel did not return to tail.");
            }
            double resumedExtent = viewer.ExtentHeight;
            message.AppendText(Lines(6));
            await WpfInput.WaitAsync(() => viewer.ExtentHeight > resumedExtent && AtBottom(viewer), "Following did not resume.");
            var details = new CopilotChatMessage(ChatRole.Tool, Lines(18) + "END OF TOOL / 工具末尾");
            details.MessageItems.Add(new CopilotChatImageItem(WpfMessageImageTests.CreateImage(), "image/png"));
            message.MessageItems.Add(new CopilotChatToolItem("scroll-fixture", "Expand details / 展开详情", "Public projection, no execution / 公开投影非执行") { Details = details });
            await WpfInput.WaitAsync(() => Descendants(items).OfType<Expander>().Any(), "Tool header did not appear.");
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var expander = Descendants(items).OfType<Expander>().Single();
            await WpfInput.ClickAsync(Descendants(expander).OfType<ToggleButton>().First());
            await WpfInput.WaitAsync(() => expander.IsExpanded && AtBottom(viewer), "Expanded long tool did not remain reachable.");
            await screenshots.CaptureAsync("scroll-after-expanded-tool-tail");
            var image = Descendants(expander).OfType<PrismCodingAgent.AvaloniaShell.Views.BinaryImage>().Single();
            Rect bounds = image.TransformToAncestor(viewer).TransformBounds(new Rect(image.RenderSize));
            Assert.IsTrue(bounds.Top >= 0 && bounds.Bottom <= viewer.ActualHeight && bounds.Left >= 0 && bounds.Right <= viewer.ActualWidth, $"Expanded image is not reachable: {bounds}");
            Assert.IsTrue(Descendants(expander).OfType<TextBox>().Any(text => text.Text.Contains("END OF TOOL") && text.IsVisible));
        }
        finally
        {
            window.Close();
        }
    });

    private static bool AtBottom(ScrollViewer viewer) => viewer.ScrollableHeight - viewer.VerticalOffset < 1;
    private static string Lines(int count) => string.Concat(Enumerable.Range(1, count).Select(index => $"Line {index:D2} / 长消息第{index:D2}行 — public projection, not model execution 公开投影非模型执行\n"));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
