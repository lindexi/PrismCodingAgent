using System.Windows;
using System.Windows.Controls;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>
/// 展开时从数据模板创建详情，收起时卸载详情控件。
/// Content 由控件管理，调用方应通过 Details 和 DetailsTemplate 提供内容。
/// </summary>
public sealed class DeferredExpander : Expander
{
    /// <summary>定义详情数据属性。</summary>
    public static readonly DependencyProperty DetailsProperty = DependencyProperty.Register(
        nameof(Details), typeof(object), typeof(DeferredExpander), new PropertyMetadata(null, OnDetailsChanged));

    /// <summary>定义详情模板属性。</summary>
    public static readonly DependencyProperty DetailsTemplateProperty = DependencyProperty.Register(
        nameof(DetailsTemplate), typeof(DataTemplate), typeof(DeferredExpander), new PropertyMetadata(null, OnDetailsChanged));

    static DeferredExpander()
    {
        IsExpandedProperty.OverrideMetadata(typeof(DeferredExpander),
            new FrameworkPropertyMetadata(false, OnDetailsChanged));
    }

    /// <summary>获取或设置详情数据。</summary>
    public object? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    /// <summary>获取或设置详情模板。</summary>
    public DataTemplate? DetailsTemplate
    {
        get => (DataTemplate?) GetValue(DetailsTemplateProperty);
        set => SetValue(DetailsTemplateProperty, value);
    }

    private static void OnDetailsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (DeferredExpander) sender;
        control.Content = null;
        if (control.IsExpanded && control.DetailsTemplate?.LoadContent() is FrameworkElement child)
        {
            child.DataContext = control.Details;
            control.Content = child;
        }
    }
}
