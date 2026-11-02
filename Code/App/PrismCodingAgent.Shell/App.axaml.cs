using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrismCodingAgent.AvaloniaShell.Infrastructure;
using PrismCodingAgent.AvaloniaShell.Services.WorkTasks;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = InitializeMainWindowAsync(desktop, CodingChatStartupOptions.FromArguments(desktop.Args).Paths);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop, CodingChatRoomPaths paths)
    {
        try
        {
            MainViewModel model = await ShellStartup.InitializeAsync(paths, new AvaloniaMainThreadDispatcher());
            var window = new MainWindow { DataContext = model };
            desktop.MainWindow = window;
            window.Show();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or ArgumentException or JsonException)
        {
            Trace.TraceError($"CodingChatRoom 应用初始化失败：{exception}");
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
