using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell;

public partial class MainWindow : Window
{
    private bool _isCleaningUp;
    private bool _allowClose;
    private bool _shutdownRequested;
    private IInputElement? _previousFocus;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        AddHandler(KeyDownEvent, OnExitKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (ExitOverlay.IsVisible) return;

        _previousFocus = FocusManager?.GetFocusedElement();
        MainContent.IsEnabled = false;
        ExitOverlay.IsVisible = true;
        CancelExitButton.Focus();
    }

    private void OnCancelExit(object? sender, RoutedEventArgs e)
    {
        if (_shutdownRequested) return;
        ExitOverlay.IsVisible = false;
        MainContent.IsEnabled = true;
        _previousFocus?.Focus();
    }

    private void OnExitKeyDown(object? sender, KeyEventArgs e)
    {
        if (!ExitOverlay.IsVisible) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            OnCancelExit(this, e);
        }
        else if (e.Key == Key.Enter)
        {
            // Confirmation requires an explicit click or Space on the focused button.
            e.Handled = true;
        }
    }

    private async void OnConfirmExit(object? sender, RoutedEventArgs e)
    {
        if (_isCleaningUp || !ExitOverlay.IsVisible) return;
        _isCleaningUp = true;
        _shutdownRequested = true;
        CancelExitButton.IsEnabled = false;
        ConfirmExitButton.IsEnabled = false;
        ExitError.IsVisible = false;
        ExitProgress.IsVisible = true;
        try
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.DisposeAsync().ConfigureAwait(true);
            }
            _allowClose = true;
            Close();
        }
        catch (Exception exception)
        {
            // Event boundary: keep the window alive and report cleanup failures.
            Trace.TraceError($"Application shutdown cleanup failed: {exception}");
            ExitError.IsVisible = true;
        }
        finally
        {
            _isCleaningUp = false;
            ExitProgress.IsVisible = false;
            ConfirmExitButton.IsEnabled = true;
        }
    }
}
