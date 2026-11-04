using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>显示历史会话列表。</summary>
public partial class SessionListView : UserControl
{
    /// <summary>初始化历史会话列表视图。</summary>
    public SessionListView()
    {
        InitializeComponent();
    }

    private void OnHistoryMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2
            || sender is not FrameworkElement { DataContext: SessionItemViewModel { IsEditing: false } session }
            || DataContext is not MainViewModel model
            || !model.OpenSessionCommand.CanExecute(session)) return;
        model.OpenSessionCommand.Execute(session);
        e.Handled = true;
    }
}
