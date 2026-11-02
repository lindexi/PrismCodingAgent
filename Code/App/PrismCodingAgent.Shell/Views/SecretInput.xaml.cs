using System.Windows;
using System.Windows.Controls;

namespace PrismCodingAgent.AvaloniaShell.Views;

/// <summary>默认遮蔽、可切换显隐的密钥编辑控件。</summary>
public partial class SecretInput : UserControl
{
    /// <summary>支持双向绑定的密钥文本。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SecretInput),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

    private bool _updating;

    /// <summary>初始化遮蔽输入框。</summary>
    public SecretInput() => InitializeComponent();

    /// <summary>获取或设置密钥文本。</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((SecretInput)sender).UpdateInputs();

    private void UpdateInputs()
    {
        if (_updating || MaskedInput is null)
        {
            return;
        }

        _updating = true;
        try
        {
            if (MaskedInput.Password != (Text ?? string.Empty))
            {
                MaskedInput.Password = Text ?? string.Empty;
            }
            // Do not retain clear text in the hidden TextBox.
            RevealedInput.Text = RevealButton.IsChecked == true ? Text ?? string.Empty : string.Empty;
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            SetCurrentValue(TextProperty, MaskedInput.Password);
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_updating && RevealButton.IsChecked == true)
        {
            SetCurrentValue(TextProperty, RevealedInput.Text);
        }
    }

    private void OnRevealChanged(object sender, RoutedEventArgs args)
    {
        bool reveal = RevealButton.IsChecked == true;
        MaskedInput.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
        RevealedInput.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
        UpdateInputs();
    }
}
