using System.Windows;
using System.Windows.Threading;

namespace PrismCodingAgent.WpfShell.Tests;

internal sealed class WpfTestSession : IDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;

    internal WpfTestSession()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "WPF test UI" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _dispatcher = _ready.Task.GetAwaiter().GetResult();
    }

    private void Run()
    {
        try
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
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

    internal Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        => _dispatcher.InvokeAsync(action).Task.Unwrap();

    public void Dispose()
    {
        _dispatcher.Invoke(() => Application.Current.Shutdown());
        _thread.Join();
    }
}
