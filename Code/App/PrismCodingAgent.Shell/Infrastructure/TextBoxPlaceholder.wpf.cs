using System.Windows;

namespace PrismCodingAgent.CodingShell.Infrastructure;

/// <summary>为共享 TextBox 模板提供可选的占位提示。</summary>
public static class TextBoxPlaceholder
{
    /// <summary>占位提示文本；空字符串表示不显示提示。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(TextBoxPlaceholder), new PropertyMetadata(string.Empty));

    /// <summary>获取占位提示文本。</summary>
    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    /// <summary>设置占位提示文本。</summary>
    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);
}
