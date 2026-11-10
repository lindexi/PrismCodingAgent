using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>
/// 显示模型服务与 Windows 沙箱设置。
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// 初始化设置视图。
    /// </summary>
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? _purposeModel;

    private void OnPurposeListDataContextChanged(object? sender, EventArgs e)
    {
        if (_purposeModel is not null)
            _purposeModel.PurposeModels.CollectionChanged -= OnPurposeCollectionChanged;
        _purposeModel = DataContext as SettingsViewModel;
        if (_purposeModel is not null)
            _purposeModel.PurposeModels.CollectionChanged += OnPurposeCollectionChanged;
        RefreshPurposeOrder();
    }

    private void OnPurposeCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshPurposeOrder();

    private void RefreshPurposeOrder()
    {
        string[] order = ["UserCommunication", "DocumentGeneration", "ImageAnalysis", "Reasoning", "CodeDesign", "CodeReview", "CodeExploration", "HistoryCompression", "SessionTitle"];
        PurposeModelsList.ItemsSource = _purposeModel?.PurposeModels.OrderBy(row =>
        {
            int index = Array.IndexOf(order, row.PurposeKey);
            return row.IsBuiltIn ? index : order.Length;
        }).ToArray();
    }

    private void OnPurposeFieldsDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Grid panel) UpdatePurposeFields(panel, panel.Bounds.Width);
    }

    private void OnPurposeFieldsSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Grid panel) UpdatePurposeFields(panel, e.NewSize.Width);
    }

    private static void UpdatePurposeFields(Grid panel, double width)
    {
        if (panel.DataContext is not PurposeModelSettingsViewModel row) return;
        bool narrow = row.CanEditPurpose && width < 600;
        panel.ColumnDefinitions = new ColumnDefinitions(row.IsBuiltIn ? "64,8,*,Auto" : narrow ? "170,Auto,*,0" : "170,16,300,Auto");
        var model = panel.Children[2];
        var modelError = panel.Children[4];
        var remove = panel.Children[5];
        Grid.SetRow(model, narrow ? 2 : 0);
        Grid.SetColumn(model, narrow ? 0 : 2);
        Grid.SetColumnSpan(model, narrow ? 3 : 1);
        model.Margin = new Avalonia.Thickness(0, narrow ? 8 : 0, 0, 0);
        Grid.SetRow(modelError, narrow ? 3 : 1);
        Grid.SetColumn(modelError, narrow ? 0 : 2);
        Grid.SetColumnSpan(modelError, narrow ? 3 : 1);
        model.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        model.Width = Math.Max(0, Math.Min(300, row.IsBuiltIn ? width - 72 : narrow ? width : 300));
        modelError.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        modelError.Width = model.Width;
        Grid.SetColumn(remove, narrow ? 1 : 3);
        panel.Children[6].IsVisible = row.CanEditPurpose && !narrow;
        panel.Margin = new Avalonia.Thickness(0, row.CanEditPurpose ? 8 : 0, 0, 0);
    }

    private void OnPurposeListSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is ItemsControl list)
            list.Tag = Math.Min(372d, e.NewSize.Width);
    }

    private void OnWindowsSandboxServerAddressLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel
            && viewModel.TestWindowsSandboxConnectionCommand.CanExecute(null))
        {
            viewModel.TestWindowsSandboxConnectionCommand.Execute(null);
        }
    }

    private async void OnInstallOrUpdateLanguageServerClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            startInfo.ArgumentList.Add("tool");
            startInfo.ArgumentList.Add("update");
            startInfo.ArgumentList.Add("-g");
            startInfo.ArgumentList.Add(Services.LanguageServerInstallCommand.ToolName);

            using Process? process = Process.Start(startInfo);
            if (process is not null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError($"安装或更新语言服务器失败：{exception}");
        }
    }

    private async void OnCopyLanguageServerInstallCommandClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.CreateText(Services.LanguageServerInstallCommand.Text));
            await clipboard.SetDataAsync(dataTransfer);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"复制语言服务器安装命令失败：{exception}");
        }
    }
}
