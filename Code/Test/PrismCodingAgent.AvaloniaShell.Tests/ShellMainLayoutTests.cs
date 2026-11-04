using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Infrastructure;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ShellMainLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RealStartupRendersMatchingClientSizesAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "MainLayoutScreenshots", Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
        string root = Path.Combine(directory, "AppData");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"),
            JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        var model = await ShellStartup.InitializeAsync(CodingChatRoomPaths.Create(root), new AvaloniaMainThreadDispatcher());
        var window = new CodingShell.MainWindow { DataContext = model, Width = 1440, Height = 900 };
        window.Show();
        try
        {
            Capture("main-1440x900");
            Assert.AreEqual(1440d, window.ClientSize.Width);
            Assert.AreEqual(900d, window.ClientSize.Height);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "main-layout.txt"),
                $"{Lang.Current.IetfLanguageTag}; client={window.ClientSize}; dpi={window.RenderScaling * 96}; appData={root}{Environment.NewLine}");
            window.Width = 1280;
            window.Height = 720;
            Capture("main-1280x720");
        }
        finally
        {
            await model.DisposeAsync();
            window.Close();
            var button = window.FindControl<Button>("ConfirmExitButton")!;
            window.UpdateLayout();
            var point = button.TranslatePoint(new Avalonia.Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, Avalonia.Input.MouseButton.Left);
            window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        void Capture(string name)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame.");
            string path = Path.Combine(directory, name + ".png");
            frame.Save(path, new PngBitmapEncoderOptions());
            TestContext.AddResultFile(path);
            TestContext.WriteLine(path);
        }
    }
}
