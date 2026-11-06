using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>显示当前聊天的运行设置与服务操作。</summary>
public partial class ChatSettingsView : UserControl
{
    private string? _logFile;
    private string? _sessionsDirectory;

    /// <summary>初始化聊天设置视图。</summary>
    public ChatSettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshDiagnostics();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && IsLoaded) RefreshDiagnostics();
        };
    }

    private void RefreshDiagnostics()
    {
        ProcessIdText.Text = Environment.ProcessId.ToString();
        DiagnosticsFeedback.Visibility = Visibility.Collapsed;
        var runtime = (Window.GetWindow(this)?.DataContext as MainViewModel)?.ActiveWorkTask.Runtime;
        _sessionsDirectory = null;
        _logFile = null;
        if (runtime is not null)
        {
            var session = runtime.ChatManager.SelectedSession;
            _sessionsDirectory = runtime.Paths.GetSessionArchiveDirectory(session);
            try
            {
                _logFile = runtime.Paths.FindSessionLogFile(session.SessionId);
            }
            catch (IOException)
            {
                ShowFeedback(Lang.Current.ChatPathOpenFailed);
            }
            catch (UnauthorizedAccessException)
            {
                ShowFeedback(Lang.Current.ChatPathOpenFailed);
            }
        }
        LogFileInput.Text = _logFile ?? string.Empty;
        LogFileInput.ToolTip = _logFile;
        OpenLogFileButton.IsEnabled = OpenLogFolderButton.IsEnabled = _logFile is not null;
        SessionsDirectoryInput.Text = _sessionsDirectory ?? string.Empty;
        SessionsDirectoryInput.ToolTip = _sessionsDirectory;
        OpenSessionsButton.IsEnabled = _sessionsDirectory is not null;
    }

    private void CopyPidClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Environment.ProcessId.ToString());
            DiagnosticsFeedback.Visibility = Visibility.Collapsed;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ShowFeedback(Lang.Current.ChatCopyFailed);
        }
    }

    private void LaunchDebuggerClicked(object sender, RoutedEventArgs e)
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

    private void OpenLogClicked(object sender, RoutedEventArgs e) => OpenPath(_logFile);
    private void OpenLogFolderClicked(object sender, RoutedEventArgs e) => OpenPath(Path.GetDirectoryName(_logFile));
    private void OpenSessionsClicked(object sender, RoutedEventArgs e) => OpenPath(_sessionsDirectory);

    private void OpenPath(string? path)
    {
        if (path is null) return;
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            DiagnosticsFeedback.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            ShowFeedback(Lang.Current.ChatPathOpenFailed);
        }
    }

    private void ShowFeedback(string message)
    {
        DiagnosticsFeedback.Text = message;
        DiagnosticsFeedback.Visibility = Visibility.Visible;
    }
}
