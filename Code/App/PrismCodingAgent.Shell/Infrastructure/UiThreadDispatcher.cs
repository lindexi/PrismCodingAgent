using System;
using System.Threading.Tasks;

#if USE_AVALONIA
using Avalonia.Threading;
#elif USE_WPF
using System.Windows;
using System.Windows.Threading;
#endif

namespace PrismCodingAgent.AvaloniaShell.Infrastructure;

internal static class UiThreadDispatcher
{
    internal static bool CheckAccess()
    {
#if USE_AVALONIA
        return Dispatcher.UIThread.CheckAccess();
#elif USE_WPF
        return Application.Current.Dispatcher.CheckAccess();
#else
#error A UI framework must be selected.
#endif
    }

    internal static void Post(Action action)
    {
#if USE_AVALONIA
        Dispatcher.UIThread.Post(action);
#elif USE_WPF
        Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
#endif
    }

    internal static async Task InvokeAtRenderAsync(Action action)
    {
#if USE_AVALONIA
        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Render);
#elif USE_WPF
        await Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Render);
#endif
    }
}
