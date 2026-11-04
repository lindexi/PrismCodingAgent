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
    public async Task RealStartupRendersShellAndTaskInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase4Data", Guid.NewGuid().ToString("N"));
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
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "phase4-layout.txt"),
                $"{Lang.Current.IetfLanguageTag}; client={content.ActualWidth}x{content.ActualHeight}; dpi={VisualTreeHelper.GetDpi(content).PixelsPerInchX}; sidebar=280; appData={root}{Environment.NewLine}");

            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchX);
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchY);
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
            await screenshots.CaptureAsync("settings");
            await WpfInput.ClickAsync(Descendants(window).OfType<Button>().Single(button => button.Name == "BackButton" && button.IsVisible));
            await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Settings did not close.");
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "phase4-layout.txt"),
                $"native={window.ActualWidth}x{window.ActualHeight}; decoration={window.ActualWidth - content.ActualWidth}x{window.ActualHeight - content.ActualHeight}{Environment.NewLine}");
            window.Width += 1280 - content.ActualWidth;
            window.Height += 720 - content.ActualHeight;
            await screenshots.CaptureAsync("main-1280x720");
            Assert.AreEqual(1280d, content.ActualWidth, 1d);
            Assert.AreEqual(720d, content.ActualHeight, 1d);
            Assert.IsTrue(surface.ActualWidth >= 990);

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
    public async Task Ss02TasksCreateSwitchSearchThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase9TaskData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"SS05 offline model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"SS05 offline model","ModelId":"ss05-offline","Provider":"openai"}]}]}""");
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

    [DataTestMethod]
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
