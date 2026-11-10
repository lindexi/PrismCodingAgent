using Avalonia.Controls;

namespace PrismCodingAgent.CodingShell.Views;

public partial class ModelServiceConnectionView : UserControl
{
    /// <summary>初始化共享的模型连接表单。</summary>
    public ModelServiceConnectionView() => InitializeComponent();

    private void OnConnectionSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        bool narrow = e.NewSize.Width < 560;
        grid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        grid.ColumnSpacing = narrow ? 0 : 24;
        Grid.SetRow(grid.Children[1], narrow ? 1 : 0);
        Grid.SetColumn(grid.Children[1], narrow ? 0 : 1);
    }
}
