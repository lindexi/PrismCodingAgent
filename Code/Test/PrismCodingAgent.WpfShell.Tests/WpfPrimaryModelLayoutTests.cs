using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfPrimaryModelLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1120)]
    [DataRow(720)]
    public async Task ModelServicesKeepGroupingAndLocalizedLabelsAsync(int width) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string original = Lang.Current.IetfLanguageTag;
        string language = original;
        using var model = new SettingsViewModel { PrimaryModel = "atlas/code" };
        model.Providers.Clear();
        foreach (string name in new[] { "atlas", "swift", "nova" })
        {
            var provider = new ProviderSettingsViewModel { EndPoint = $"https://{name}.example.invalid/v1", ApiKey = $"sk-fictional-{name}-test-only" };
            provider.Models.Add(new ModelSettingsViewModel { Provider = name, ModelName = name + "-code", ModelId = name + "-code-v2", ContextWindowSize = 128000, MaxOutputTokens = 8192 });
            provider.Models.Add(new ModelSettingsViewModel { Provider = name, ModelName = name + "-chat", ModelId = name + "-chat-v1", ContextWindowSize = 64000, MaxOutputTokens = 4096 });
            model.Providers.Add(provider);
        }
        var view = new SettingsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = 1000 };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var input = (TextBox)view.FindName("PrimaryModelInput");
            var label = (TextBlock)view.FindName("PrimaryModelLabel");
            var hint = (TextBlock)view.FindName("PrimaryModelHint");
            Assert.AreEqual(Lang.Current.DefaultModelLabel, label.Text);
            Assert.AreEqual(Lang.Current.DefaultModelHint, hint.Text);
            Assert.AreSame(label, AutomationProperties.GetLabeledBy(input));
            input.Text = "nova/chat";
            Assert.AreEqual("nova/chat", model.PrimaryModel);
            model.PrimaryModel = "atlas/code";
            Assert.AreEqual(model.PrimaryModel, input.Text);
            var editor = Descendants(view).OfType<ModelSettingsView>().Single();
            var outer = Ancestors(editor).OfType<Border>().First(border => border.Style == view.FindResource("SettingsCard"));
            var adjacent = (Border)view.FindName("CpaQuotaSettings");
            Assert.AreSame(outer.Style, adjacent.Style);
            Assert.AreSame(outer.Parent, adjacent.Parent);
            var cards = Descendants(editor).OfType<Border>().Where(b => b.CornerRadius == new CornerRadius(12) && model.Providers.Contains(b.DataContext as ProviderSettingsViewModel)).ToArray();
            Assert.HasCount(3, cards);
            var scroll = Descendants(view).OfType<ScrollViewer>().First();
            for (int index = 0; index < cards.Length; index++)
            {
                var card = cards[index];
                var provider = model.Providers[index];
                Assert.AreSame(provider, card.DataContext);
                Assert.AreEqual(new Thickness(1), card.BorderThickness);
                Assert.HasCount(2, Descendants(card).OfType<TextBox>().Where(b => b.Name == "ModelIdInput").ToArray());
                Assert.AreEqual(provider.ApiKey, Descendants(card).OfType<SecretInput>().Single().Text);
                var endpoint = Descendants(card).OfType<TextBox>().Single(b => b.Name == "EndpointInput");
                Assert.AreEqual(provider.EndPoint, endpoint.Text);
                if (index > 0)
                    Assert.IsTrue(card.TranslatePoint(default, editor).Y - cards[index - 1].TranslatePoint(new Point(0, cards[index - 1].ActualHeight), editor).Y >= 16);
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + card.TranslatePoint(default, scroll).Y - 8);
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                await screenshots.CaptureAsync($"{language}-{width}-service-{index + 1}");
            }
            window.Height = outer.ActualHeight + 400;
            window.UpdateLayout();
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + outer.TranslatePoint(default, scroll).Y - 8);
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            await screenshots.CaptureAsync($"{language}-{width}-overview");
            Lang.SetCurrent(language == "de" ? "en" : "de");
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(Lang.Current.DefaultModelLabel, label.Text);
            Assert.AreEqual(Lang.Current.DefaultModelHint, hint.Text);
        }
        finally { window.Close(); Lang.SetCurrent(original); }
    });

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject item)
    {
        while ((item = VisualTreeHelper.GetParent(item)) is not null) yield return item;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
