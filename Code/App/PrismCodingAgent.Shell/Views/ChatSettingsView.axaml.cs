using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Services.WorkTasks;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>显示聊天设置选项。</summary>
public partial class ChatSettingsView : UserControl
{
    private string? _logFile;
    private string? _sessionsDirectory;

    /// <summary>初始化聊天设置面板。</summary>
    public ChatSettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshDiagnostics();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty && IsVisible && IsLoaded)
            {
                RefreshDiagnostics();
            }
        };
    }

    private void RefreshDiagnostics()
    {
        ProcessIdText.Text = Environment.ProcessId.ToString();
        DiagnosticsFeedback.IsVisible = false;
        CodingWorkTaskRuntime? runtime = (this.FindAncestorOfType<MainView>()?.DataContext as MainViewModel)
            ?.ActiveWorkTask.Runtime;
        _sessionsDirectory = null;
        if (runtime is not null)
        {
            var session = runtime.ChatManager.SelectedSession;
            _sessionsDirectory = runtime.Paths.GetSessionArchiveDirectory(session);
        }
        _logFile = null;
        try
        {
            _logFile = runtime?.Paths.FindSessionLogFile(runtime.ChatManager.SelectedSession.SessionId);
        }
        catch (IOException)
        {
            ShowFeedback(Lang.Current.ChatPathOpenFailed);
        }
        catch (UnauthorizedAccessException)
        {
            ShowFeedback(Lang.Current.ChatPathOpenFailed);
        }
        LogFileInput.Text = _logFile;
        ToolTip.SetTip(LogFileInput, _logFile);
        OpenLogFileButton.IsEnabled = _logFile is not null;
        OpenLogFolderButton.IsEnabled = _logFile is not null;
        SessionsDirectoryInput.Text = _sessionsDirectory;
        ToolTip.SetTip(SessionsDirectoryInput, _sessionsDirectory);
        OpenSessionsButton.IsEnabled = _sessionsDirectory is not null;
    }

    private async void CopyPidClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                ShowFeedback(Lang.Current.ChatCopyFailed);
                return;
            }
            var data = new Avalonia.Input.DataTransfer();
            data.Add(Avalonia.Input.DataTransferItem.CreateText(Environment.ProcessId.ToString()));
            await clipboard.SetDataAsync(data);
            DiagnosticsFeedback.IsVisible = false;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.ExternalException or NotSupportedException)
        {
            ShowFeedback(Lang.Current.ChatCopyFailed);
        }
    }

    private void LaunchDebuggerClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Debugger.Launch();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException or System.Security.SecurityException)
        {
            ShowFeedback(Lang.Current.ChatDebuggerFailed);
        }
    }

    private void OpenLogClicked(object? sender, RoutedEventArgs e) => OpenPath(_logFile);
    private void OpenLogFolderClicked(object? sender, RoutedEventArgs e) => OpenPath(Path.GetDirectoryName(_logFile));
    private void OpenSessionsClicked(object? sender, RoutedEventArgs e) => OpenPath(_sessionsDirectory);

    private void OpenPath(string? path)
    {
        if (path is null) return;
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            DiagnosticsFeedback.IsVisible = false;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            ShowFeedback(Lang.Current.ChatPathOpenFailed);
        }
    }

    private void ShowFeedback(string message)
    {
        DiagnosticsFeedback.Text = message;
        DiagnosticsFeedback.IsVisible = true;
    }
}
