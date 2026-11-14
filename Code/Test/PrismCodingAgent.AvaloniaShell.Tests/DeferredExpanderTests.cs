using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("zh-Hans")]
public sealed class DeferredExpanderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void InitiallyCollapsedShouldNotBuildDetails()
    {
        int builds = 0;
        var control = new DeferredExpander
        {
            Details = "data",
            DetailsTemplate = new FuncDataTemplate<string>((data, _) =>
            {
                builds++;
                return new TextBlock { Text = data };
            })
        };
        Assert.AreEqual(0, builds);
    }

    [TestMethod]
    public void ReopeningShouldCreateNewDetailsWithCurrentData()
    {
        var control = CreateDeferred("before");
        control.IsExpanded = true;
        object? first = control.Content;
        control.IsExpanded = false;
        control.Details = "after";
        control.IsExpanded = true;
        Assert.AreEqual((false, "after"),
            (ReferenceEquals(first, control.Content), ((Control) control.Content!).DataContext));
    }

    [TestMethod]
    public void ChangingDataWhileExpandedShouldUpdateBoundText()
    {
        var control = new DeferredExpander
        {
            Details = "before",
            DetailsTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var text = new TextBlock();
                text.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("."));
                return text;
            }),
            IsExpanded = true
        };
        control.Details = "after";
        Assert.AreEqual("after", ((TextBlock) control.Content!).Text);
    }

    [TestMethod]
    public void SettingTemplateAfterExpansionShouldCreateDetails()
    {
        var control = new DeferredExpander { IsExpanded = true, Details = "data" };
        control.DetailsTemplate = new FuncDataTemplate<string>((data, _) => new TextBlock { Text = data });
        Assert.AreEqual("data", ((TextBlock) control.Content!).Text);
    }

    [TestMethod]
    public void RemovingTemplateWhileExpandedShouldReleaseDetails()
    {
        var control = CreateDeferred("data");
        control.IsExpanded = true;
        control.DetailsTemplate = null;
        Assert.IsNull(control.Content);
    }

    [TestMethod]
    public void UpdatingDataWhileExpandedShouldRefreshBindingWithoutRebuilding()
    {
        int builds = 0;
        var data = new ObservableDetail();
        var control = new DeferredExpander
        {
            Details = data,
            DetailsTemplate = new FuncDataTemplate<ObservableDetail>((_, _) =>
            {
                builds++;
                var text = new TextBlock();
                text.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(ObservableDetail.Text)));
                return text;
            }),
            IsExpanded = true
        };
        data.Text = "updated";
        Assert.AreEqual((1, "updated"), (builds, ((TextBlock) control.Content!).Text));
    }

    [TestMethod]
    public void CollapsingShouldClearPresenterChild()
    {
        var control = CreateDeferred("data");
        var window = new Window { Content = control };
        try
        {
            window.Show();
            control.IsExpanded = true;
            Drain(window);
            control.IsExpanded = false;
            Assert.IsNull(control.Presenter!.Child);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ChangingTemplateWhileExpandedShouldReplaceDetails()
    {
        var control = CreateDeferred("data");
        control.IsExpanded = true;
        control.DetailsTemplate = new FuncDataTemplate<string>((data, _) => new TextBlock { Text = data });
        Assert.AreEqual("data", ((TextBlock) control.Content!).Text);
    }

    [TestMethod]
    public void CollapsingShouldReleasePresentedDetailsWhileDataAndOwnerRemainAlive()
    {
        var control = CreateDeferred("retained data");
        var window = new Window { Content = control };
        try
        {
            window.Show();
            WeakReference reference = ExpandAndCollapse(control, window);
            Drain(window);
            FullCollection();
            Assert.IsFalse(reference.IsAlive);
            GC.KeepAlive(control);
            GC.KeepAlive(control.Details);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public async Task RepeatedCollapsingShouldRetainMuchLessMemoryThanDefaultExpander()
    {
        const int count = 180;
        string data = "Shared detail data\n" + new string('x', 512);
        string directory = Path.Combine(AppContext.BaseDirectory, "DeferredExpanderArtifacts",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        // 先预热两种路径；测量中不把截图编码及首次主题加载算入控件内存。
        await MeasureAsync(true, 2, data, null);
        await MeasureAsync(false, 2, data, null);
        var standard = await MeasureAsync(false, count, data, directory);
        var deferred = await MeasureAsync(true, count, data, directory);
        string report = JsonSerializer.Serialize(new
        {
            Count = count, Cycles = 4, Metric = "Managed retained bytes after blocking generation-2 GC",
            Standard = standard, Deferred = deferred,
            Ratio = (double) deferred.RetainedBytes / Math.Max(1, standard.RetainedBytes)
        }, new JsonSerializerOptions { WriteIndented = true });
        string reportPath = Path.Combine(directory, "memory.json");
        File.WriteAllText(reportPath, report);
        TestContext.WriteLine(report);
        TestContext.WriteLine(directory);
        TestContext.AddResultFile(reportPath);
        Assert.IsGreaterThan(0L, deferred.RetainedBytes, report);
        Assert.IsLessThan(standard.RetainedBytes * 0.5, deferred.RetainedBytes,
            $"Expected less than half the retained managed memory. {report}");
        GC.KeepAlive(data);
    }

    private async Task<Measurement> MeasureAsync(bool deferred, int count, string data, string? directory)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var window = new Window
        {
            Width = 800, Height = 600, Background = Brushes.White,
            Content = new ScrollViewer { Content = panel }
        };
        var controls = new List<Expander>();
        var references = new List<WeakReference>();
        int builds = 0;
        var template = new FuncDataTemplate<string>((text, _) =>
        {
            builds++;
            Control child = BuildDetails(text!);
            references.Add(new WeakReference(child));
            return child;
        });
        try
        {
            window.Show();
            Drain(window);
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(100);
            Drain(window);
            FullCollection();
            long baseline = GC.GetTotalMemory(false);
            for (int i = 0; i < count; i++)
            {
                Expander control = deferred
                    ? new DeferredExpander { Details = data, DetailsTemplate = template }
                    : new Expander { Content = template.Build(data) };
                control.Header = $"Details {i + 1}";
                control.ContentTransition = null;
                controls.Add(control);
                panel.Children.Add(control);
            }
            Drain(window);
            FullCollection();
            long initial = GC.GetTotalMemory(false) - baseline;
            string prefix = deferred ? "deferred" : "standard";
            if (directory is not null) await CaptureAsync(window, directory, prefix + "-initial.png");
            for (int cycle = 0; cycle < 4; cycle++)
            {
                foreach (Expander control in controls) control.IsExpanded = true;
                Drain(window);
                if (cycle == 0 && directory is not null)
                    await CaptureAsync(window, directory, prefix + "-expanded.png");
                foreach (Expander control in controls) control.IsExpanded = false;
                Drain(window);
            }
            if (directory is not null) await CaptureAsync(window, directory, prefix + "-collapsed.png");
            // 渲染器也需要提交折叠后的场景，才能释放上一帧的详情节点。
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(60);
            Drain(window);
            FullCollection();
            long retained = GC.GetTotalMemory(false) - baseline;
            int alive = references.Count(reference => reference.IsAlive);
            Assert.AreEqual(deferred ? 0 : count, alive);
            Assert.AreEqual(deferred ? count * 4 : count, builds);
            GC.KeepAlive(controls);
            GC.KeepAlive(template);
            GC.KeepAlive(window);
            return new Measurement(baseline, initial, retained, builds, alive);
        }
        finally
        {
            window.Close();
            panel.Children.Clear();
            controls.Clear();
            window.Content = null;
            Drain(window);
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(100);
            FullCollection();
        }
    }

    private async Task CaptureAsync(Window window, string directory, string name)
    {
        window.UpdateLayout();
        await Task.Delay(60);
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless frame is unavailable.");
        string path = Path.Combine(directory, name);
        bitmap.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        TestContext.AddResultFile(path);
    }

    private static DeferredExpander CreateDeferred(string data) => new()
    {
        Details = data,
        ContentTransition = null,
        DetailsTemplate = new FuncDataTemplate<string>((text, _) => BuildDetails(text!))
    };

    private static Control BuildDetails(string data)
    {
        var panel = new StackPanel { Spacing = 6 };
        for (int i = 0; i < 8; i++)
        {
            panel.Children.Add(new TextBlock { Text = "Detail row " + i });
            var text = new TextBox { Text = data };
            text.Classes.Add("ChatOutput");
            panel.Children.Add(text);
        }
        panel.Children.Add(new ItemsControl { ItemsSource = new[] { "Result one", "Result two" } });
        var border = new Border { Child = panel };
        border.Classes.Add("ToolPanel");
        return border;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ExpandAndCollapse(DeferredExpander control, Window window)
    {
        control.IsExpanded = true;
        Drain(window);
        var reference = new WeakReference(control.Content);
        control.IsExpanded = false;
        return reference;
    }

    private static void Drain(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void FullCollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private sealed class ObservableDetail : System.ComponentModel.INotifyPropertyChanged
    {
        private string _text = "initial";
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Text)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed record Measurement(long BaselineBytes, long InitialBytes, long RetainedBytes,
        int Builds, int AliveDetails);
}
