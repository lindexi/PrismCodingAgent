using System.Globalization;
using System.Windows.Data;

namespace PrismCodingAgent.CodingShell.Converters;

/// <summary>组合应用、任务、工作区及会话标题。</summary>
public sealed class WorkspaceTitleConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string applicationTitle = values.Length > 3 ? values[3] as string ?? string.Empty : parameter as string ?? string.Empty;
        string? workspacePath = values.Length > 0 ? values[0] as string : null;
        string? sessionTitle = values.Length > 1 ? values[1] as string : null;
        string? taskName = values.Length > 2 ? values[2] as string : null;
        return string.Join(" - ", new[] { applicationTitle, taskName, workspacePath, sessionTitle }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
