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

    internal async Task<string> CaptureAsync(string stepName)
    {
        Window.Dispatcher.VerifyAccess();
        await Window.Dispatcher.InvokeAsync(Window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        var content = (FrameworkElement)Window.Content;
        DpiScale dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
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
