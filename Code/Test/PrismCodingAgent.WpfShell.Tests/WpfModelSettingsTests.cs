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
public sealed class WpfModelSettingsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OfflineModelConfigurationThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase5Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"),
            JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            await screenshots.CaptureAsync("without-model");
            var guide = (UserControl)window.FindName("ModelGuide");
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
            var settings = Descendants(window).OfType<SettingsView>().Single();
            var editor = Descendants(settings).OfType<ModelSettingsView>().Single();
            var catalog = Descendants(editor).OfType<OpenAIModelCatalogView>().Single();
            await screenshots.CaptureAsync("settings-opened");
            var add = (Button)catalog.FindName("AddManualButton");
            add.BringIntoView();
            await WpfInput.ClickAsync(add);
            await WpfInput.WaitAsync(() => ((TextBlock)catalog.FindName("CatalogStatus")).Text == Lang.Current.SetupModelRequired,
                "Empty model validation did not appear.");
            await screenshots.CaptureAsync("empty-model-validation");
            var endpoint = Descendants(editor).OfType<TextBox>().Single(input => input.Name == "EndpointInput");
            await ReplaceAsync(endpoint, "https://model-settings.invalid/v1");
            var secret = Descendants(editor).OfType<SecretInput>().Single();
            await WpfInput.ClickAsync((PasswordBox)secret.FindName("MaskedInput"));
            WpfInput.Text("screenshot-only-key");
            await WpfInput.WaitAsync(() => model.SettingsViewModel!.Providers.Single().ApiKey == "screenshot-only-key", "Key input was not bound.");
            await screenshots.CaptureAsync("service-connection-edited");
            await ReplaceAsync((TextBox)catalog.FindName("ManualModelId"), "screenshot-offline-model");
            await WpfInput.ClickAsync(add);
            await WpfInput.WaitAsync(() => model.SettingsViewModel!.Providers.Single().Models.Count == 1, "Manual model was not added.");
            window.UpdateLayout();
            await screenshots.CaptureAsync("model-added");
            var lastField = Descendants(editor).OfType<TextBox>().Single(input => input.Name == "OutputSizeInput");
            lastField.BringIntoView();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var scroll = Descendants(settings).OfType<ScrollViewer>().First();
            Point fieldPosition = lastField.TranslatePoint(default, scroll);
            Assert.IsTrue(fieldPosition.Y >= 0 && fieldPosition.Y + lastField.ActualHeight <= scroll.ViewportHeight, "Output field must be fully reachable.");
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(window).PixelsPerInchX);
            scroll.ScrollToEnd();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var name = Descendants(editor).OfType<TextBox>().Single(input => input.Name == "ModelNameInput");
            await ReplaceAsync(name, "Edited offline model");
            Assert.IsTrue(Descendants(editor).OfType<CheckBox>().Any(option => Equals(option.Content, Lang.Current.ModelFastText)));
            scroll.ScrollToEnd();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            await screenshots.CaptureAsync("model-fields-bottom");
            await WpfInput.ClickAsync((Button)settings.FindName("BackButton"));
            await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Back did not close settings.");
            Assert.IsTrue(guide.IsVisible);
            Assert.IsFalse(File.Exists(Path.Combine(root, "AgentConfiguration.json")));
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not reopen.");
            Assert.AreEqual("Edited offline model", name.Text);
            await WpfInput.ClickAsync((Button)settings.FindName("SaveButton"));
            await WpfInput.WaitAsync(() => model.SettingsViewModel!.StatusMessage == Lang.Current.SettingsSaved, "Settings were not saved.");
            await screenshots.CaptureAsync("saved");
            await WpfInput.ClickAsync((Button)settings.FindName("BackButton"));
            await WpfInput.WaitAsync(() => !model.IsSettingsOpen, "Saved settings did not return.");
            Assert.IsFalse(guide.IsVisible);
            Assert.AreEqual("Edited offline model", model.SettingsViewModel!.Providers.Single().Models.Single().ModelName);
            await screenshots.CaptureAsync("guide-dismissed");
        }
        finally
        {
            await CloseAsync(window, model);
        }
        var restored = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var restoredModel = (MainViewModel)restored.DataContext;
        using var restoredScreenshots = new WpfScreenshotSession(restored, TestContext);
        try
        {
            SetContentSize(restored);
            Assert.AreEqual(1, restoredModel.ChatViewModel.AvailableModels.Count);
            await WpfInput.ClickAsync((Button)restored.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => restoredModel.IsSettingsOpen, "Restored settings did not open.");
            var provider = restoredModel.SettingsViewModel!.Providers.Single();
            Assert.AreEqual("https://model-settings.invalid/v1", provider.EndPoint);
            Assert.AreEqual("screenshot-only-key", provider.ApiKey);
            Assert.AreEqual("Edited offline model", provider.Models.Single().ModelName);
            await restoredScreenshots.CaptureAsync("restored");
            var editor = Descendants(restored).OfType<ModelSettingsView>().Single();
            var remove = Descendants(editor).OfType<Button>().Single(button => Equals(button.Content, "×"));
            remove.BringIntoView();
            await WpfInput.ClickAsync(remove);
            await WpfInput.WaitAsync(() => provider.Models.Count == 0, "Model was not removed.");
            var settings = Descendants(restored).OfType<SettingsView>().Single();
            await WpfInput.ClickAsync((Button)settings.FindName("SaveButton"));
            await WpfInput.WaitAsync(() => restoredModel.ChatViewModel.AvailableModels.Count == 0, "Deleted model was not applied.");
            await WpfInput.ClickAsync((Button)settings.FindName("BackButton"));
            await WpfInput.WaitAsync(() => !restoredModel.IsSettingsOpen, "Deletion did not return.");
            Assert.IsTrue(((UserControl)restored.FindName("ModelGuide")).IsVisible);
            await restoredScreenshots.CaptureAsync("removed-guide-restored");
        }
        finally
        {
            await CloseAsync(restored, restoredModel);
        }
        var deleted = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var deletedModel = (MainViewModel)deleted.DataContext;
        using var deletedScreenshots = new WpfScreenshotSession(deleted, TestContext);
        try
        {
            SetContentSize(deleted);
            Assert.AreEqual(0, deletedModel.ChatViewModel.AvailableModels.Count);
            Assert.IsTrue(((UserControl)deleted.FindName("ModelGuide")).IsVisible);
            var provider = deletedModel.SettingsViewModel!.Providers.Single();
            Assert.AreEqual(0, provider.Models.Count);
            using var savedConfiguration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "AgentConfiguration.json")));
            Assert.AreEqual(0, savedConfiguration.RootElement.GetProperty("OpenAIConfigurationList").GetArrayLength());
            await deletedScreenshots.CaptureAsync("deleted-restarted-without-model");
        }
        finally
        {
            await CloseAsync(deleted, deletedModel);
        }
    });

    [TestMethod]
    public async Task MultipleServicesAndPrimaryModelPersistThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase5Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"),
            JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var main = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            SetContentSize(window);
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => main.IsSettingsOpen, "Settings did not open.");
            var settings = Descendants(window).OfType<SettingsView>().Single();
            var editor = Descendants(settings).OfType<ModelSettingsView>().Single();
            var vm = main.SettingsViewModel!;
            var first = vm.Providers.Single();
            await EditServiceAsync(editor, first, "alpha", "key-alpha-offline");
            await ClickContentAsync(editor, Lang.Current.AddModelServiceText);
            await WpfInput.WaitAsync(() => vm.Providers.Count == 2, "Second service was not added.");
            var second = vm.Providers.Last();
            await EditServiceAsync(editor, second, "beta", "key-beta-offline");
            Assert.IsTrue(first.ApiKey == "key-alpha-offline" && second.ApiKey == "key-beta-offline", "Service keys must remain independent.");
            var primary = Descendants(settings).OfType<TextBox>().Single(input => input.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "PrimaryModel");
            await ReplaceAsync(primary, "beta/beta");
            var context = Field(editor, second.Models.Single(), "ContextSizeInput");
            var output = Field(editor, second.Models.Single(), "OutputSizeInput");
            foreach (var sample in new[] { ("", ""), ("0", "1"), ("-4", "1"), ("100000001", "100000000"), ("2147483648", "100000000"), ("invalid", "100000000") })
            {
                await ReplaceAsync(context, sample.Item1);
                await WpfInput.ClickAsync(output);
                await WpfInput.WaitAsync(() => context.Text == sample.Item2, "Context boundary was not committed.");
            }
            foreach (var sample in new[] { ("", ""), ("0", "1"), ("-2", "1"), ("10000001", "10000000"), ("2147483648", "10000000"), ("invalid", "10000000") })
            {
                await ReplaceAsync(output, sample.Item1);
                await WpfInput.ClickAsync(context);
                await WpfInput.WaitAsync(() => output.Text == sample.Item2, "Output boundary was not committed.");
            }
            await ReplaceAsync(context, "4096");
            var increment = Descendants((DependencyObject)context.Parent).OfType<Button>().Single(button => Equals(button.Content, "▲"));
            await WpfInput.ClickAsync(increment);
            await WpfInput.WaitAsync(() => context.Text == "4097", "Increment did not commit.");
            await WpfInput.ClickAsync(Descendants((DependencyObject)context.Parent).OfType<Button>().Single(button => Equals(button.Content, "▼")));
            await WpfInput.WaitAsync(() => context.Text == "4096", "Decrement did not commit.");
            await ReplaceAsync(output, "512");
            var betaModel = second.Models.Single();
            await ReplaceAsync(Field(editor, betaModel, "ModelIdInput"), "beta-api-edited");
            foreach (var option in Descendants(editor).OfType<CheckBox>().Where(option => ReferenceEquals(option.DataContext, betaModel)))
            {
                option.BringIntoView();
                await option.Dispatcher.InvokeAsync(() => option.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                bool? previous = option.IsChecked;
                await WpfInput.ClickAsync(option);
                await WpfInput.WaitAsync(() => option.IsChecked != previous, "Model capability did not change.");
            }
            await WpfInput.ClickAsync((Button)settings.FindName("BackButton"));
            Assert.IsFalse(File.Exists(Path.Combine(root, "AgentConfiguration.json")));
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            Assert.AreEqual("beta/beta", primary.Text);
            await SaveAndCheckPrimaryAsync(settings, root, "beta/beta");
            await screenshots.CaptureAsync("multi-service-primary-saved");
            await ReplaceAsync(primary, "beta");
            await SaveAndCheckPrimaryAsync(settings, root, "beta");
            await ReplaceAsync(primary, "");
            await SaveAndCheckPrimaryAsync(settings, root, "alpha/alpha");
            await ReplaceAsync(primary, "missing/model");
            await SaveAndCheckPrimaryAsync(settings, root, "alpha/alpha");
            await ReplaceAsync(primary, "beta/beta");
            await ReplaceAsync(Field(editor, betaModel, "ModelNameInput"), "beta-renamed");
            await SaveAndCheckPrimaryAsync(settings, root, "alpha/alpha");
            await ReplaceAsync(primary, "beta/beta-renamed");
            await SaveAndCheckPrimaryAsync(settings, root, "beta/beta-renamed");
            await screenshots.CaptureAsync("multi-service-renamed-restorable");
        }
        finally { await CloseAsync(window, main); }
        var restored = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var restoredMain = (MainViewModel)restored.DataContext;
        using var restoredShots = new WpfScreenshotSession(restored, TestContext);
        try
        {
            SetContentSize(restored);
            await WpfInput.ClickAsync((Button)restored.FindName("SettingsButton"));
            var settings = Descendants(restored).OfType<SettingsView>().Single();
            var editor = Descendants(settings).OfType<ModelSettingsView>().Single();
            var providers = restoredMain.SettingsViewModel!.Providers;
            Assert.AreEqual(2, providers.Count);
            Assert.AreEqual("https://alpha.invalid/v1", providers[0].EndPoint);
            Assert.AreEqual("https://beta.invalid/v1", providers[1].EndPoint);
            Assert.IsTrue(providers[0].ApiKey == "key-alpha-offline" && providers[1].ApiKey == "key-beta-offline", "Restored service keys differ.");
            Assert.AreEqual("beta/beta-renamed", restoredMain.SettingsViewModel.PrimaryModel);
            var beta = providers[1].Models.Single();
            Assert.AreEqual("beta-api-edited", beta.ModelId);
            Assert.AreEqual(4096, beta.ContextWindowSize);
            Assert.AreEqual(512, beta.MaxOutputTokens);
            await restoredShots.CaptureAsync("multi-service-open-restored");
            await ClickContentAsync(editor, "×", beta);
            await SaveAndCheckPrimaryAsync(settings, root, "alpha/alpha");
            await ClickContentAsync(editor, Lang.Current.RemoveModelServiceText, providers[1]);
            await WpfInput.WaitAsync(() => providers.Count == 1, "Service was not removed.");
            await ClickContentAsync(editor, Lang.Current.RemoveModelServiceText, providers[0]);
            await SaveAndCheckPrimaryAsync(settings, root, null);
            await restoredShots.CaptureAsync("multi-service-deleted");
        }
        finally { await CloseAsync(restored, restoredMain); }
        var empty = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var emptyMain = (MainViewModel)empty.DataContext;
        try
        {
            Assert.AreEqual(0, emptyMain.ChatViewModel.AvailableModels.Count);
            Assert.IsNull(emptyMain.SettingsViewModel!.PrimaryModel);
        }
        finally { await CloseAsync(empty, emptyMain); }
    });

    private static TextBox Field(ModelSettingsView editor, object model, string name)
        => Descendants(editor).OfType<TextBox>().Single(input => input.Name == name && ReferenceEquals(input.DataContext, model));

    private static async Task ClickContentAsync(ModelSettingsView editor, string content, object? data = null)
    {
        editor.UpdateLayout();
        await editor.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var candidates = Descendants(editor).OfType<Button>().ToArray();
        var button = candidates.SingleOrDefault(button => button.Content?.ToString() == content && (data is null || ReferenceEquals(button.DataContext, data)))
            ?? throw new InvalidOperationException($"Missing button {content}; visible buttons: {string.Join(" | ", candidates.Select(button => button.Content))}");
        button.BringIntoView();
        await WpfInput.ClickAsync(button);
    }

    private static async Task EditServiceAsync(ModelSettingsView editor, ProviderSettingsViewModel provider, string name, string key)
    {
        await ReplaceAsync(Field(editor, provider, "EndpointInput"), $"https://{name}.invalid/v1");
        var secret = Descendants(editor).OfType<SecretInput>().Single(input => ReferenceEquals(input.DataContext, provider));
        var masked = (PasswordBox)secret.FindName("MaskedInput");
        masked.BringIntoView();
        await editor.Dispatcher.InvokeAsync(() => editor.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WpfInput.ClickAsync(masked);
        await WpfInput.WaitAsync(() => masked.IsKeyboardFocusWithin, "Service password input did not receive keyboard focus.");
        WpfInput.Key(0x24);
        int passwordLength = masked.Password.Length;
        for (int i = 0; i < passwordLength; i++) WpfInput.Key(0x2E);
        WpfInput.Text(key);
        try
        {
            await WpfInput.WaitAsync(() => provider.ApiKey == key, "Service key input was not committed.");
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException($"Service key input failed: password length={masked.Password.Length}, control length={secret.Text?.Length}, source length={provider.ApiKey?.Length}, expected length={key.Length}, focus={masked.IsKeyboardFocusWithin}, binding={secret.GetBindingExpression(SecretInput.TextProperty)?.Status}, source matches={ReferenceEquals(secret.GetBindingExpression(SecretInput.TextProperty)?.DataItem, provider)}.");
        }
        var catalog = Descendants(editor).OfType<OpenAIModelCatalogView>().Single(view => ReferenceEquals(view.DataContext, provider));
        await ReplaceAsync((TextBox)catalog.FindName("ManualModelId"), name);
        var add = (Button)catalog.FindName("AddManualButton");
        add.BringIntoView();
        await WpfInput.ClickAsync(add);
        await WpfInput.WaitAsync(() => provider.Models.Count == 1, "Service model was not added.");
        await WpfInput.WaitAsync(() => Descendants(editor).OfType<TextBox>().Any(input => input.Name == "ProviderInput" && ReferenceEquals(input.DataContext, provider.Models.Single())), "Service model editor was not generated.");
        await ReplaceAsync(Field(editor, provider.Models.Single(), "ProviderInput"), name);
    }

    private static async Task SaveAndCheckPrimaryAsync(SettingsView settings, string root, string? expected)
    {
        await WpfInput.ClickAsync((Button)settings.FindName("SaveButton"));
        await WpfInput.WaitAsync(() =>
        {
            if (((SettingsViewModel)settings.DataContext).IsBusy || ((SettingsViewModel)settings.DataContext).StatusMessage != Lang.Current.SettingsSaved)
                return false;
            string path = Path.Combine(root, "AgentConfiguration.json");
            if (!File.Exists(path)) return false;
            using var current = JsonDocument.Parse(File.ReadAllText(path));
            return current.RootElement.GetProperty("PrimaryModel").GetString() == expected;
        }, "Settings did not persist the expected primary model.");
        using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "AgentConfiguration.json")));
        Assert.AreEqual(expected, saved.RootElement.GetProperty("PrimaryModel").GetString());
    }

    private static void SetContentSize(MainWindow window)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        window.Width += 1440 - content.ActualWidth;
        window.Height += 900 - content.ActualHeight;
        window.UpdateLayout();
    }

    private static async Task ReplaceAsync(TextBox input, string text)
    {
        input.BringIntoView();
        await input.Dispatcher.InvokeAsync(() => Window.GetWindow(input).UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WpfInput.ClickAsync(input);
        await WpfInput.WaitAsync(() => input.IsKeyboardFocusWithin, "Text input did not receive focus.");
        WpfInput.Key(0x24);
        int length = input.Text.Length;
        for (int i = 0; i < length; i++) WpfInput.Key(0x2E);
        WpfInput.Text(text);
        await WpfInput.WaitAsync(() => input.Text == text, "Text input did not replace the field.");
    }

    private static async Task CloseAsync(MainWindow window, MainViewModel model)
    {
        window.Close();
        await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !window.IsVisible, "Settings window did not close.");
        await model.DisposeAsync();
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
