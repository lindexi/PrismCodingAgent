using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;
using PrismCodingAgent.AvaloniaShell.Abilities;
using PrismCodingAgent.AvaloniaShell.Infrastructure;
using PrismCodingAgent.AvaloniaShell.Services.WorkTasks;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>使用真实运行时和独立数据目录驱动主窗口，保留各步骤截图供人工检查。</summary>
internal sealed class ShellScreenshotSession : IAsyncDisposable
{
    private readonly TestContext _testContext;
    private int _step;

    private ShellScreenshotSession(MainWindow window, MainViewModel model, string directory, TestContext testContext)
    {
        Window = window;
        Model = model;
        DirectoryPath = directory;
        _testContext = testContext;
    }

    public MainWindow Window { get; }
    public MainViewModel Model { get; }
    public string DirectoryPath { get; }

    public static async Task<ShellScreenshotSession> OpenAsync(TestContext testContext)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots",
            testContext.TestName!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        testContext.WriteLine($"UI artifacts: {directory}");
        var paths = CodingChatRoomPaths.Create(Path.Combine(directory, "AppData"));
        var dispatcher = new AvaloniaMainThreadDispatcher();
        await DefaultAbilityInstaller.InstallOnceAsync(paths.AbilitiesDirectory);
        var runtime = await CodingWorkTaskRuntimeFactory.InitializeAsync(paths, dispatcher);
        var abilities = new AbilityCatalog(paths.AbilitiesDirectory);
        await abilities.RefreshAsync();
        var task = MainViewModel.CreateRuntimeTask(runtime,
            new WorkTaskRecord(Guid.NewGuid(), "工作任务 1", null, null, null), abilities);
        var model = MainViewModel.Create([task], [], runtime.SettingsService, new WorkTaskStore(paths),
            () => CodingWorkTaskRuntimeFactory.InitializeAsync(paths, dispatcher), abilities);
        await model.InitializeSettingsAsync();
        var window = new MainWindow { DataContext = model };
        window.Show();
        return new ShellScreenshotSession(window, model, directory, testContext);
    }

    /// <summary>仅通过真实输入手动保存离线模型，不刷新模型列表或发送消息。</summary>
    public async Task ConfigureOfflineModelAsync()
    {
        var main = Window.GetVisualDescendants().OfType<MainView>().Single();
        var setup = main.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(control => control.Name == "ModelSetupScrollViewer");
        Capture("offline-model-guide");
        Assert.IsTrue(setup.IsEffectivelyVisible);
        var editor = setup.GetVisualDescendants().OfType<ModelSettingsView>().Single();
        var connection = editor.GetVisualDescendants().OfType<ModelServiceConnectionView>().Single();
        var catalog = editor.GetVisualDescendants().OfType<OpenAIModelCatalogView>().Single();

        void ScrollTo(Control control)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                Window.UpdateLayout();
                Point position = control.TranslatePoint(default, setup)
                    ?? throw new InvalidOperationException("Setup control is not attached.");
                if (position.Y >= 0 && position.Y + control.Bounds.Height <= setup.Bounds.Height)
                {
                    return;
                }

                Point wheelPoint = setup.TranslatePoint(new Point(setup.Bounds.Width - 30,
                    setup.Bounds.Height / 2), Window)
                    ?? throw new InvalidOperationException("Setup scroll viewer is not attached.");
                Window.MouseMove(wheelPoint);
                Window.MouseWheel(wheelPoint, new Vector(0, position.Y < 0 ? 3 : -3));
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Fail("Could not scroll the setup control into view using the mouse wheel.");
        }

        void ReplaceText(TextBox input, string value, string step)
        {
            ScrollTo(input);
            Click(input);
            Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
            Window.KeyTextInput(value);
            Capture(step);
            Assert.AreEqual(value, input.Text);
        }

        ReplaceText(connection.GetVisualDescendants().OfType<TextBox>()
            .Single(input => input.PlaceholderText == "https://api.openai.com/v1"),
            "http://127.0.0.1:1/v1", "offline-endpoint");
        ReplaceText(connection.GetVisualDescendants().OfType<SecretInput>().Single(),
            "screenshot-only-key", "offline-key");
        ReplaceText(catalog.GetVisualDescendants().OfType<TextBox>().Single(),
            "screenshot-offline-model", "offline-model-name");
        Button add = catalog.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, Lang.Current.SetupAddModel));
        ScrollTo(add);
        Click(add);
        Capture("offline-model-added");
        Assert.AreEqual(1, editor.GetVisualDescendants().OfType<Border>()
            .Count(border => border.Classes.Contains("ModelCard")));
        Button save = main.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && button.Classes.Contains("SaveSettings"));
        await ClickAndWaitAsync(save);
        Capture("offline-chat-ready");
        Assert.IsTrue(Model.IsChatOpen);
        Assert.IsTrue(main.GetVisualDescendants().OfType<ChatView>().Single().IsEffectivelyVisible);
    }

    public string Capture(string stepName)
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        using var frame = Window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless renderer did not produce a frame.");
        string path = Path.Combine(DirectoryPath, $"{++_step:00}-{stepName}.png");
        frame.Save(path, new PngBitmapEncoderOptions());
        _testContext.AddResultFile(path);
        _testContext.WriteLine(path);
        return path;
    }

    public void Click(Control control)
    {
        Window.UpdateLayout();
        Point point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException("The control is not attached to the test window.");
        Window.MouseMove(point);
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    public async Task ClickAndWaitAsync(Button button)
    {
        var command = button.Command
            ?? throw new InvalidOperationException("The button has no command.");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCanExecuteChanged(object? sender, EventArgs args)
        {
            if (command.CanExecute(button.CommandParameter))
            {
                completion.TrySetResult();
            }
        }

        command.CanExecuteChanged += OnCanExecuteChanged;
        try
        {
            Click(button);
            if (command.CanExecute(button.CommandParameter))
            {
                completion.TrySetResult();
            }

            await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            command.CanExecuteChanged -= OnCanExecuteChanged;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Model.DisposeAsync();
        Window.DataContext = null;
        if (Window.IsVisible)
        {
            Window.Close();
            Click(Window.FindControl<Button>("ConfirmExitButton")!);
        }
    }
}
