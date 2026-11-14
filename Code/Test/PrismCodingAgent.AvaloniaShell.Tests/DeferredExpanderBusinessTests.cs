using System.Runtime.CompilerServices;
using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("zh-Hans")]
public sealed class DeferredExpanderBusinessTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ToolDetailsShouldBeDeferredAndRecreatedWithLatestNestedContent()
    {
        var tool = new CopilotChatToolItem("tool-call", "ReadFile", "input.txt", "result");
        var details = new CopilotChatMessage(ChatRole.Assistant, "first detail");
        tool.Details = details;
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer");
        message.MessageItems.Add(tool);
        using var projection = new MessageItemViewModel(message);
        var (window, host) = CreateWindow(projection);
        try
        {
            window.Show();
            Drain(window);
            var expander = host.GetVisualDescendants().OfType<DeferredExpander>().Single();
            Assert.IsNull(expander.Content);
            await CaptureAsync(window, "tool-collapsed");
            Toggle(window, expander);
            Assert.HasCount(1, expander.GetVisualDescendants().OfType<TextBox>()
                .Where(text => text.Text == "first detail"));
            await CaptureAsync(window, "tool-expanded");
            WeakReference reference = Collapse(expander);
            details.MessageItems.Add(new CopilotChatTextItem("added while collapsed"));
            await SettleAndCollectAsync(window);
            Assert.IsFalse(reference.IsAlive);
            Toggle(window, expander);
            Assert.HasCount(1, expander.GetVisualDescendants().OfType<TextBox>()
                .Where(text => text.Text == "added while collapsed"));
            await CaptureAsync(window, "tool-reopened");
            GC.KeepAlive(tool);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public async Task CollapsingSubAgentShouldReleaseNestedToolDetailsAndRestoreLatestMessages()
    {
        var subAgent = new CopilotChatSubAgentItem("agent-call", "Explore", "task", "done");
        subAgent.MessageItems.Add(new CopilotChatToolItem("nested-call", "Search", "query", "found"));
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer");
        message.MessageItems.Add(subAgent);
        using var projection = new MessageItemViewModel(message);
        var (window, host) = CreateWindow(projection);
        try
        {
            window.Show();
            Drain(window);
            var expanders = host.GetVisualDescendants().OfType<DeferredExpander>().ToArray();
            Assert.HasCount(2, expanders);
            var parent = expanders[0];
            Assert.IsTrue(parent.IsExpanded);
            var nested = expanders[1];
            Assert.IsFalse(nested.IsExpanded);
            nested.IsExpanded = true;
            Drain(window);
            await CaptureAsync(window, "subagent-expanded");
            // 测试本身不能持有嵌套控件，其逻辑父级会反向持有整棵详情树。
            nested = null;
            expanders = null;
            WeakReference reference = Collapse(parent);
            subAgent.AppendText("new child message");
            await SettleAndCollectAsync(window);
            Assert.IsFalse(reference.IsAlive);
            Assert.HasCount(1, host.GetVisualDescendants().OfType<DeferredExpander>());
            await CaptureAsync(window, "subagent-collapsed");
            Toggle(window, parent);
            Assert.HasCount(1, parent.GetVisualDescendants().OfType<TextBox>()
                .Where(text => text.Text == "new child message"));
            GC.KeepAlive(subAgent);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public async Task CollapsingInnerSubAgentShouldKeepOuterDetailsAndSiblingTool()
    {
        var outer = new CopilotChatSubAgentItem("outer", "Outer agent", "outer task");
        var inner = new CopilotChatSubAgentItem("inner", "Inner agent", "inner task");
        inner.MessageItems.Add(new CopilotChatToolItem("leaf", "ReadFile", "file.txt", "leaf result"));
        outer.MessageItems.Add(inner);
        outer.MessageItems.Add(new CopilotChatToolItem("sibling", "Search", "query", "sibling result"));
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer");
        message.MessageItems.Add(outer);
        using var projection = new MessageItemViewModel(message);
        var (window, host) = CreateWindow(projection);
        try
        {
            window.Show();
            Drain(window);
            var parent = FindExpander(host, outer);
            var child = FindExpander(host, inner);
            object? outerDetails = parent.Content;
            WeakReference reference = ExpandNestedToolAndCollapse(child, window);
            await SettleAndCollectAsync(window);
            Assert.IsFalse(reference.IsAlive);
            Assert.AreSame(outerDetails, parent.Content);
            Assert.IsTrue(parent.IsExpanded);
            Assert.HasCount(3, host.GetVisualDescendants().OfType<DeferredExpander>());
            await CaptureAsync(window, "inner-subagent-collapsed-sibling-retained");
            inner.AppendText("latest inner message");
            Toggle(window, child);
            Assert.HasCount(1, child.GetVisualDescendants().OfType<TextBox>()
                .Where(text => text.Text == "latest inner message"));
            await CaptureAsync(window, "inner-subagent-reopened");
            GC.KeepAlive(outer);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public async Task RepeatedCollapsingOuterSubAgentShouldReleaseAllNestedDetails()
    {
        var outer = new CopilotChatSubAgentItem("outer", "Outer agent", "outer task");
        var inner = new CopilotChatSubAgentItem("inner", "Inner agent", "inner task");
        inner.MessageItems.Add(new CopilotChatToolItem("leaf", "ReadFile", "file.txt", "leaf result"));
        outer.MessageItems.Add(inner);
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer");
        message.MessageItems.Add(outer);
        using var projection = new MessageItemViewModel(message);
        var (window, host) = CreateWindow(projection);
        try
        {
            window.Show();
            Drain(window);
            var parent = FindExpander(host, outer);
            var references = new List<WeakReference>();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                ExpandNestedTree(parent, window);
                await CaptureAsync(window, $"nested-expanded-{cycle}");
                references.AddRange(CollapseNestedTree(parent));
                await SettleAndCollectAsync(window);
                Assert.HasCount(0, references.Where(reference => reference.IsAlive));
                Assert.HasCount(1, host.GetVisualDescendants().OfType<DeferredExpander>());
            }
            inner.AppendText("added after repeated collapse");
            Toggle(window, parent);
            Assert.HasCount(1, host.GetVisualDescendants().OfType<TextBox>()
                .Where(text => text.Text == "added after repeated collapse"));
            await CaptureAsync(window, "nested-restored-latest-data");
            GC.KeepAlive(outer);
        }
        finally { window.Close(); }
    }

    private static DeferredExpander FindExpander(Control host, object data) =>
        host.GetVisualDescendants().OfType<DeferredExpander>()
            .Single(control => ReferenceEquals(control.Details, data));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ExpandNestedToolAndCollapse(DeferredExpander inner, Window window)
    {
        inner.GetVisualDescendants().OfType<DeferredExpander>().Single().IsExpanded = true;
        Drain(window);
        return Collapse(inner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExpandNestedTree(DeferredExpander parent, Window window)
    {
        parent.IsExpanded = true;
        Drain(window);
        var inner = parent.GetVisualDescendants().OfType<DeferredExpander>()
            .Single(control => control.Details is CopilotChatSubAgentItem);
        inner.GetVisualDescendants().OfType<DeferredExpander>().Single().IsExpanded = true;
        Drain(window);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CollapseNestedTree(DeferredExpander parent)
    {
        var references = parent.GetVisualDescendants().OfType<DeferredExpander>()
            .Select(control => new WeakReference(control.Content)).Prepend(new WeakReference(parent.Content)).ToArray();
        parent.IsExpanded = false;
        return references;
    }

    private static (Window Window, ContentControl Host) CreateWindow(MessageItemViewModel projection)
    {
        var view = new ChatView();
        var host = new ContentControl
        {
            Content = projection,
            ContentTemplate = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate,
            Margin = new Thickness(24)
        };
        view.Content = host;
        return (new Window { Width = 900, Height = 700, Content = view }, host);
    }

    private static void Toggle(Window window, DeferredExpander expander)
    {
        var toggle = expander.GetVisualDescendants().OfType<ToggleButton>().First();
        Point point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Expander header is not attached to the test window.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Drain(window);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Collapse(DeferredExpander expander)
    {
        var reference = new WeakReference(expander.Content);
        expander.IsExpanded = false;
        return reference;
    }

    private async Task CaptureAsync(Window window, string name)
    {
        await Task.Delay(300);
        Drain(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        string directory = Path.Combine(AppContext.BaseDirectory, "DeferredExpanderBusinessArtifacts",
            TestContext.TestName!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".png");
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless business frame is unavailable.");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        TestContext.WriteLine(path);
        TestContext.AddResultFile(path);
    }

    private static async Task SettleAndCollectAsync(Window window)
    {
        await Task.Delay(300);
        Drain(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        await Task.Delay(100);
        Drain(window);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static void Drain(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
