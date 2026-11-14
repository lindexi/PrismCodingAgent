using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.CodingShell;

/// <summary>承载真实运行时，并在用户确认后异步停止及释放所有任务。</summary>
public partial class MainWindow : Window
{
    private bool _isCleaningUp;
    private bool _allowClose;
    private bool _shutdownRequested;
    private IInputElement? _previousFocus;
    private readonly ChatAutoScrollState _autoScrollState = new();
    private ChatViewModel? _scrollChat;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnChatDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_scrollChat is not null)
        {
            _scrollChat.PropertyChanged -= OnChatPropertyChanged;
        }
        _scrollChat = e.NewValue as ChatViewModel;
        if (_scrollChat is not null)
        {
            _scrollChat.PropertyChanged += OnChatPropertyChanged;
        }
        _autoScrollState.Reset();
        ScrollToEndAfterLayout((System.Windows.Controls.ScrollViewer)sender);
    }

    private void OnScrollToEndRequested(object sender, RoutedEventArgs e)
    {
        _autoScrollState.Reset();
        ScrollToEndAfterLayout(MessagesScrollViewer);
    }

    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.CurrentSessionId))
        {
            _autoScrollState.Reset();
            ScrollToEndAfterLayout(MessagesScrollViewer);
        }
    }

    private void OnMessagesScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        var viewer = (System.Windows.Controls.ScrollViewer)sender;
        if (!ReferenceEquals(e.OriginalSource, viewer)) return;
        if (_autoScrollState.HandleScrollChanged(viewer.VerticalOffset, viewer.ExtentHeight, viewer.ViewportHeight,
            e.ExtentHeightChange, e.ViewportHeightChange, e.VerticalChange))
            ScrollToEndAfterLayout(viewer);
    }

    private static void ScrollToEndAfterLayout(System.Windows.Controls.ScrollViewer viewer)
        => viewer.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, (Action)viewer.ScrollToEnd);

    private void OnMessageCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Parameter is not string text) return;
        e.CanExecute = !string.IsNullOrEmpty(text);
        e.Handled = true;
    }

    private void OnMessageCopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not string text) return;
        Clipboard.SetText(text);
        e.Handled = true;
    }

    private void OnMessageImageClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { CommandParameter: AgentLib.Model.CopilotChatImageItem image } button)
        {
            ImageViewer.Open(button, image.Data, Lang.Current.ImageType);
        }
    }

    private void OnApproveToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AgentLib.Model.CopilotChatApprovalToolItem { CanRespondToApproval: true } item } element)
            FindApprovalChat(element)?.ApproveTool(item);
    }

    private void OnRejectToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AgentLib.Model.CopilotChatApprovalToolItem { CanRespondToApproval: true } item } element)
            FindApprovalChat(element)?.RejectTool(item);
    }

    private static ChatViewModel? FindApprovalChat(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: ChatViewModel chat }) return chat;
            if (current is FrameworkElement { DataContext: MainViewModel model }) return model.ActiveWorkTask?.Chat;
        }
        return null;
    }

    private void OnTaskMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (ExitOverlay.IsVisible) return;
        _previousFocus = Keyboard.FocusedElement;
        MainContent.IsEnabled = false;
        ExitOverlay.Visibility = Visibility.Visible;
        CancelExitButton.Focus();
    }

    private void OnCancelExit(object sender, RoutedEventArgs e)
    {
        if (_shutdownRequested) return;
        ExitOverlay.Visibility = Visibility.Collapsed;
        MainContent.IsEnabled = true;
        _previousFocus?.Focus();
    }

    private void OnExitKeyDown(object sender, KeyEventArgs e)
    {
        if (!ExitOverlay.IsVisible) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            OnCancelExit(this, e);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
        }
    }

    private async void OnConfirmExit(object sender, RoutedEventArgs e)
    {
        if (_isCleaningUp || !ExitOverlay.IsVisible) return;
        _isCleaningUp = true;
        _shutdownRequested = true;
        CancelExitButton.IsEnabled = false;
        ConfirmExitButton.IsEnabled = false;
        ExitError.Visibility = Visibility.Collapsed;
        ExitProgress.Visibility = Visibility.Visible;
        try
        {
            if (DataContext is MainViewModel model) await model.DisposeAsync();
            _allowClose = true;
            Close();
        }
        catch (Exception exception)
        {
            // Event boundary: preserve the window so failed cleanup can be retried.
            Trace.TraceError($"Application shutdown cleanup failed: {exception}");
            ExitError.Visibility = Visibility.Visible;
        }
        finally
        {
            _isCleaningUp = false;
            ExitProgress.Visibility = Visibility.Collapsed;
            ConfirmExitButton.IsEnabled = true;
        }
    }
}
