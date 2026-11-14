using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using AgentLib.Model;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfDeferredExpanderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CollapsedDetailsShouldBeDeferredAndRecreatedAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var control = new DeferredExpander { Details = "before", DetailsTemplate = TextTemplate() };
        var window = new Window { Width = 600, Height = 400, Content = control };
        try
        {
            window.Show();
            Assert.IsNull(control.Content);
            control.IsExpanded = true;
            await SettleAsync(window);
            Assert.AreEqual("before", ((TextBlock)control.Content).Text);
            WeakReference reference = Collapse(control);
            control.Details = "after";
            await CollectAsync(window);
            Assert.IsFalse(reference.IsAlive);
            control.IsExpanded = true;
            await SettleAsync(window);
            Assert.AreEqual("after", ((TextBlock)control.Content).Text);
            GC.KeepAlive(control);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public async Task ProductionSubAgentShouldReleaseNestedToolAndRestoreLatestMessagesAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Show();
        resources.Close();
        await WpfInput.ClickAsync((Button)resources.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !resources.IsVisible, "Resource window did not close.");
        var outer = new CopilotChatSubAgentItem("outer", "Outer agent", "task");
        var inner = new CopilotChatSubAgentItem("inner", "Inner agent", "nested task");
        inner.MessageItems.Add(new CopilotChatToolItem("tool", "ReadFile", "file.txt", "result"));
        outer.MessageItems.Add(inner);
        var host = new ItemsControl
        {
            ItemsSource = new[] { outer },
            ItemTemplateSelector = (DataTemplateSelector)resources.Resources["MessageParts"]
        };
        var window = new Window
        {
            Width = 900, Height = 700, Content = host,
            Background = Brushes.White
        };
        try
        {
            TestContext.WriteLine("Showing nested template window.");
            window.Show();
            await SettleAsync(window);
            TestContext.WriteLine("Nested template window laid out.");
            var parent = Descendants(host).OfType<DeferredExpander>()
                .Single(control => ReferenceEquals(control.Details, outer));
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                ExpandNested(parent);
                await SettleAsync(window);
                await screenshots.CaptureAsync($"nested-expanded-{cycle}");
                WeakReference[] references = CollapseTree(parent);
                await CollectAsync(window);
                Assert.HasCount(0, references.Where(reference => reference.IsAlive));
                Assert.HasCount(1, Descendants(host).OfType<DeferredExpander>());
                parent.IsExpanded = true;
                await SettleAsync(window);
            }
            parent.IsExpanded = false;
            inner.AppendText("latest nested message");
            parent.IsExpanded = true;
            await SettleAsync(window);
            Assert.HasCount(1, Descendants(host).OfType<TextBox>()
                .Where(text => text.Text == "latest nested message"));
            await screenshots.CaptureAsync("nested-restored");
            GC.KeepAlive(outer);
        }
        finally
        {
            TestContext.WriteLine("Closing nested template window.");
            window.Close();
            TestContext.WriteLine("Nested template window closed.");
        }
    });

    private static DataTemplate TextTemplate() => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}'/></DataTemplate>");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Collapse(DeferredExpander control)
    {
        var reference = new WeakReference(control.Content);
        control.IsExpanded = false;
        return reference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExpandNested(DeferredExpander parent)
    {
        foreach (var control in Descendants(parent).OfType<DeferredExpander>().ToArray())
            control.IsExpanded = true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CollapseTree(DeferredExpander parent)
    {
        var references = Descendants(parent).OfType<DeferredExpander>()
            .Select(control => new WeakReference(control.Content)).Prepend(new WeakReference(parent.Content)).ToArray();
        parent.IsExpanded = false;
        return references;
    }

    private static async Task SettleAsync(Window window)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    }

    private static async Task CollectAsync(Window window)
    {
        await SettleAsync(window);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
