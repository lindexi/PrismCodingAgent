using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>
/// 展开时从数据模板创建详情，收起时卸载详情控件。
/// Content 由控件管理，调用方应通过 Details 和 DetailsTemplate 提供内容。
/// </summary>
public sealed class DeferredExpander : Expander
{
    /// <summary>定义详情数据属性。</summary>
    public static readonly StyledProperty<object?> DetailsProperty =
        AvaloniaProperty.Register<DeferredExpander, object?>(nameof(Details));

    /// <summary>定义详情模板属性。</summary>
    public static readonly StyledProperty<IDataTemplate?> DetailsTemplateProperty =
        AvaloniaProperty.Register<DeferredExpander, IDataTemplate?>(nameof(DetailsTemplate));

    /// <summary>获取或设置详情数据。</summary>
    public object? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    /// <summary>获取或设置详情模板。</summary>
    public IDataTemplate? DetailsTemplate
    {
        get => GetValue(DetailsTemplateProperty);
        set => SetValue(DetailsTemplateProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Expander);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsExpandedProperty && change.Property != DetailsProperty
            && change.Property != DetailsTemplateProperty)
        {
            return;
        }

        Content = null;
        // 隐藏的呈现器可能不会再次布局，主动更新以解除其对旧 Child 的持有。
        Presenter?.UpdateChild();
        if (IsExpanded && DetailsTemplate is { } template)
        {
            Control? child = template.Build(Details);
            if (child is not null)
            {
                child.DataContext = Details;
            }
            Content = child;
        }
    }
}
