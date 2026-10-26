using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ShellResourceTests
{
    [TestMethod]
    public void ShellAvatarShouldLoadFromPackagedResources()
    {
        var uri = new Uri("avares://PrismCodingAgent/Assets/Icons/CodingChatRoom_48x48.png");

        using var stream = AssetLoader.Open(uri);
        using var bitmap = new Bitmap(stream);

        Assert.AreEqual(new PixelSize(48, 48), bitmap.PixelSize);
    }

    [TestMethod]
    public void ShellLogoShouldLoadFromPackagedSvg()
    {
        var uri = new Uri("avares://PrismCodingAgent/Assets/Icons/coding-chat.svg");

        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        string svg = reader.ReadToEnd();

        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 24 24\"", svg, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ShellLogoGeometryShouldBeAvailableAsResource()
    {
        Assert.IsTrue(Application.Current!.TryGetResource("AppLogoGeometry", null, out object? value),
            "应用标识的矢量几何应作为 AppLogoGeometry 资源提供。");
        Assert.IsInstanceOfType<StreamGeometry>(value);
    }

    [TestMethod]
    public void MainWindowShouldConstructWithItsResources()
    {
        var window = new PrismCodingAgent.AvaloniaShell.MainWindow
        {
            DataContext = new MainViewModel(),
        };

        try
        {
            Assert.IsNotNull(window.Content);
        }
        finally
        {
            window.Close();
        }
    }
}
