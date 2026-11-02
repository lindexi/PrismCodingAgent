using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PrismCodingAgent.AvaloniaShell.Views;

internal static class ImageViewer
{
    internal static void Open(FrameworkElement source, BinaryData data, string title)
    {
        var viewer = new Window
        {
            Owner = Window.GetWindow(source),
            Title = title,
            Width = 800,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)source.FindResource("SurfaceBrush"),
            Content = new BinaryImage { Data = data, Margin = new Thickness(16) }
        };
        viewer.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                viewer.Close();
            }
        };
        viewer.Show();
    }
}
