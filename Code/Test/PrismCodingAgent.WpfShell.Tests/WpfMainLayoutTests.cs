using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfMainLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ToolbarRespondsToWindowWidthWithoutChangingSelectionsAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "ToolbarLayoutData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"gpt-5.4","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"gpt-5.4","ModelId":"gpt-5.4","Provider":"openai"}]}]}""");
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        window.Closing += (_, args) => args.Cancel = false;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            // Only this test host permits sizes below the production minimum.
            window.MinWidth = 0;
            window.MinHeight = 0;
            var toolbar = (Grid)window.FindName("ConversationToolbar");
            var workspace = (TextBox)window.FindName("WorkspaceInput");
            var apply = (Button)window.FindName("ApplyWorkspaceButton");
            var selector = (ComboBox)window.FindName("ModelSelector");
            var reasoning = (ComboBox)window.FindName("ReasoningEffortSelector");
            var history = (Button)window.FindName("TaskHistoryButton");
            var log = (Button)window.FindName("OpenLogDirectoryButton");
            var selectedModel = model.ChatViewModel.SelectedModel;
            Assert.IsNotNull(selectedModel);
            double toolbarHeight = 0;
            double normalDirectoryWidth = 0;

            async Task ResizeAsync(int width, int height, bool showModels)
            {
                window.Width = width;
                window.Height = height;
                await screenshots.CaptureSurfaceAsync(window, $"toolbar-window-{width}x{height}");
                Assert.AreEqual((double)width, window.ActualWidth, 1d);
                Assert.AreEqual((double)height, window.ActualHeight, 1d);
                Assert.AreEqual(showModels, selector.IsVisible);
                Assert.AreEqual(showModels, reasoning.IsVisible);
                Assert.AreEqual(showModels, ((TextBlock)window.FindName("ConversationStatus")).IsVisible);
                Assert.AreEqual(showModels, ((TextBlock)window.FindName("ReasoningEffortLabel")).IsVisible);
                Assert.IsTrue(workspace.IsVisible && apply.IsVisible && history.IsVisible && log.IsVisible);
                Assert.IsGreaterThan(100d, workspace.ActualWidth);
                Assert.AreEqual(toolbarHeight == 0 ? toolbar.ActualHeight : toolbarHeight, toolbar.ActualHeight, 0.1d);
                toolbarHeight = toolbar.ActualHeight;
                Rect Bounds(FrameworkElement element) => new(element.TranslatePoint(default, toolbar), element.RenderSize);
                Assert.IsLessThanOrEqualTo(Bounds(apply).Left, Bounds(workspace).Right);
                Assert.IsLessThanOrEqualTo(Bounds(history).Left, Bounds(apply).Right);
                Assert.IsLessThanOrEqualTo(Bounds(log).Left, Bounds(history).Right);
                Assert.IsLessThanOrEqualTo(toolbar.ActualWidth + 0.1d, Bounds(log).Right);
                Assert.AreEqual(Bounds(workspace).Top, Bounds(history).Top, 0.1d);
                Assert.AreSame(selectedModel, selector.SelectedItem);
            }

            await ResizeAsync(1200, 900, true);
            normalDirectoryWidth = workspace.ActualWidth;
            Assert.AreEqual(220d, selector.ActualWidth, 0.1d);
            selector.ApplyTemplate();
            Assert.IsNotNull(selector.Template.FindName("PART_Popup", selector));
            Assert.AreSame(model.ChatViewModel.AvailableModels, selector.ItemsSource);
            Assert.AreEqual(System.Windows.Data.BindingMode.TwoWay,
                System.Windows.Data.BindingOperations.GetBinding(selector, ComboBox.SelectedItemProperty)!.Mode);
            reasoning.SetCurrentValue(ComboBox.SelectedItemProperty, model.ChatViewModel.AvailableReasoningEfforts.Last());
            Assert.AreSame(reasoning.SelectedItem, model.ChatViewModel.SelectedReasoningEffort);
            var selectedEffort = reasoning.SelectedItem;
            await ResizeAsync(1440, 900, true);
            Assert.AreEqual(normalDirectoryWidth + 240, workspace.ActualWidth, 0.1d);
            await ResizeAsync(800, 600, false);
            await ResizeAsync(1199, 900, false);
            Assert.AreEqual(normalDirectoryWidth + 323, workspace.ActualWidth, 0.1d);
            await ResizeAsync(1200, 900, true);
            Assert.AreSame(selectedEffort, reasoning.SelectedItem);
            Assert.AreEqual(normalDirectoryWidth, workspace.ActualWidth, 0.1d);
        }
        finally
        {
            window.Close();
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task RealStartupRendersShellAndTaskInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "MainLayoutData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"),
            JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            var content = (FrameworkElement)window.Content;
            // Match Avalonia client size, not the decorated native window size.
            window.UpdateLayout();
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            await screenshots.CaptureAsync("main-1440x900");
            Assert.AreEqual(1440d, content.ActualWidth, 1d);
            Assert.AreEqual(900d, content.ActualHeight, 1d);
            Assert.AreEqual(Colors.White, ((SolidColorBrush)((Grid)content).Background).Color);
            Assert.AreEqual(32d, ((TextBox)window.FindName("TaskSearch")).ActualHeight, 1d);
            var surface = (Border)window.FindName("ContentSurface");
            Assert.AreEqual(280d, surface.TranslatePoint(default, content).X, 1d);
            Assert.IsTrue(((UserControl)window.FindName("ModelGuide")).IsVisible);
            Assert.IsTrue(((Button)window.FindName("SettingsButton")).IsEnabled);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "main-layout.txt"),
                $"{Lang.Current.IetfLanguageTag}; client={content.ActualWidth}x{content.ActualHeight}; dpi={VisualTreeHelper.GetDpi(content).PixelsPerInchX}; sidebar=280; appData={root}{Environment.NewLine}");

            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchX);
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchY);
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
            await screenshots.CaptureAsync("settings");
            await WpfInput.ClickAsync(Descendants(window).OfType<Button>().Single(button => button.Name == "BackButton" && button.IsVisible));
            await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Settings did not close.");
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "main-layout.txt"),
                $"native={window.ActualWidth}x{window.ActualHeight}; decoration={window.ActualWidth - content.ActualWidth}x{window.ActualHeight - content.ActualHeight}{Environment.NewLine}");
            window.Width += 1280 - content.ActualWidth;
            window.Height += 720 - content.ActualHeight;
            await screenshots.CaptureAsync("main-1280x720");
            Assert.AreEqual(1280d, content.ActualWidth, 1d);
            Assert.AreEqual(720d, content.ActualHeight, 1d);
            Assert.IsGreaterThanOrEqualTo(990, surface.ActualWidth);

            Guid original = model.ActiveWorkTask!.Id;
            await WpfInput.ClickAsync((Button)window.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => model.WorkTasks.Count == 2, "New task did not complete.");
            Assert.AreNotEqual(original, model.ActiveWorkTask!.Id);
            var items = (ItemsControl)window.FindName("TaskItems");
            window.UpdateLayout();
            Button first = Descendants(items).OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, model.ActivateWorkTaskCommand)
                    && button.DataContext is WorkTaskItemViewModel task && task.Id == original);
            await WpfInput.ClickAsync(first);
            await WpfInput.WaitAsync(() => model.ActiveWorkTask!.Id == original, "Task selection did not activate original task.");
            await WpfInput.ClickAsync((TextBox)window.FindName("TaskSearch"));
            WpfInput.Text("no-matching-task-4831");
            await WpfInput.WaitAsync(() => model.IsWorkTaskSearchEmpty, "Search did not filter tasks.");
            await screenshots.CaptureAsync("search-empty");
            ((TextBox)window.FindName("TaskSearch")).Clear();
            await WpfInput.WaitAsync(() => model.FilteredWorkTasks.Count == 2, "Clearing search did not restore tasks.");
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Layout window did not close.");
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task TasksCreateSwitchSearchThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "TaskNavigationData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"Offline navigation model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Offline navigation model","ModelId":"offline-navigation","Provider":"openai"}]}]}""");
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            window.UpdateLayout();
            Assert.AreEqual(1440d, content.ActualWidth, 1d);
            Assert.AreEqual(900d, content.ActualHeight, 1d);
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchX);
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchY);
            var original = model.ActiveWorkTask;
            await screenshots.CaptureAsync("main-window");
            await WpfInput.ClickAsync((Button)window.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => model.WorkTasks.Count == 2, "Task creation did not finish.");
            var created = model.ActiveWorkTask;
            Assert.AreNotSame(original, created);
            await screenshots.CaptureAsync("task-created");
            Button TaskButton(WorkTaskItemViewModel task) => Descendants(window).OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, model.ActivateWorkTaskCommand) && ReferenceEquals(button.DataContext, task));
            await WpfInput.ClickAsync(TaskButton(original));
            await WpfInput.WaitAsync(() => ReferenceEquals(model.ActiveWorkTask, original), "Original task did not activate.");
            await screenshots.CaptureAsync("switched-to-original");
            await WpfInput.ClickAsync(TaskButton(created));
            await WpfInput.WaitAsync(() => ReferenceEquals(model.ActiveWorkTask, created), "Created task did not activate.");
            await screenshots.CaptureAsync("switched-to-created");
            var search = (TextBox)window.FindName("TaskSearch");
            await WpfInput.ClickAsync(search);
            WpfInput.Text(original.DisplayName);
            await WpfInput.WaitAsync(() => model.FilteredWorkTasks.Count == 1, "Task search did not match.");
            Assert.AreSame(original, model.FilteredWorkTasks.Single());
            await screenshots.CaptureAsync("search-match");
            WpfInput.Text("-no-such-task");
            await WpfInput.WaitAsync(() => model.IsWorkTaskSearchEmpty, "Task search did not become empty.");
            Assert.AreSame(created, model.ActiveWorkTask);
            await screenshots.CaptureAsync("search-empty");
            WpfInput.Key(0x24);
            for (int i = 0, length = search.Text.Length; i < length; i++) WpfInput.Key(0x2E);
            await WpfInput.WaitAsync(() => search.Text.Length == 0, "Search did not clear through keyboard.");
            await WpfInput.WaitAsync(() => model.FilteredWorkTasks.Count == 2, "Clearing task search did not restore tasks.");
            Assert.AreEqual(string.Empty, search.Text);
            Assert.AreSame(created, model.ActiveWorkTask);
            await screenshots.CaptureAsync("search-cleared");
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Task window did not close.");
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    [DataRow("WorkspaceInput")]
    [DataRow("TaskSearch")]
    public async Task SingleLineInputDoesNotClipTextAsync(string inputName) => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var input = (TextBox)window.FindName(inputName);
            input.Text = @"C:\Workspace\PrismCodingAgent 工作目录";
            window.UpdateLayout();
            var host = (ScrollViewer)input.Template.FindName("PART_ContentHost", input);
            Rect character = input.GetRectFromCharacterIndex(0);

            Assert.IsTrue(host.ViewportHeight >= character.Height && character.Height > 0,
                $"Text viewport {host.ViewportHeight} must accommodate line height {character.Height}.");
        }
        finally
        {
            window.Close();
            ((Button)window.FindName("ConfirmExitButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }
        return Task.CompletedTask;
    });

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
