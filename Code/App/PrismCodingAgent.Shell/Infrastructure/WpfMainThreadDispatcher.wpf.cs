using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AgentLib;

namespace PrismCodingAgent.AvaloniaShell.Infrastructure;

/// <summary>将 AgentLib 的异步主线程操作投递到生产 WPF Dispatcher。</summary>
public sealed class WpfMainThreadDispatcher : IMainThreadDispatcher
{
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    /// <inheritdoc />
    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    /// <inheritdoc />
    public Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    /// <inheritdoc />
    public bool CheckAccess() => _dispatcher.CheckAccess();
}
