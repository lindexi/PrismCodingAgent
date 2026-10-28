using Avalonia.Controls;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>从真实主窗口和用户输入出发，输出可供开发时阅读的界面截图。</summary>
[AvaloniaTestClass]
public sealed class ShellScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MainWindowShouldRenderAndOpenSettingsThroughMouseInput()
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        Lang.SetCurrent("zh-Hans");
        try
        {
            await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
            string mainScreenshot = session.Capture("main-window");
            Assert.IsGreaterThan(0L, new FileInfo(mainScreenshot).Length);

            Button settings = session.Window.GetVisualDescendants().OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, session.Model.OpenSettingsCommand));
            session.Click(settings);
            session.Capture("settings");

            Assert.IsTrue(session.Model.IsSettingsOpen);
        }
        finally
        {
            Lang.SetCurrent(originalLanguage);
        }
    }
}
