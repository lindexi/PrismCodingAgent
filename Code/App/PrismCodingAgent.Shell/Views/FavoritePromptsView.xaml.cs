using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

    /// <summary>初始化收藏提示词面板。</summary>
    public FavoritePromptsView()
    {
        InitializeComponent();
        PromptSearch.TextChanged += (_, _) => RefreshList();
        PromptList.SelectionChanged += (_, _) => UpdateButtons();
    }

    internal async Task OpenAsync(string filePath)
    {
        if (_busy || Visibility == Visibility.Visible) return;

        _filePath = filePath;
        _loaded = false;
        _rows.Clear();
        _editing = null;
        ClearNotice();
        SetBusy(true);
        PromptSearch.Clear();
        RefreshList();
        Visibility = Visibility.Visible;
        PromptSearch.Focus();

        try
        {
            string[] prompts = await FavoritePromptStore.LoadAsync(filePath, (DesktopApplication.Current as App)?.ProcessLogger);
            _rows.AddRange(prompts.Select(text => new FavoritePromptRow(text)));
            _loaded = true;
        }
        finally
        {
            SetBusy(false);
            RefreshList();
        }
    }

    private void RefreshList(FavoritePromptRow? selection = null)
    {
        selection ??= PromptList.SelectedItem as FavoritePromptRow;
        string search = PromptSearch.Text.Trim();
        var visible = _rows.Where(row => row == _editing || row.Text.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        PromptList.ItemsSource = visible;
        PromptList.SelectedItem = selection is not null && visible.Contains(selection) ? selection : null;
        EmptyNotice.Visibility = _loaded && visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyNotice.Text = _rows.Count == 0 ? Lang.Current.FavoritePromptsEmpty : Lang.Current.FavoritePromptsNoResults;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        NewPromptButton.IsEnabled = _loaded && !_busy && _editing is null;
        ApplyPromptButton.IsEnabled = NewPromptButton.IsEnabled && PromptList.SelectedItem is FavoritePromptRow;
    }

    private void OnRowPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(PromptList, source) is not ListBoxItem { DataContext: FavoritePromptRow row } item)
        {
            return;
        }

        PromptList.SelectedItem = row;
        if (e.ClickCount != 2 || !ApplyPromptButton.IsEnabled) return;

        for (DependencyObject? element = source; element is not null && element != item; element = GetParent(element))
        {
            if (element is Button) return;
        }

        Apply();
        e.Handled = true;
    }

    private static DependencyObject? GetParent(DependencyObject element) => element switch
    {
        Visual => VisualTreeHelper.GetParent(element),
        FrameworkContentElement content => content.Parent,
        _ => null,
    };

    private void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        RequestClose();
        e.Handled = true;
    }

    private async void OnNewAsync(object sender, RoutedEventArgs e)
    {
        if (!NewPromptButton.IsEnabled) return;
        var row = new FavoritePromptRow(string.Empty) { IsNew = true };
        _rows.Add(row);
        await BeginEditAsync(row);
    }

    private async void OnEditAsync(object sender, RoutedEventArgs e)
    {
        if (!_busy && _editing is null && sender is Button { DataContext: FavoritePromptRow row }) await BeginEditAsync(row);
    }

    private async Task BeginEditAsync(FavoritePromptRow row)
    {
        ClearNotice();
        _editing = row;
        row.BeginEdit();
        RefreshList(row);
        PromptList.ScrollIntoView(row);
        await Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible || _editing != row || PromptList.ItemContainerGenerator.ContainerFromItem(row) is not ListBoxItem item) return;
            var editor = FindEditor(item);
            if (editor is null) return;
            editor.Focus();
            editor.CaretIndex = editor.Text.Length;
        }, DispatcherPriority.Loaded);
    }

    private static TextBox? FindEditor(DependencyObject element)
    {
        if (element is TextBox editor) return editor;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            if (FindEditor(VisualTreeHelper.GetChild(element, i)) is { } childEditor) return childEditor;
        }
        return null;
    }

    private void OnToggle(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FavoritePromptRow { IsEditing: false } row }) row.Toggle();
    }

    private async void OnDoneAsync(object sender, RoutedEventArgs e)
    {
        if (_busy || _editing is not { } row) return;
        string text = row.DisplayText;
        if (string.IsNullOrWhiteSpace(text))
        {
            PromptNotice.Text = Lang.Current.FavoritePromptsPlaceholder;
            return;
        }

        if (!await SaveAsync(_rows.Select(item => item == row ? text : item.Text).ToArray())) return;
        row.EndEdit(text);
        _editing = null;
        ClearNotice();
        RefreshList(row);
        PromptList.Focus();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (_busy || _editing is not { } row) return;
        if (row.IsNew) _rows.Remove(row);
        else row.EndEdit(row.Text);
        _editing = null;
        ClearNotice();
        RefreshList();
        PromptList.Focus();
    }

    private void OnRequestDelete(object sender, RoutedEventArgs e)
    {
        if (_busy || _editing is not null || sender is not Button { DataContext: FavoritePromptRow row }) return;
        _deleting = row;
        PromptNotice.Text = Lang.Current.FavoritePromptsConfirmDelete;
        DeleteConfirmation.Visibility = Visibility.Visible;
    }

    private async void OnConfirmDeleteAsync(object sender, RoutedEventArgs e)
    {
        if (_busy || _deleting is not { } row) return;
        if (!await SaveAsync(_rows.Where(item => item != row).Select(item => item.Text).ToArray())) return;
        _rows.Remove(row);
        ClearNotice();
        RefreshList();
        PromptList.Focus();
    }

    private void OnCancelDelete(object sender, RoutedEventArgs e)
    {
        ClearNotice();
        PromptList.Focus();
    }

    private void OnApply(object sender, RoutedEventArgs e) => Apply();

    private void Apply()
    {
        if (ApplyPromptButton.IsEnabled && PromptList.SelectedItem is FavoritePromptRow row)
            PromptApplied?.Invoke(this, new FavoritePromptAppliedEventArgs(row.Text));
    }

    private void OnClose(object sender, RoutedEventArgs e) => RequestClose();

    private void RequestClose()
    {
        if (_busy) return;
        if (_editing is not null) PromptNotice.Text = Lang.Current.FavoritePromptsFinishFirst;
        else CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> SaveAsync(string[] prompts)
    {
        if (_filePath is not { } path) return false;
        SetBusy(true);
        try
        {
            await FavoritePromptStore.SaveAsync(path, prompts, (DesktopApplication.Current as App)?.ProcessLogger);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            PromptNotice.Text = Lang.Current.FavoritePromptsSaveError;
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        PromptList.IsEnabled = !busy;
        DeleteConfirmation.IsEnabled = !busy;
        UpdateButtons();
    }

    private void ClearNotice()
    {
        _deleting = null;
        DeleteConfirmation.Visibility = Visibility.Collapsed;
        PromptNotice.Text = string.Empty;
    }
}
