using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>承载语言、沙盒及模型服务设置。</summary>
public partial class SettingsView : UserControl
{
    /// <summary>初始化设置页。</summary>
    public SettingsView() => InitializeComponent();

    private SettingsViewModel? _purposeModel;

    private void OnPurposeListDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_purposeModel is not null)
            _purposeModel.PurposeModels.CollectionChanged -= OnPurposeCollectionChanged;
        _purposeModel = DataContext as SettingsViewModel;
        if (_purposeModel is not null)
            _purposeModel.PurposeModels.CollectionChanged += OnPurposeCollectionChanged;
        RefreshPurposeOrder();
    }

    private void OnPurposeCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RefreshPurposeOrder();

    private void RefreshPurposeOrder()
    {
        string[] order = ["UserCommunication", "DocumentGeneration", "ImageAnalysis", "Reasoning", "CodeDesign", "CodeReview", "CodeExploration", "HistoryCompression", "SessionTitle"];
        PurposeModelsList.ItemsSource = _purposeModel?.PurposeModels.OrderBy(row =>
            row.IsBuiltIn ? Array.IndexOf(order, row.PurposeKey) : order.Length).ToArray();
    }

    private void OnPurposeListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        foreach (var border in PurposeDescendants(PurposeModelsList).OfType<Border>().Where(b => b.Name == "PurposeRow"))
            UpdatePurposeRow(border);
    }

    private void OnPurposeRowLoaded(object sender, RoutedEventArgs e) => UpdatePurposeRow((Border)sender);
    private void OnPurposeRowSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePurposeRow((Border)sender);

    private void UpdatePurposeRow(Border border)
    {
        if (border.DataContext is not PurposeModelSettingsViewModel row || border.Child is not Grid grid) return;
        double available = PurposeModelsList.ActualWidth;
        if (available <= 0) return;
        double labelWidth = 64;
        foreach (var item in _purposeModel?.PurposeModels.Where(item => item.IsBuiltIn) ?? [])
        {
            var label = new TextBlock { Text = item.DisplayName + ":", FontSize = FontSize, FontFamily = FontFamily };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            labelWidth = Math.Max(labelWidth, Math.Ceiling(label.DesiredSize.Width));
        }
        labelWidth = Math.Min(labelWidth, Math.Min(128, Math.Max(64, available - 180)));
        double itemWidth = available >= 880 ? Math.Floor(available / 2) : available;
        border.Width = row.IsBuiltIn ? itemWidth : available;
        border.Margin = new Thickness(0, row.CanEditPurpose ? 8 : 0, 0, 0);
        bool narrow = row.CanEditPurpose && available < 632;
        grid.ColumnDefinitions[0].Width = new GridLength(row.IsBuiltIn ? labelWidth : 170);
        grid.ColumnDefinitions[1].Width = narrow ? GridLength.Auto : new GridLength(8);
        grid.ColumnDefinitions[2].Width = row.CanEditPurpose && !narrow ? new GridLength(300) : new GridLength(1, GridUnitType.Star);
        var model = (StackPanel)grid.Children[1];
        model.MaxWidth = 300;
        model.HorizontalAlignment = HorizontalAlignment.Left;
        model.Width = Math.Max(0, Math.Min(300, border.Width - 32 - (row.IsBuiltIn ? labelWidth + 8 : narrow ? 0 : 178)));
        Grid.SetRow(model, narrow ? 1 : 0);
        Grid.SetColumn(model, narrow ? 0 : 2);
        Grid.SetColumnSpan(model, narrow ? 3 : 1);
        model.Margin = new Thickness(0, narrow ? 8 : 0, 0, 0);
        Grid.SetColumn(grid.Children[2], narrow ? 1 : 3);
    }

    private static IEnumerable<DependencyObject> PurposeDescendants(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in PurposeDescendants(child)) yield return descendant;
        }
    }

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
