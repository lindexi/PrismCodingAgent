using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>显示、编辑和应用本地收藏的提示词。</summary>
public partial class FavoritePromptsView : UserControl
{
    private readonly List<FavoritePromptRow> _rows = [];
    private FavoritePromptRow? _editing;
    private FavoritePromptRow? _deleting;
    private string? _filePath;
    private bool _busy;
    private bool _loaded;

    internal event EventHandler<FavoritePromptAppliedEventArgs>? PromptApplied;
    internal event EventHandler? CloseRequested;

    /// <summary>初始化收藏面板。</summary>
    public FavoritePromptsView()
    {
        InitializeComponent();
        PromptList.AddHandler(PointerPressedEvent, OnRowPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Tunnel);
    }

    internal async Task OpenAsync(string filePath)
    {
        if (_busy || IsVisible) return;

        _busy = true;
        _loaded = false;
        _filePath = filePath;
        _rows.Clear();
        _editing = null;
        ClearNotice();
        PromptSearch.Text = string.Empty;
        RefreshList();
        IsVisible = true;
        PromptSearch.Focus();

        try
        {
            string[] prompts = await FavoritePromptStore.LoadAsync(filePath, (DesktopApplication.Current as App)?.ProcessLogger);
            _rows.AddRange(prompts.Select(text => new FavoritePromptRow(text)));
            _loaded = true;
        }
        finally
        {
            _busy = false;
            RefreshList();
        }
    }

    private void RefreshList(FavoritePromptRow? selection = null)
    {
        if (PromptList is null) return;

        selection ??= PromptList.SelectedItem as FavoritePromptRow;
        string search = PromptSearch.Text?.Trim() ?? string.Empty;
        var visible = _rows.Where(row => row == _editing || row.Text.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        PromptList.ItemsSource = visible;
        PromptList.SelectedItem = selection is not null && visible.Contains(selection) ? selection : null;
        EmptyNotice.IsVisible = _loaded && visible.Length == 0;
        EmptyNotice.Text = _rows.Count == 0
            ? Lang.Current.FavoritePromptsEmpty.ToString()
            : Lang.Current.FavoritePromptsNoResults.ToString();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        NewPromptButton.IsEnabled = _loaded && !_busy && _editing is null;
        ApplyPromptButton.IsEnabled = _loaded && !_busy && _editing is null && PromptList.SelectedItem is FavoritePromptRow;
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => RefreshList();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual visual && e.GetCurrentPoint(PromptList).Properties.IsLeftButtonPressed &&
            visual.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is FavoritePromptRow row)
        {
            PromptList.SelectedItem = row;
            if (e.ClickCount != 2 || !ApplyPromptButton.IsEnabled ||
                visual.FindAncestorOfType<Button>(includeSelf: true) is not null) return;

            Apply();
            e.Handled = true;
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        RequestClose();
        e.Handled = true;
    }

    private void OnNew(object? sender, RoutedEventArgs e)
    {
        if (!NewPromptButton.IsEnabled) return;
        var row = new FavoritePromptRow(string.Empty) { IsNew = true };
        _rows.Add(row);
        BeginEdit(row);
    }

    private void OnEdit(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FavoritePromptRow row } && !_busy && _editing is null) BeginEdit(row);
    }

    private void BeginEdit(FavoritePromptRow row)
    {
        ClearNotice();
        _editing = row;
        row.BeginEdit();
        RefreshList(row);
        PromptList.ScrollIntoView(row);
        Dispatcher.UIThread.Post(() =>
        {
            if (_editing != row) return;
            var textBox = PromptList.ContainerFromItem(row)?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (textBox is null) return;
            textBox.Focus();
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
        }, DispatcherPriority.Loaded);
    }

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FavoritePromptRow row } && !row.IsEditing) row.Toggle();
    }

    private async void OnDone(object? sender, RoutedEventArgs e)
    {
        if (_busy || _editing is not { } row) return;
        string text = row.DisplayText;
        if (string.IsNullOrWhiteSpace(text))
        {
            PromptNotice.Text = Lang.Current.FavoritePromptsPlaceholder.ToString();
            return;
        }

        if (!await SaveAsync(_rows.Select(item => item == row ? text : item.Text))) return;
        row.EndEdit(text);
        _editing = null;
        ClearNotice();
        RefreshList(row);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (_busy || _editing is not { } row) return;
        if (row.IsNew) _rows.Remove(row);
        else row.EndEdit(row.Text);
        _editing = null;
        ClearNotice();
        RefreshList();
    }

    private void OnRequestDelete(object? sender, RoutedEventArgs e)
    {
        if (_busy || _editing is not null || sender is not Button { DataContext: FavoritePromptRow row }) return;
        _deleting = row;
        PromptNotice.Text = Lang.Current.FavoritePromptsConfirmDelete.ToString();
        DeleteConfirmation.IsVisible = true;
    }

    private async void OnConfirmDelete(object? sender, RoutedEventArgs e)
    {
        if (_busy || _deleting is not { } row) return;
        if (!await SaveAsync(_rows.Where(item => item != row).Select(item => item.Text))) return;
        _rows.Remove(row);
        ClearNotice();
        RefreshList();
    }

    private void OnCancelDelete(object? sender, RoutedEventArgs e) => ClearNotice();

    private void OnApply(object? sender, RoutedEventArgs e) => Apply();

    private void Apply()
    {
        if (ApplyPromptButton.IsEnabled && PromptList.SelectedItem is FavoritePromptRow row)
            PromptApplied?.Invoke(this, new FavoritePromptAppliedEventArgs(row.Text));
    }

    private void OnClose(object? sender, RoutedEventArgs e) => RequestClose();

    private void RequestClose()
    {
        if (_busy) return;
        if (_editing is not null) PromptNotice.Text = Lang.Current.FavoritePromptsFinishFirst.ToString();
        else CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> SaveAsync(IEnumerable<string> prompts)
    {
        if (_filePath is not { } path) return false;
        string[] snapshot = prompts.ToArray();
        _busy = true;
        PromptList.IsEnabled = false;
        DeleteConfirmation.IsEnabled = false;
        UpdateButtons();

        try
        {
            await FavoritePromptStore.SaveAsync(path, snapshot, (DesktopApplication.Current as App)?.ProcessLogger);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            PromptNotice.Text = Lang.Current.FavoritePromptsSaveError.ToString();
            return false;
        }
        finally
        {
            PromptList.IsEnabled = true;
            DeleteConfirmation.IsEnabled = true;
            _busy = false;
            UpdateButtons();
        }
    }

    private void ClearNotice()
    {
        _deleting = null;
        DeleteConfirmation.IsVisible = false;
        PromptNotice.Text = null;
    }
}
