using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class CpaQuotaScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SettingsRenderMultipleIndependentQuotaServicesAsync()
    {
        Lang.SetCurrent("zh-Hans");
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext,
            """{"PrimaryModel":"Preview","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline","ModelDefinitions":[{"ModelName":"Preview","ModelId":"preview","Provider":"openai"}]}]}""");
        var model = session.Model.SettingsViewModel!;
        Assert.IsFalse(model.HasCpaServices);
        model.AddCpaServiceCommand.Execute(null);
        model.CpaServices[0].Address = "https://aabbcc.xx.com";
        model.CpaServices[0].ManagementKey = "offline-test-secret";
        model.CpaServices[0].IsPrimary = true;
        model.AddCpaServiceCommand.Execute(null);
        model.CpaServices[1].Address = "https://monitor.example.com";
        model.CpaServices[1].ManagementKey = "another-offline-secret";
        using var client = new System.Net.Http.HttpClient(new QuotaResponseHandler());
        using var populated = new PrismCodingAgent.CodingShell.ViewModels.CpaQuotaServiceViewModel(_ => { }, () => { }, client)
        {
            Address = "https://aabbcc.xx.com", ManagementKey = "offline-test-secret", IsPrimary = true,
        };
        await populated.RefreshAsync();
        model.CpaServices[0].Dispose();
        model.CpaServices[0] = populated;
        model.CpaServices[1].Alias = "example";
        session.Model.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        session.Window.UpdateLayout();
        var view = session.Window.GetVisualDescendants().OfType<SettingsView>().First();
        view.FindControl<Border>("CpaQuotaSettings")!.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        session.Window.UpdateLayout();
        var secret = view.GetVisualDescendants().OfType<SecretInput>()
            .Single(input => ReferenceEquals(input.DataContext, populated));
        Assert.IsFalse(secret.RevealPassword);
        var reveal = secret.FindControl<Avalonia.Controls.Primitives.ToggleButton>("RevealButton")!;
        session.Click(reveal);
        Assert.IsTrue(secret.RevealPassword);
        Assert.AreEqual(populated.ManagementKey, secret.Text);
        session.Click(reveal);
        Assert.IsFalse(secret.RevealPassword);
        var main = session.Window.GetVisualDescendants().OfType<MainView>().Single();
        var settingsButton = main.FindControl<Button>("SettingsButton")!;
        var quotaLabel = main.FindControl<TextBlock>("PrimaryQuotaLabel")!;
        var navigationText = settingsButton.GetVisualDescendants().OfType<TextBlock>().First();
        TestContext.WriteLine($"Navigation Font={navigationText.FontFamily}, Size={navigationText.FontSize}, Weight={navigationText.FontWeight}, Bounds={navigationText.Bounds}, Origin={navigationText.TranslatePoint(default, main)}");
        TestContext.WriteLine($"Quota Font={quotaLabel.FontFamily}, Size={quotaLabel.FontSize}, Weight={quotaLabel.FontWeight}, Bounds={quotaLabel.Bounds}, Origin={quotaLabel.TranslatePoint(default, main)}");
        foreach (var text in new[] { navigationText, quotaLabel })
        {
            var manager = Avalonia.Media.FontManager.Current;
            bool matchedCharacter = manager.TryMatchCharacter('额', text.FontStyle, text.FontWeight, text.FontStretch, text.FontFamily,
                System.Globalization.CultureInfo.CurrentCulture, out var matched);
            Assert.IsTrue(matchedCharacter);
            TestContext.WriteLine($"Text={text.Text}, MatchedTypeface={matched.FontFamily}/{matched.Weight}, Rendering={Avalonia.Media.TextOptions.GetTextOptions(text)}, Transform={text.RenderTransform}, Opacity={text.Opacity}");
        }
        TestContext.WriteLine($"Quota TextBlockCount={main.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Name == "PrimaryQuotaLabel")}");
        Assert.AreEqual(14d, quotaLabel.FontSize);
        Assert.AreEqual(settingsButton.FontFamily, quotaLabel.FontFamily);
        Assert.AreEqual(Avalonia.Media.FontWeight.Normal, quotaLabel.FontWeight);
        string directory = Path.Combine(AppContext.BaseDirectory, "QuotaScreenshots", "Current");
        Directory.CreateDirectory(directory);
        using var frame = session.Window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        string path = Path.Combine(directory, "settings.png");
        frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        TestContext.AddResultFile(path);
        Assert.IsTrue(model.HasCpaServices);
        Assert.AreEqual("xx", model.CpaServices[0].Alias);
        await ((PrismCodingAgent.CodingShell.ViewModels.SimpleAsyncCommand)model.SaveCommand).ExecuteAsync();
        await model.LoadAsync();
        Assert.AreEqual(2, model.CpaServices.Count);
        Assert.AreEqual("offline-test-secret", model.CpaServices[0].ManagementKey);
        Assert.IsTrue(model.CpaServices[0].IsPrimary);
    }

    private sealed class QuotaResponseHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("""{"files":[{"account":"preview@example.com","quota":{"observed_at":"2026-11-06T11:44:49+08:00","signals":{"X-Codex-Primary-Used-Percent":"39","X-Codex-Primary-Window-Minutes":"10080","X-Codex-Primary-Reset-At":"1794298229"}}}]}""")
            });
    }
}
