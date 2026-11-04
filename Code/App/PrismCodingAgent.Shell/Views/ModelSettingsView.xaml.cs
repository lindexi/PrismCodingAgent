using System.Windows.Controls;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>编辑共享设置中的服务连接和模型。</summary>
public partial class ModelSettingsView : UserControl
{
    /// <summary>初始化模型编辑视图。</summary>
    public ModelSettingsView() => InitializeComponent();

    private void OnNumericLostFocus(object sender, System.Windows.RoutedEventArgs e)
        => CommitNumber((TextBox)sender);

    private static void CommitNumber(TextBox input)
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            return;
        }
        if (decimal.TryParse(input.Text, out decimal value))
        {
            input.Text = decimal.Clamp(decimal.Truncate(value), 1, decimal.Parse((string)input.Tag)).ToString();
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        else
        {
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }
    }

    private void OnIncrement(object sender, System.Windows.RoutedEventArgs e) => Step((Button)sender, 1);

    private void OnDecrement(object sender, System.Windows.RoutedEventArgs e) => Step((Button)sender, -1);

    private static void Step(Button button, int delta)
    {
        var input = (TextBox)button.Tag;
        CommitNumber(input);
        decimal.TryParse(input.Text, out decimal value);
        input.Text = decimal.Clamp(value + delta, 1, decimal.Parse((string)input.Tag)).ToString();
        input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
