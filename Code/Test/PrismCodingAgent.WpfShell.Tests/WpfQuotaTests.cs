using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfQuotaTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en")]
    [DataRow("de")]
    public async Task QuotaSettingsRenderAndPersistAsync(string language) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        Lang.SetCurrent(language);
        string root = Path.Combine(AppContext.BaseDirectory, "QuotaTestData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), System.Text.Json.JsonSerializer.Serialize(new { LanguageTag = language }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"Preview","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline","ModelDefinitions":[{"ModelName":"Preview","ModelId":"preview","Provider":"openai"}]}]}""");
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var main = (MainViewModel)window.DataContext;
        window.Closing += (_, args) => args.Cancel = false;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            var settings = main.SettingsViewModel!;
            settings.Quotas.AddServiceCommand.Execute(null);
            var service = settings.Quotas.Services.Single();
            service.Address = "https://preview.example.com";
            service.ManagementKey = "offline-secret";
            main.OpenSettingsCommand.Execute(null);
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var view = Descendants(window).OfType<SettingsView>().First();
            ((Border)view.FindName("CpaQuotaSettings")).BringIntoView();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var secret = Descendants(view).OfType<SecretInput>().Single(input => ReferenceEquals(input.DataContext, service));
            Assert.AreEqual("offline-secret", secret.Text);
            Assert.IsTrue(service.IsPrimary);
            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)window.FindName("PrimaryQuotaLabel")).Visibility);
            string directory = Path.Combine(AppContext.BaseDirectory, "QuotaScreenshots", language);
            Directory.CreateDirectory(directory);
            File.Copy(await screenshots.CaptureAsync("quota-settings"), Path.Combine(directory, "settings.png"), true);
            settings.SaveCommand.Execute(null);
            await WpfInput.WaitAsync(() => !settings.IsBusy, "Settings save did not complete.");
            Assert.IsTrue((await File.ReadAllTextAsync(Path.Combine(root, "ShellSettings.json"))).Contains("example"));
            settings.Quotas.RemoveServiceCommand.Execute(service);
            Assert.IsEmpty(settings.Quotas.Services);
        }
        finally { window.Close(); await main.DisposeAsync(); }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
