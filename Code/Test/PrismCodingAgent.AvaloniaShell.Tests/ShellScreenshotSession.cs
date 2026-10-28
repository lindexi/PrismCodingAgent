using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
