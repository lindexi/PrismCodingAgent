using System.IO;
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
public sealed class WpfGeneralSettingsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LanguageProxyAndSandboxPersistThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string original = Lang.Current.IetfLanguageTag;
        string other = original == "en" ? "zh-Hans" : "en";
        string root = Path.Combine(AppContext.BaseDirectory, "Phase5Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "ShellSettings.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { LanguageTag = original, IsWindowsSandboxEnabled = false }));
        TestContext.WriteLine($"Isolated settings: {file}");
        for (int pass = 0; pass < 3; pass++)
        {
            var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
            var model = (MainViewModel)window.DataContext;
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            try
            {
                window.UpdateLayout();
                var content = (FrameworkElement)window.Content;
                window.Width += 1440 - content.ActualWidth;
                window.Height += 900 - content.ActualHeight;
                Assert.AreEqual(1d, VisualTreeHelper.GetDpi(window).DpiScaleX);
                await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
                await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
                var settings = Descendants(window).OfType<SettingsView>().Single();
                var vm = model.SettingsViewModel!;
                var language = (ComboBox)settings.FindName("LanguageSelector");
                var proxy = (CheckBox)settings.FindName("ProxyEnabled");
                var address = (TextBox)settings.FindName("ProxyAddress");
                var bypass = (CheckBox)settings.FindName("ProxyBypass");
                var sandbox = (CheckBox)settings.FindName("SandboxEnabled");
                var path = (TextBox)settings.FindName("SandboxPath");
                var server = (TextBox)settings.FindName("SandboxAddress");
                var save = (Button)settings.FindName("SaveButton");
                var back = (Button)settings.FindName("BackButton");
                var scroll = Descendants(settings).OfType<ScrollViewer>().First();
                Assert.AreEqual(pass == 1 ? other : original, Lang.Current.IetfLanguageTag);
                Assert.AreEqual(pass == 1, proxy.IsChecked);
                Assert.AreEqual(pass == 1, address.IsEnabled);
                Assert.AreEqual(pass == 1, sandbox.IsChecked);
                Assert.AreEqual(pass == 1, path.IsEnabled);
                if (pass > 0)
                {
                    Assert.AreEqual("http://127.0.0.1:18888", address.Text);
                    Assert.AreEqual(false, bypass.IsChecked);
                    Assert.AreEqual("C:\\Offline\\WinRemoteShell.exe", path.Text);
                    Assert.AreEqual("127.0.0.1:12398", server.Text);
                }
                await screenshots.CaptureAsync($"pass-{pass}-language-restored");
                if (pass == 0)
                {
                    bypass.BringIntoView();
                    await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    await screenshots.CaptureAsync("proxy-disabled-checked-complete");
                }
                if (pass == 2)
                {
                    bypass.BringIntoView();
                    await screenshots.CaptureAsync("pass-2-proxy-restored");
                    continue;
                }
                if (pass == 0)
                {
                    await ToggleAsync(proxy);
                    await ReplaceAsync(address, "http://127.0.0.1:18888");
                    bypass.BringIntoView();
                    await screenshots.CaptureAsync("proxy-enabled-checked");
                    await ToggleAsync(bypass);
                    await screenshots.CaptureAsync("proxy-enabled-unchecked");
                    await ToggleAsync(sandbox);
                    // A blank tool path keeps the production tester disabled during address focus loss.
                    await ReplaceAsync(path, "");
                    await ReplaceAsync(server, "127.0.0.1:12398");
                    await ReplaceAsync(path, "C:\\Offline\\WinRemoteShell.exe");
                    await WpfInput.ClickAsync(back);
                    await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Back did not close settings.");
                    using (var unsaved = JsonDocument.Parse(await File.ReadAllTextAsync(file)))
                        Assert.IsFalse(unsaved.RootElement.TryGetProperty("NetworkProxyAddress", out _), "Back must not save drafts.");
                    await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
                    await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Draft did not reopen.");
                    Assert.AreEqual("http://127.0.0.1:18888", vm.NetworkProxyAddress);
                }
                else
                {
                    await ToggleAsync(proxy);
                    await ToggleAsync(sandbox);
                    Assert.IsFalse(address.IsEnabled);
                    Assert.IsFalse(path.IsEnabled);
                }
                scroll.ScrollToTop();
                language.BringIntoView();
                await WpfInput.ClickAsync(language);
                WpfInput.Key(pass == 0 ? (other == "en" ? (ushort)0x23 : (ushort)0x24) : (original == "en" ? (ushort)0x23 : (ushort)0x24));
                WpfInput.Key(0x0D);
                string target = pass == 0 ? other : original;
                await WpfInput.WaitAsync(() => vm.LanguageTag == target, "Language selector did not bind.");
                Assert.AreNotEqual(target, Lang.Current.IetfLanguageTag, "Language applies only when saved.");
                await WpfInput.ClickAsync(save);
                await WpfInput.WaitAsync(() => !vm.IsBusy && Lang.Current.IetfLanguageTag == target, "Language save did not apply.");
                Assert.IsFalse(vm.IsStatusError);
                using (var saved = JsonDocument.Parse(await File.ReadAllTextAsync(file)))
                {
                    Assert.AreEqual(target, saved.RootElement.GetProperty("LanguageTag").GetString());
                    Assert.AreEqual(pass == 0, saved.RootElement.GetProperty("IsNetworkProxyEnabled").GetBoolean());
                    Assert.AreEqual(pass == 0, saved.RootElement.GetProperty("IsWindowsSandboxEnabled").GetBoolean());
                }
                await screenshots.CaptureAsync($"pass-{pass}-language-saved");
                sandbox.BringIntoView();
                await screenshots.CaptureAsync($"pass-{pass}-sandbox-saved");
                bypass.BringIntoView();
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                await screenshots.CaptureAsync($"pass-{pass}-proxy-saved");
                await WpfInput.ClickAsync(back);
                await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Saved settings did not close.");
                await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
                await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Saved settings did not reopen.");
                Assert.AreEqual(pass == 0, vm.IsNetworkProxyEnabled);
                Assert.AreEqual(target, vm.LanguageTag);
            }
            finally
            {
                window.Close();
                await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
                await WpfInput.WaitAsync(() => !window.IsVisible, "Window did not close.");
                await model.DisposeAsync();
            }
        }
    });

    [TestMethod]
    public async Task DeepSeekAndInstructionsPersistThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "Phase5Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "ShellSettings.json");
        string instruction = Path.Combine(root, "offline-instructions.md");
        await File.WriteAllTextAsync(instruction, "Use the offline acceptance convention.");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false }));
        string secret = Guid.NewGuid().ToString("N");
        TestContext.WriteLine($"Isolated settings: {file}");
        for (int pass = 0; pass < 3; pass++)
        {
            var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
            var model = (MainViewModel)window.DataContext;
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            try
            {
                window.UpdateLayout();
                var content = (FrameworkElement)window.Content;
                window.Width += 1440 - content.ActualWidth;
                window.Height += 900 - content.ActualHeight;
                Assert.AreEqual(1d, VisualTreeHelper.GetDpi(window).DpiScaleX);
                await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
                await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
                var settings = Descendants(window).OfType<SettingsView>().Single();
                var vm = model.SettingsViewModel!;
                var input = (SecretInput)settings.FindName("DeepSeekApiKeyInput");
                var masked = (PasswordBox)input.FindName("MaskedInput");
                var revealed = (TextBox)input.FindName("RevealedInput");
                var reveal = (System.Windows.Controls.Primitives.ToggleButton)input.FindName("RevealButton");
                var enabled = (CheckBox)settings.FindName("InstructionsEnabled");
                var path = (TextBox)settings.FindName("InstructionsPath");
                var save = (Button)settings.FindName("SaveButton");
                var back = (Button)settings.FindName("BackButton");
                Assert.IsTrue(masked.IsVisible);
                Assert.IsFalse(revealed.IsVisible);
                Assert.IsTrue(string.Equals(vm.DeepSeekApiKey, pass == 1 ? secret : null, StringComparison.Ordinal) || (pass != 1 && string.IsNullOrEmpty(vm.DeepSeekApiKey)), "Independent key restore mismatch.");
                Assert.AreEqual(pass == 1, enabled.IsChecked);
                Assert.AreEqual(pass == 1, path.IsEnabled);
                if (pass > 0) Assert.AreEqual(instruction, path.Text);
                if (pass == 2)
                {
                    enabled.BringIntoView();
                    await screenshots.CaptureAsync("disabled-restored");
                    continue;
                }
                input.BringIntoView();
                await WpfInput.ClickAsync(masked);
                WpfInput.ControlKey(0x41);
                WpfInput.Key(0x2E);
                WpfInput.Text(pass == 0 ? secret : "");
                await WpfInput.WaitAsync(() => string.Equals(vm.DeepSeekApiKey ?? "", pass == 0 ? secret : "", StringComparison.Ordinal), "Independent key input mismatch.");
                await WpfInput.ClickAsync(reveal);
                await WpfInput.WaitAsync(() => revealed.IsVisible, "Reveal did not become visible.");
                Assert.IsTrue(revealed.IsVisible);
                Assert.IsTrue(string.Equals(revealed.Text, pass == 0 ? secret : "", StringComparison.Ordinal), "Reveal mismatch.");
                await WpfInput.ClickAsync(reveal);
                await WpfInput.WaitAsync(() => !revealed.IsVisible, "Reveal did not hide.");
                Assert.IsFalse(revealed.IsVisible);
                Assert.IsTrue(string.IsNullOrEmpty(revealed.Text), "Hidden plaintext must be cleared.");
                await screenshots.CaptureAsync($"pass-{pass}-key-masked");
                enabled.BringIntoView();
                await ToggleAsync(enabled);
                Assert.AreEqual(pass == 0, path.IsEnabled);
                if (pass == 0)
                {
                    await ReplaceAsync(path, instruction);
                    await WpfInput.ClickAsync(back);
                    await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Draft did not close.");
                    using (var unsaved = JsonDocument.Parse(await File.ReadAllTextAsync(file)))
                    {
                        Assert.IsFalse(unsaved.RootElement.TryGetProperty("DeepSeekApiKey", out _), "Draft key was saved by Back.");
                        Assert.IsFalse(unsaved.RootElement.TryGetProperty("CopilotInstructionsPath", out _), "Draft path was saved by Back.");
                    }
                    await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
                    await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Draft did not reopen.");
                    Assert.IsTrue(string.Equals(vm.DeepSeekApiKey, secret, StringComparison.Ordinal), "Draft key lost.");
                    Assert.AreEqual(instruction, vm.CopilotInstructionsPath);
                    Assert.IsTrue(vm.IsCopilotInstructionsEnabled);
                }
                await WpfInput.ClickAsync(save);
                await WpfInput.WaitAsync(() => !vm.IsBusy && vm.HasStatusMessage, "Settings save did not finish.");
                Assert.IsFalse(vm.IsStatusError);
                using (var saved = JsonDocument.Parse(await File.ReadAllTextAsync(file)))
                {
                    Assert.IsTrue(string.Equals(saved.RootElement.GetProperty("DeepSeekApiKey").GetString() ?? "", pass == 0 ? secret : "", StringComparison.Ordinal), "Saved independent key mismatch.");
                    Assert.AreEqual(pass == 0, saved.RootElement.GetProperty("IsCopilotInstructionsEnabled").GetBoolean());
                    Assert.AreEqual(instruction, saved.RootElement.GetProperty("CopilotInstructionsPath").GetString());
                }
                path.BringIntoView();
                var scroll = Descendants(settings).OfType<ScrollViewer>().First();
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 240);
                await screenshots.CaptureAsync($"pass-{pass}-instructions-saved");
                await WpfInput.ClickAsync(back);
                await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Saved settings did not close.");
                await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
                await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Saved settings did not reopen.");
                Assert.AreEqual(pass == 0, vm.IsCopilotInstructionsEnabled);
            }
            finally
            {
                window.Close();
                await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
                await WpfInput.WaitAsync(() => !window.IsVisible, "Window did not close.");
                await model.DisposeAsync();
            }
        }
    });

    [TestMethod]
    public async Task EmptyDisabledSettingsShowPlaceholdersThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "Phase5Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag,
            IsWindowsSandboxEnabled = true,
            IsNetworkProxyEnabled = false,
            NetworkProxyAddress = "",
            BypassProxyOnLocal = true,
            IsCopilotInstructionsEnabled = false,
            CopilotInstructionsPath = ""
        }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
            var settings = Descendants(window).OfType<SettingsView>().Single();
            var scroll = Descendants(settings).OfType<ScrollViewer>().First();
            var proxy = (TextBox)settings.FindName("ProxyAddress");
            var instructions = (TextBox)settings.FindName("InstructionsPath");
            var bypass = (CheckBox)settings.FindName("ProxyBypass");
            async Task RevealAsync(FrameworkElement target)
            {
                for (int i = 0; i < 30; i++)
                {
                    window.UpdateLayout();
                    double top = target.TranslatePoint(new Point(), scroll).Y;
                    if (top >= 120 && top + target.ActualHeight < scroll.ViewportHeight - 100) return;
                    await WpfInput.WheelAsync(scroll, top < 120 ? 120 : -120);
                    await Task.Delay(50, WpfTestSession.Cancellation);
                }
                Assert.Fail("Settings region did not enter the viewport through real wheel input.");
            }
            async Task VerifyEmptyAsync(TextBox input, string expected, string shot)
            {
                await RevealAsync(input);
                input.ApplyTemplate();
                var placeholder = (TextBlock)input.Template.FindName("Placeholder", input);
                Assert.AreEqual("", input.Text);
                Assert.IsFalse(input.IsEnabled);
                Assert.AreEqual(expected, placeholder.Text);
                Assert.IsTrue(placeholder.IsVisible);
                var surface = (Border)input.Template.FindName("InputSurface", input);
                Assert.AreEqual(Color.FromRgb(204, 204, 204), ((SolidColorBrush)surface.Background).Color);
                await screenshots.CaptureAsync(shot);
            }
            await VerifyEmptyAsync(proxy, Lang.Current.NetworkProxyAddressPlaceholder, "empty-proxy-disabled");
            Assert.AreEqual(true, bypass.IsChecked);
            Assert.IsFalse(bypass.IsEnabled);
            await WpfInput.ClickAsync((CheckBox)settings.FindName("ProxyEnabled"));
            await ReplaceAsync(proxy, "http://127.0.0.1:18888");
            Assert.IsFalse(((TextBlock)proxy.Template.FindName("Placeholder", proxy)).IsVisible);
            await ReplaceAsync(proxy, "");
            await WpfInput.ClickAsync((CheckBox)settings.FindName("ProxyEnabled"));
            await VerifyEmptyAsync(instructions, Lang.Current.InstructionsPlaceholder, "empty-instructions-disabled");
            Assert.IsNotNull(settings.FindName("InstallLanguageServerButton"));
            Assert.IsNotNull(settings.FindName("CopyLanguageServerCommandButton"));
            Assert.IsNotNull(settings.FindName("LanguageServerStatus"));
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Window did not close.");
            await model.DisposeAsync();
        }
    });

    private static async Task ToggleAsync(CheckBox input)
    {
        var window = Window.GetWindow(input);
        window?.UpdateLayout();
        var scroll = Descendants(window!).OfType<SettingsView>()
            .SelectMany(Descendants).OfType<ScrollViewer>().First();
        await input.Dispatcher.InvokeAsync(() =>
        {
            window?.UpdateLayout();
            double top = input.TransformToAncestor(scroll).Transform(new Point()).Y;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + top - scroll.ViewportHeight / 2);
            window?.UpdateLayout();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WpfInput.WaitAsync(() =>
        {
            window?.UpdateLayout();
            Point center = input.TranslatePoint(new Point(input.ActualWidth / 2, input.ActualHeight / 2), scroll);
            return input.IsVisible && center.Y >= 0 && center.Y < scroll.ViewportHeight;
        }, "Toggle did not enter the settings viewport.");
        bool? previous = input.IsChecked;
        await WpfInput.ClickAsync(input);
        await WpfInput.WaitAsync(() => input.IsChecked != previous, "Toggle did not change.");
    }

    private static async Task ReplaceAsync(TextBox input, string value)
    {
        input.BringIntoView();
        await WpfInput.ClickAsync(input);
        WpfInput.Key(0x24);
        int length = input.Text.Length;
        for (int i = 0; i < length; i++) WpfInput.Key(0x2E);
        WpfInput.Text(value);
        await WpfInput.WaitAsync(() => input.Text == value, "Text did not update.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
