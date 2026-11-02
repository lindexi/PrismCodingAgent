using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PrismCodingAgent.AvaloniaShell.Localizations;

namespace PrismCodingAgent.WpfShell.Tests;

internal sealed class WpfScreenshotSession : IDisposable
{
    private readonly TestContext _testContext;
    private int _step;

    internal WpfScreenshotSession(Window window, TestContext testContext)
    {
        Window = window;
        _testContext = testContext;
        DirectoryPath = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots",
            testContext.TestName!, Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        AppDataDirectory = Directory.CreateDirectory(Path.Combine(DirectoryPath, "AppData")).FullName;
        testContext.WriteLine($"UI artifacts: {DirectoryPath}");
    }

    internal Window Window { get; }
    internal string DirectoryPath { get; }
    internal string AppDataDirectory { get; }

    internal Task<string> CaptureAsync(string stepName)
        => CaptureSurfaceAsync((FrameworkElement)Window.Content, stepName);

    // Popup.Child and ContextMenu live in separate HWNDs; capture each visible WPF surface explicitly.
    internal async Task<string> CaptureSurfaceAsync(FrameworkElement content, string stepName)
    {
        Window.Dispatcher.VerifyAccess();
        await Window.Dispatcher.InvokeAsync(content.UpdateLayout, DispatcherPriority.ApplicationIdle);
        if (!content.IsVisible || content.ActualWidth <= 0 || content.ActualHeight <= 0)
        {
            throw new InvalidOperationException("Screenshot surface is not visible or has no size.");
        }
        DpiScale dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        if (content is System.Windows.Controls.ContextMenu)
        {
            var background = new DrawingVisual();
            using (DrawingContext drawing = background.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(content.RenderSize));
            }
            bitmap.Render(background);
        }
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string path = Path.Combine(DirectoryPath, $"{++_step:00}-{stepName}.png");
        using (FileStream stream = File.Create(path))
        {
            encoder.Save(stream);
        }
        _testContext.WriteLine(path);
        _testContext.AddResultFile(path);
        return path;
    }

    public void Dispose() => Window.Close();
}
