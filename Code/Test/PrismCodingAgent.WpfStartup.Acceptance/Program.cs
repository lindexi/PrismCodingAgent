using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PrismCodingAgent.WpfShell;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? Option(string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        string root = Path.GetFullPath(Option("--data-directory")
            ?? throw new ArgumentException("An isolated --data-directory is required."));
        Directory.CreateDirectory(root);
        string evidence = Path.Combine(root, "startup-ownership-evidence.txt");
        string? language = Option("--seed-language");
        if (language is not null)
        {
            if (language is not ("en" or "zh-Hans")) throw new ArgumentException("Unsupported acceptance language.");
            File.WriteAllText(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
            {
                LanguageTag = language,
                IsWindowsSandboxEnabled = false,
                IsNetworkProxyEnabled = false,
                NetworkProxyAddress = "http://127.0.0.1:18888",
                WindowsSandboxToolPath = "C:\\Offline\\WinRemoteShell.exe",
                WindowsSandboxServerAddress = "127.0.0.1:12398",
            }));
        }
        bool expectFailure = args.Contains("--expect-failure");
        if (expectFailure) File.WriteAllText(Path.Combine(root, "AgentConfiguration.json"), "{invalid-json");
        Guid[] previousTasks = File.Exists(Path.Combine(root, "WorkTasks.json"))
            ? JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "WorkTasks.json"))).RootElement
                .EnumerateArray().Select(task => task.GetProperty("Id").GetGuid()).ToArray()
            : [];
        bool publishedEntry = args.Contains("--published-entry");
        bool ownsStartup = !args.Contains("--external-owner");
        App? app = null;
        if (!publishedEntry)
        {
            app = new App(ownsStartup) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
        }
        Exception? failure = null;
        bool checking = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        timer.Tick += async (_, _) =>
        {
            app ??= Application.Current as App;
            if (checking || app is null) return;
            if (ownsStartup && app.MainWindow is null && DateTime.UtcNow < deadline) return;
            checking = true;
            timer.Stop();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (!ownsStartup)
                {
                    if (app.Windows.Count != 0) throw new InvalidOperationException("External owner opened an automatic window.");
                    app.MainWindow = await app.OpenAsync(["--data-directory", root]);
                }
                if (expectFailure)
                {
                    if (app.MainWindow is not StartupFailureWindow fault)
                        throw new InvalidOperationException("Corrupt configuration did not open the production failure window.");
                    fault.Close();
                    File.AppendAllText(evidence, "productionFailureWindow=True; closed=True" + Environment.NewLine);
                    return;
                }
                if (app.MainWindow is not MainWindow window || window.DataContext is not MainViewModel model)
                    throw new InvalidOperationException("OnStartup did not produce the production window.");
                if (model.WorkTasks.Count != 1) throw new InvalidOperationException("Production initialization did not complete.");
                if (previousTasks.Length > 0 && !previousTasks.SequenceEqual(model.WorkTasks.Select(task => task.Id)))
                    throw new InvalidOperationException("Task identity was not restored.");
                string expectedLanguage = Option("--expect-language") ?? language ?? "zh-Hans";
                if (Lang.Current.IetfLanguageTag != expectedLanguage)
                    throw new InvalidOperationException("Saved language was not restored.");
                if (window.Icon is null) throw new InvalidOperationException("Production window icon is missing.");
                foreach (string resource in new[] { "CodingChatRoom.ico", "CodingChatRoom_48x48.png" })
                {
                    using var stream = Application.GetResourceStream(new Uri(
                        $"pack://application:,,,/PrismCodingAgent.WpfShell;component/Assets/Icons/{resource}"))?.Stream
                        ?? throw new InvalidOperationException("Published icon resource is missing.");
                    if (stream.Length == 0) throw new InvalidOperationException("Published icon resource is empty.");
                }
                window.Close();
                if (!window.IsVisible) throw new InvalidOperationException("Close skipped confirmation.");
                ((Button)window.FindName("ConfirmExitButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                while (window.IsVisible && DateTime.UtcNow < deadline) await Task.Delay(25);
                if (window.IsVisible) throw new TimeoutException("Confirmed shutdown did not finish.");
                await model.DisposeAsync();
                await model.DisposeAsync();
                string assembly = typeof(App).Assembly.Location;
                File.AppendAllText(evidence,
                    $"publishedEntry={publishedEntry}; assembly={assembly}; sha256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly)))}; " +
                    $"ownsStartup={ownsStartup}; realRun=True; productionModel=True; language={Lang.Current.IetfLanguageTag}; " +
                    $"restoredTasks={previousTasks.Length}; task={model.WorkTasks.Single().Id}; icons=True; confirmedClosed=True; repeatedDispose=True; windows={app.Windows.Count}{Environment.NewLine}");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally { app.Shutdown(failure is null ? 0 : 1); }
        };
        timer.Start();
        int exitCode;
        if (publishedEntry)
        {
            var entry = typeof(App).Assembly.EntryPoint ?? throw new InvalidOperationException("Published entry point is missing.");
            entry.Invoke(null, null);
            exitCode = failure is null ? 0 : 1;
        }
        else
        {
            exitCode = app!.Run();
        }
        File.AppendAllText(evidence,
            $"exitCode={exitCode}; dispatcherFinished={app?.Dispatcher.HasShutdownFinished}; failure={failure}{Environment.NewLine}");
        return exitCode;
    }
}
