using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.ViewModels;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;
using SettingsView = PrismCodingAgent.CodingShell.Views.SettingsView;
using ModelSettingsView = PrismCodingAgent.CodingShell.Views.ModelSettingsView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class PrimaryModelLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> LayoutCases() =>
        from language in new[] { "zh-Hans", "zh-Hant", "en", "de", "fr", "es", "it", "ja", "ko", "nl", "pt-BR", "ru" }
        from width in new[] { 1120, 720 }
        select new object[] { width, language };

    [TestMethod]
    [DynamicData(nameof(LayoutCases))]
    public async Task MultipleServicesShouldHaveSeparateBoundsAndPreserveBindings(int width, string language)
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(() =>
        {
            string originalLanguage = Lang.Current.IetfLanguageTag;
            Lang.SetCurrent(language);
            using var model = new SettingsViewModel { PrimaryModel = "atlas/code" };
            model.Providers.Clear();
            foreach (string name in new[] { "atlas", "swift", "nova" })
            {
                var provider = new ProviderSettingsViewModel
                {
                    EndPoint = $"https://{name}.example.invalid/v1",
                    ApiKey = $"sk-fictional-{name}-test-only",
                };
                provider.Models.Add(new ModelSettingsViewModel
                {
                    Provider = name, ModelName = $"{name}-code", ModelId = $"{name}-code-v2",
                    ContextWindowSize = 128000, MaxOutputTokens = 8192, SupportsToolCall = true,
                });
                provider.Models.Add(new ModelSettingsViewModel
                {
                    Provider = name, ModelName = $"{name}-chat", ModelId = $"{name}-chat-v1",
                    ContextWindowSize = 64000, MaxOutputTokens = 4096, IsFlash = true,
                });
                model.Providers.Add(provider);
            }
            var view = new SettingsView { DataContext = model };
            var window = new Window { Content = view, Width = width, Height = 1000 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                var editor = view.GetVisualDescendants().OfType<ModelSettingsView>().Single();
                var modelCard = editor.GetVisualAncestors().OfType<Border>().First();
                var quotaCard = view.FindControl<Border>("CpaQuotaSettings")!;
                Assert.IsTrue(modelCard.Classes.Contains("SettingsCard"));
                Assert.AreSame(quotaCard.Parent, modelCard.Parent);
                Assert.AreEqual(quotaCard.Background, modelCard.Background);
                Assert.AreEqual(quotaCard.BorderBrush, modelCard.BorderBrush);
                Assert.AreEqual(quotaCard.BorderThickness, modelCard.BorderThickness);
                Assert.AreEqual(quotaCard.CornerRadius, modelCard.CornerRadius);
                Assert.AreEqual(quotaCard.Padding, modelCard.Padding);
                Assert.AreEqual(quotaCard.Margin, modelCard.Margin);
                Assert.AreEqual(quotaCard.BoxShadow, modelCard.BoxShadow);
                Assert.IsTrue(view.FindControl<TextBox>("PrimaryModelInput")!.GetVisualAncestors().Contains(modelCard));
                Assert.IsTrue(modelCard.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == Lang.Current.ModelConfigurationTitle));
                var addService = editor.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("AddOutline"));
                Assert.AreSame(model.AddProviderCommand, addService.Command);
                Assert.IsTrue(addService.GetVisualAncestors().Contains(modelCard));
                var cards = editor.GetVisualDescendants().OfType<Border>()
                    .Where(border => border.Classes.Contains("ProviderCard")).ToArray();
                Assert.HasCount(3, cards);
                Rect[] bounds = cards.Select(card => new Rect(card.TranslatePoint(default, editor)!.Value, card.Bounds.Size)).ToArray();
                Assert.IsGreaterThanOrEqualTo(16, bounds[1].Top - bounds[0].Bottom);
                Assert.IsGreaterThanOrEqualTo(16, bounds[2].Top - bounds[1].Bottom);
                for (int index = 0; index < cards.Length; index++)
                {
                    var card = cards[index];
                    var provider = model.Providers[index];
                    Assert.AreSame(provider, card.DataContext);
                    Assert.AreEqual(new Thickness(1), card.BorderThickness);
                    Assert.IsTrue(card.BorderBrush is Avalonia.Media.ISolidColorBrush brush && brush.Color.A > 0);
                    Assert.AreEqual(new CornerRadius(12), card.CornerRadius);
                    var connection = card.GetVisualDescendants().OfType<PrismCodingAgent.CodingShell.Views.ModelServiceConnectionView>().Single();
                    var endpoint = connection.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == provider.EndPoint);
                    var key = connection.GetVisualDescendants().OfType<PrismCodingAgent.CodingShell.Views.SecretInput>().Single();
                    var models = card.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("ModelCard")).ToArray();
                    Assert.HasCount(2, models);
                    Assert.AreSame(provider.Models[0], models[0].DataContext);
                    Assert.AreSame(provider.Models[1], models[1].DataContext);
                    Assert.AreEqual(provider.ApiKey, key.Text);
                    foreach (Control field in new Control[] { connection, endpoint, key, models[0], models[1] })
                    {
                        Rect fieldBounds = new(field.TranslatePoint(default, card)!.Value, field.Bounds.Size);
                        Assert.IsTrue(fieldBounds.Width > 0 && fieldBounds.Height > 0 && new Rect(card.Bounds.Size).Contains(fieldBounds));
                    }
                    var remove = card.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("QuietDanger"));
                    Assert.AreSame(model.RemoveProviderCommand, remove.Command);
                    Assert.AreSame(provider, remove.CommandParameter);
                    double headerBottom = remove.TranslatePoint(default, card)!.Value.Y + remove.Bounds.Height;
                    double connectionTop = connection.TranslatePoint(default, card)!.Value.Y;
                    Assert.IsTrue(connectionTop - headerBottom >= 0 && connectionTop - headerBottom <= 12);
                    endpoint.Text = $"https://edited-{index}.example.invalid/v1";
                    Assert.AreEqual(endpoint.Text, provider.EndPoint);
                    provider.EndPoint = $"https://restored-{index}.example.invalid/v1";
                    Assert.AreEqual(provider.EndPoint, endpoint.Text);
                    key.Text = $"sk-fictional-edited-{index}";
                    Assert.AreEqual(key.Text, provider.ApiKey);
                    provider.ApiKey = $"sk-fictional-restored-{index}";
                    Assert.AreEqual(provider.ApiKey, key.Text);
                    scroll.Offset = new Vector(0, card.TranslatePoint(default, scroll)!.Value.Y + scroll.Offset.Y - 8);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Rect visibleCard = new(card.TranslatePoint(default, scroll)!.Value, card.Bounds.Size);
                    Assert.IsTrue(visibleCard.Top >= 0 && visibleCard.Bottom <= scroll.Bounds.Height);
                    string directory = Path.Combine(AppContext.BaseDirectory, "PrimaryModelScreenshots");
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, $"{language}-{width}-three-services-{index + 1}.png");
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
                    frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    TestContext.WriteLine(path);
                    TestContext.AddResultFile(path);
                }
                window.Height = modelCard.Bounds.Height + 400;
                window.UpdateLayout();
                scroll.Offset = new Vector(0, modelCard.TranslatePoint(default, scroll)!.Value.Y + scroll.Offset.Y - 8);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Rect outerBounds = new(modelCard.TranslatePoint(default, scroll)!.Value, modelCard.Bounds.Size);
                var purposes = view.FindControl<Expander>("PurposeModelsExpander")!;
                Assert.IsFalse(purposes.IsExpanded);
                Assert.IsTrue(purposes.GetVisualAncestors().Contains(modelCard));
                Assert.IsTrue(outerBounds.Top >= 0 && outerBounds.Bottom < scroll.Bounds.Height);
                string overviewPath = Path.Combine(AppContext.BaseDirectory, "PrimaryModelScreenshots", $"{language}-{width}-settings-cards-overview.png");
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var overview = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
                overview.Save(overviewPath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                TestContext.WriteLine(overviewPath);
                TestContext.AddResultFile(overviewPath);
            }
            finally
            {
                window.Close();
                Lang.SetCurrent(originalLanguage);
            }
            return Task.CompletedTask;
        }, CancellationToken.None).ContinueWith(static completed => completed.GetAwaiter().GetResult(),
            CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow(1120, "example/atlas-code")]
    [DataRow(1120, "")]
    [DataRow(720, "example/atlas-code")]
    [DataRow(720, "")]
    public async Task ChineseModelSettingsShouldKeepLabelAndEditableInputVisible(int width, string primaryModel)
    {
        await AvaloniaTestClassAttribute.Session.Dispatch(async () =>
        {
            string originalLanguage = Lang.Current.IetfLanguageTag;
            Lang.SetCurrent("zh-Hans");
            using var model = new SettingsViewModel { PrimaryModel = primaryModel };
            var provider = model.Providers.Single();
            provider.EndPoint = "https://models.example.invalid/v1";
            provider.ApiKey = "sk-fictional-ui-test-only";
            provider.Models.Add(new ModelSettingsViewModel
            {
                Provider = "example", ModelName = "atlas-code", ModelId = "atlas-code-v2",
                ContextWindowSize = 128000, MaxOutputTokens = 8192,
                SupportsReasoning = true, SupportsToolCall = true, SupportsImageInput = true,
            });
            provider.Models.Add(new ModelSettingsViewModel
            {
                Provider = "example", ModelName = "swift-chat", ModelId = "swift-chat-v1",
                ContextWindowSize = 64000, MaxOutputTokens = 4096,
                SupportsToolCall = true, IsFlash = true,
            });
            var view = new SettingsView { DataContext = model };
            var window = new Window { Content = view, Width = width, Height = 1000 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var input = view.FindControl<TextBox>("PrimaryModelInput")!;
                var label = view.FindControl<TextBlock>("PrimaryModelLabel")!;
                var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                var editor = view.GetVisualDescendants().OfType<ModelSettingsView>().Single();
                var card = input.GetVisualAncestors().OfType<Border>().First();
                Point cardPosition = card.TranslatePoint(default, scroll)!.Value;
                scroll.Offset = new Vector(0, cardPosition.Y + scroll.Offset.Y);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                string directory = Path.Combine(AppContext.BaseDirectory, "PrimaryModelScreenshots");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"zh-Hans-{width}-{(primaryModel.Length == 0 ? "empty" : "filled")}.png");
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
                frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                TestContext.WriteLine(path);
                TestContext.AddResultFile(path);
                Assert.AreEqual(Lang.Current.DefaultModelLabel, label.Text);
                var hint = view.FindControl<TextBlock>("PrimaryModelHint")!;
                Assert.AreEqual(Lang.Current.DefaultModelHint, hint.Text);
                Lang.SetCurrent("de");
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual(Lang.Current.DefaultModelLabel, label.Text);
                Assert.AreEqual(Lang.Current.DefaultModelHint, hint.Text);
                Assert.AreEqual(primaryModel, input.Text);
                Point labelPosition = label.TranslatePoint(default, scroll)!.Value;
                Point inputPosition = input.TranslatePoint(default, scroll)!.Value;
                Assert.IsTrue(labelPosition.Y >= 0 && inputPosition.X > labelPosition.X + label.Bounds.Width);
                var connection = editor.GetVisualDescendants().OfType<PrismCodingAgent.CodingShell.Views.ModelServiceConnectionView>().Single();
                Point connectionPosition = connection.TranslatePoint(default, scroll)!.Value;
                Assert.AreEqual(connectionPosition.X + connection.Bounds.Width + 17, inputPosition.X + input.Bounds.Width, 1);
                Assert.AreEqual(2, editor.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("ModelCard")));
                input.Text = "example/swift-chat";
                Assert.AreEqual("example/swift-chat", model.PrimaryModel);
                model.PrimaryModel = "example/atlas-code";
                Assert.AreEqual("example/atlas-code", input.Text);
                await Task.CompletedTask;
            }
            finally
            {
                window.Close();
                Lang.SetCurrent(originalLanguage);
            }
        }, CancellationToken.None).ContinueWith(static completed => completed.GetAwaiter().GetResult(),
            CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
    }
}
