using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfModelCatalogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CatalogLoadingCancellationFailureAndRecoveryThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "ModelCatalogData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var content = (FrameworkElement)window.Content;
            window.UpdateLayout();
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => Descendants(window).OfType<SettingsView>().Any(), "Settings did not open.");
            var settings = Descendants(window).OfType<SettingsView>().Single();
            await WpfInput.WaitAsync(() => Descendants(settings).OfType<OpenAIModelCatalogView>().Any(), "Catalog did not open.");
            var catalog = Descendants(settings).OfType<OpenAIModelCatalogView>().Single();
            var endpoint = Descendants(settings).OfType<TextBox>().Single(x => x.Name == "EndpointInput");
            var panel = (StackPanel)catalog.FindName("CatalogPanel");
            var vm = (ModelCatalogViewModel)panel.DataContext;
            var discover = (Button)catalog.FindName("DiscoverButton");
            var progress = (ProgressBar)catalog.FindName("CatalogProgress");
            var status = (TextBlock)catalog.FindName("CatalogStatus");
            string address = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/v1";
            await ReplaceAsync(endpoint, address);
            discover.BringIntoView();
            await WpfInput.ClickAsync(discover);
            using TcpClient pending = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation);
            string request = await ReadRequestAsync(pending);
            StringAssert.StartsWith(request, "GET /v1/models HTTP/1.1");
            await WpfInput.WaitAsync(() => vm.IsBusy && !discover.IsEnabled && progress.IsVisible, "Loading feedback missing.");
            Assert.AreEqual(Lang.Current.SetupCatalogLoading, status.Text);
            await screenshots.CaptureAsync("catalog-loading");

            await ReplaceAsync(endpoint, address + "/changed");
            await WpfInput.WaitAsync(() => !vm.IsBusy && discover.IsEnabled && !progress.IsVisible && string.IsNullOrEmpty(status.Text), "Connection change did not cancel loading.");
            Assert.AreEqual(0, vm.Models.Count);
            pending.Dispose();

            discover.BringIntoView();
            await WpfInput.ClickAsync(discover);
            using (TcpClient failure = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation))
            {
                StringAssert.StartsWith(await ReadRequestAsync(failure), "GET /v1/changed/models HTTP/1.1");
                await RespondAsync(failure, "401 Unauthorized", "{\"error\":{\"message\":\"controlled failure\",\"type\":\"invalid_request_error\"}}");
            }
            await WpfInput.WaitAsync(() => !vm.IsBusy && status.Text == Lang.Current.SetupCatalogFailed && discover.IsEnabled, "Failure feedback missing.");
            Assert.AreEqual(0, vm.Models.Count);
            await screenshots.CaptureAsync("catalog-failed");

            await WpfInput.ClickAsync(discover);
            using (TcpClient success = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation))
            {
                await ReadRequestAsync(success);
                await RespondAsync(success, "200 OK", "{\"object\":\"list\",\"data\":[{\"id\":\"local-catalog-model\",\"object\":\"model\",\"created\":0,\"owned_by\":\"local\"}]}");
            }
            await WpfInput.WaitAsync(() => !vm.IsBusy && status.Text == Lang.Current.SetupCatalogReady && vm.Models.Count == 1, "Recovery feedback missing.");
            Assert.AreEqual("local-catalog-model", vm.Models.Single());
            await screenshots.CaptureAsync("catalog-recovered");
            var add = Descendants(catalog).OfType<Button>().Single(x => Equals(x.CommandParameter, "local-catalog-model"));
            add.BringIntoView();
            await WpfInput.ClickAsync(add);
            await WpfInput.WaitAsync(() => status.Text == Lang.Current.SetupModelAdded && vm.Models.Count == 0, "Discovered model was not added.");
            Assert.IsTrue(((SettingsViewModel)settings.DataContext).Providers.Single().Models.Any(x => x.ModelId == "local-catalog-model"));
        }
        finally
        {
            listener.Stop();
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Window did not close.");
            await ((MainViewModel)window.DataContext).DisposeAsync();
        }
    });

    private static async Task ReplaceAsync(TextBox input, string text)
    {
        input.BringIntoView();
        await WpfInput.ClickAsync(input);
        await WpfInput.WaitAsync(() => input.IsKeyboardFocused, "Endpoint did not receive focus.");
        input.SelectAll();
        WpfInput.Text(text);
        await WpfInput.WaitAsync(() => input.Text == text, "Endpoint input was not processed.");
    }

    private static async Task<string> ReadRequestAsync(TcpClient client)
    {
        var buffer = new byte[1];
        var request = new StringBuilder();
        while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            int count = await client.GetStream().ReadAsync(buffer, WpfTestSession.Cancellation);
            if (count == 0) throw new EndOfStreamException("HTTP request ended before headers.");
            request.Append((char)buffer[0]);
        }
        return request.ToString();
    }

    private static async Task RespondAsync(TcpClient client, string status, string body)
    {
        byte[] payload = Encoding.UTF8.GetBytes(body);
        byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await client.GetStream().WriteAsync(headers, WpfTestSession.Cancellation);
        await client.GetStream().WriteAsync(payload, WpfTestSession.Cancellation);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
