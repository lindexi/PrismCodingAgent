using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Services;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>承载语言、沙盒及模型服务设置。</summary>
public partial class SettingsView : UserControl
{
    /// <summary>初始化设置页。</summary>
    public SettingsView() => InitializeComponent();

    private void OnWindowsSandboxServerAddressLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel
            && viewModel.TestWindowsSandboxConnectionCommand.CanExecute(null))
        {
            viewModel.TestWindowsSandboxConnectionCommand.Execute(null);
        }
    }

    private async void OnInstallOrUpdateLanguageServerClick(object sender, RoutedEventArgs e)
    {
        InstallLanguageServerButton.IsEnabled = false;
        LanguageServerStatus.Text = Lang.Current.LanguageServerInstalling;
        try
        {
            var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = false };
            startInfo.ArgumentList.Add("tool");
            startInfo.ArgumentList.Add("update");
            startInfo.ArgumentList.Add("-g");
            startInfo.ArgumentList.Add(LanguageServerInstallCommand.ToolName);
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(Lang.Current.LanguageServerInstallFailed);
            await process.WaitForExitAsync();
            LanguageServerStatus.Text = process.ExitCode == 0
                ? Lang.Current.LanguageServerInstallSucceeded : Lang.Current.LanguageServerInstallFailed;
        }
        catch (Win32Exception exception)
        {
            Trace.TraceError($"{Lang.Current.LanguageServerInstallFailed}: {exception}");
            LanguageServerStatus.Text = Lang.Current.LanguageServerInstallFailed;
        }
        catch (InvalidOperationException exception)
        {
            Trace.TraceError($"{Lang.Current.LanguageServerInstallFailed}: {exception}");
            LanguageServerStatus.Text = Lang.Current.LanguageServerInstallFailed;
        }
        finally
        {
            InstallLanguageServerButton.IsEnabled = true;
        }
    }

    private void OnCopyLanguageServerInstallCommandClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(LanguageServerInstallCommand.Text);
            LanguageServerStatus.Text = Lang.Current.LanguageServerCommandCopied;
        }
        catch (ExternalException exception)
        {
            Trace.TraceError($"{Lang.Current.LanguageServerCopyFailed}: {exception}");
            LanguageServerStatus.Text = Lang.Current.LanguageServerCopyFailed;
        }
    }
}
