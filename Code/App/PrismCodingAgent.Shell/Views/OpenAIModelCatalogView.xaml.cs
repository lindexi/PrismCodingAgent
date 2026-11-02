using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>使用共享目录逻辑添加模型。</summary>
public partial class OpenAIModelCatalogView : UserControl
{
    private ModelCatalogViewModel? _ownedModel;

    /// <summary>初始化模型添加视图。</summary>
    public OpenAIModelCatalogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateModel();
        Loaded += (_, _) => UpdateModel();
        Unloaded += (_, _) =>
        {
            _ownedModel?.Dispose();
            _ownedModel = null;
            CatalogPanel.ClearValue(DataContextProperty);
        };
    }

    private void UpdateModel()
    {
        _ownedModel?.Dispose();
        _ownedModel = DataContext is ProviderSettingsViewModel provider ? new ModelCatalogViewModel(provider) : null;
        if (_ownedModel is not null) CatalogPanel.DataContext = _ownedModel;
        else CatalogPanel.ClearValue(DataContextProperty);
    }
}
