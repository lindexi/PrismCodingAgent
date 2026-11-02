using System.Windows;
using System.Windows.Controls;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>保留流式追加正文时的选择及光标位置。</summary>
public sealed class MessageTextBox : TextBox
{
    /// <summary>绑定公开消息正文。</summary>
    public static readonly DependencyProperty MessageTextProperty = DependencyProperty.Register(
        nameof(MessageText), typeof(string), typeof(MessageTextBox), new PropertyMetadata(string.Empty, OnMessageTextChanged));

    /// <summary>获取或设置公开消息正文。</summary>
    public string MessageText
    {
        get => (string)GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    private static void OnMessageTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var control = (MessageTextBox)sender;
        string text = e.NewValue as string ?? string.Empty;
        // 替换整个 Text 会让 WPF 将聚焦光标重置到开头并请求外层滚动。
        if (text.StartsWith(control.Text, StringComparison.Ordinal))
            control.AppendText(text[control.Text.Length..]);
        else
            control.Text = text;
    }
}
