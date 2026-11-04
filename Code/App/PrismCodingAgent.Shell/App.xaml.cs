using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell;

/// <summary>WPF 组合根；业务初始化由共享启动流程拥有。</summary>
public partial class App : Application
{
    private readonly bool _ownsStartup;

    /// <summary>创建拥有生产启动流程的应用。</summary>
    public App() : this(true)
    {
    }

    /// <summary>创建应用；外部宿主可显式拥有启动与退出生命周期。</summary>
    public App(bool ownsStartup)
    {
        _ownsStartup = ownsStartup;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (_ownsStartup)
        {
            MainWindow = await OpenAsync(e.Args);
        }
    }

    /// <summary>使用真实启动参数创建并显示窗口；显式数据目录用于隔离宿主和诊断。</summary>
    public async Task<Window> OpenAsync(string[] arguments)
    {
        Dispatcher.VerifyAccess();
        CodingChatRoomPaths? paths = null;
        try
        {
            paths = CodingChatStartupOptions.FromArguments(arguments).Paths;
            var model = await ShellStartup.InitializeAsync(paths, new WpfMainThreadDispatcher());
            var window = new MainWindow { DataContext = model };
            window.Show();
            return window;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or ArgumentException or JsonException)
        {
            Trace.TraceError($"CodingChatRoom initialization failed: {exception}");
            var window = new StartupFailureWindow
            {
                DataContext = new StartupFailureViewModel(
                    (paths ?? CodingChatRoomPaths.CreateForCurrentUser()).ConfigurationFile.FullName,
                    exception, () => Shutdown(1)),
            };
            window.Show();
            return window;
        }
    }
}
