using System.Windows;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;

namespace PrismCodingAgent.WpfShell.Tests;

internal sealed class WpfTestSession : IDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;
    private int _disposed;
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    internal WpfTestSession()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "WPF test UI" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _dispatcher = _ready.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
    }

    private void Run()
    {
        try
        {
            var application = new App(ownsStartup: false);
            var resourceUri = new Uri("/PrismCodingAgent.WpfShell;component/App.xaml", UriKind.Relative);
            Application.LoadComponent(application, resourceUri);
            if (application.ShutdownMode != ShutdownMode.OnMainWindowClose)
            {
                throw new InvalidOperationException("Production App.xaml was not loaded.");
            }
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ready.SetResult(application.Dispatcher);
            Dispatcher.Run();
        }
        catch (Exception exception)
        {
            if (!_ready.TrySetException(exception))
            {
                throw;
            }
        }
    }

    private static readonly AsyncLocal<CancellationToken> OperationCancellation = new();
    internal static CancellationToken Cancellation => OperationCancellation.Value;

    internal async Task<T> ExecuteAsync<T>(Func<Task<T>> action, TimeSpan? timeout = null)
    {
        using var cancellation = new CancellationTokenSource(timeout ?? Deadline);
        Task<T> task = _dispatcher.InvokeAsync(async () =>
        {
            CancellationToken previous = OperationCancellation.Value;
            OperationCancellation.Value = cancellation.Token;
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return await action();
            }
            finally
            {
                OperationCancellation.Value = previous;
            }
        }, DispatcherPriority.Normal, cancellation.Token).Task.Unwrap();
        try
        {
            return await task.WaitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Observe cooperative cancellation before allowing another test to use the host.
            try
            {
                await task.WaitAsync(Deadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            throw new TimeoutException("WPF UI operation exceeded its deadline and was canceled.");
        }
    }

    internal static void CloseWindows()
    {
        foreach (Window window in Application.Current.Windows.Cast<Window>().ToArray())
        {
            window.Close();
        }
        if (Application.Current.Windows.Count != 0)
        {
            throw new InvalidOperationException("Test windows refused to close.");
        }
    }

    public void Dispose()
    {
        if (_dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("Dispose must run outside the UI thread.");
        }
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        int remainingWindows = -1;
        _dispatcher.InvokeAsync(() =>
        {
            CloseWindows();
            remainingWindows = Application.Current.Windows.Count;
            Application.Current.Shutdown();
            _dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        }).Task.WaitAsync(Deadline).GetAwaiter().GetResult();
        if (!_thread.Join(Deadline))
        {
            throw new TimeoutException("WPF Dispatcher did not terminate.");
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "dispatcher-shutdown.txt"),
            $"Windows={remainingWindows}; HasShutdownFinished={_dispatcher.HasShutdownFinished}; ThreadAlive={_thread.IsAlive}; CleanupThread={Environment.CurrentManagedThreadId}; UiThread={_thread.ManagedThreadId}");
    }
}
