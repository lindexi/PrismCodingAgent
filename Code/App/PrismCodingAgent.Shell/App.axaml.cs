using System;
using System.Collections.Generic;
using AgentLib.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Services.WorkTasks;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = InitializeMainWindowAsync(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        CodingChatRoomPaths paths = CodingChatRoomPaths.CreateForCurrentUser();
        try
        {
            paths = CodingChatStartupOptions.FromArguments(desktop.Args).Paths;
            var logger = InitializeLogging(paths);
            MainViewModel model = await ShellStartup.InitializeAsync(paths, new AvaloniaMainThreadDispatcher(), logger, ProcessLogger);
            var window = new MainWindow { DataContext = model };
            desktop.MainWindow = window;
            window.Show();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or ArgumentException or JsonException)
        {
            if (ProcessLogger is null) InitializeLogging(paths);
            await ProcessLogger.TryLogDiagnosticAsync("[App][Startup]", $"Main ProcessId={Environment.ProcessId}；应用初始化失败。", exception);
            var window = new StartupFailureWindow
            {
                DataContext = new StartupFailureViewModel(paths.ConfigurationFile.FullName, exception,
                    () => desktop.TryShutdown(1)),
            };
            desktop.MainWindow = window;
            window.Show();
        }
    }

    internal static Task DisposeStartupStateAsync(MainViewModel? mainViewModel,
        IReadOnlyList<WorkTaskItemViewModel> tasks, IReadOnlyList<CodingWorkTaskRuntime> runtimes)
        => ShellStartup.DisposeStartupStateAsync(mainViewModel, tasks, runtimes);
}
