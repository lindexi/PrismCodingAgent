using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell.Views;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfProxyTextLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProxyToggleKeepsTextLineVisibleLikeSandboxAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var view = new SettingsView();
        var window = new Window { Width = 1100, Height = 900, Content = view };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var proxy = (TextBox)view.FindName("ProxyAddress");
        var sandbox = (TextBox)view.FindName("SandboxAddress");
        var failures = new List<string>();
        ControlTemplate? template = null;
        foreach (bool enabled in new[] { false, true, false })
        {
            view.DataContext = new LayoutSettings
            {
                IsNetworkProxyEnabled = enabled,
                IsWindowsSandboxEnabled = enabled,
                NetworkProxyAddress = "http://127.0.0.1:10112",
                WindowsSandboxServerAddress = "127.0.0.1:12398",
                WindowsSandboxToolPath = @"C:\Sandbox\WinRemoteShell.exe"
            };
            proxy.BringIntoView();
            await screenshots.CaptureAsync(enabled ? "proxy-on" : "proxy-off");
            template ??= proxy.Template;
            Assert.AreSame(template, proxy.Template, "Disabling must not replace the template.");
            Assert.AreSame(sandbox.Template, proxy.Template, "Settings inputs must reuse the sandbox template.");
            var sharedStyle = (Style)Application.Current.FindResource(typeof(TextBox));
            var sharedTemplate = (ControlTemplate)sharedStyle.Setters.OfType<Setter>()
                .Single(setter => setter.Property == Control.TemplateProperty).Value;
            Assert.AreSame(sharedTemplate, proxy.Template, "Settings must use the actual application template.");
            var proxySurface = (Border)proxy.Template.FindName("Surface", proxy);
            var sandboxSurface = (Border)sandbox.Template.FindName("Surface", sandbox);
            Assert.AreEqual(sandboxSurface.Opacity, proxySurface.Opacity);
            Assert.AreEqual(sandboxSurface.Background.ToString(), proxySurface.Background.ToString());
            foreach (var box in new[] { proxy, sandbox, (TextBox)view.FindName("SandboxPath") })
            {
                var host = (ScrollViewer)box.Template.FindName("PART_ContentHost", box);
                var presenter = Descendants(host).OfType<ScrollContentPresenter>().Single();
                Rect viewport = presenter.TransformToAncestor(box).TransformBounds(new Rect(presenter.RenderSize));
                Rect line = box.GetRectFromCharacterIndex(0);
                TestContext.WriteLine($"{box.Name} enabled={box.IsEnabled} Height={box.ActualHeight} Padding={box.Padding} HostPadding={host.Padding} HostMargin={host.Margin} Viewport={viewport} Line={line}");
                if (line.IsEmpty || line.Top < viewport.Top - 0.5 || line.Bottom > viewport.Bottom + 0.5)
                    failures.Add($"{box.Name} enabled={box.IsEnabled}: line {line} exceeds viewport {viewport}");
            }
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    });

    [TestMethod]
    public async Task SharedMultilineAndSecretInputsKeepRenderedContentVisibleAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var multiline = new TextBox
        {
            Height = 100, AcceptsReturn = true, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            Text = "First line with descenders gyp\nSecond line\nLast line with ascenders htf"
        };
        var secret = new SecretInput { Height = 38, Text = "sample-key" };
        var panel = new StackPanel { Margin = new Thickness(20), Children = { multiline, secret } };
        var window = new Window { Width = 600, Height = 260, Content = panel };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        await screenshots.CaptureAsync("multiline-and-masked");
        var host = (ScrollViewer)multiline.Template.FindName("PART_ContentHost", multiline);
        Assert.IsTrue(host.ViewportHeight >= host.ExtentHeight, "All three text lines must fit without vertical clipping.");
        var masked = (PasswordBox)secret.FindName("MaskedInput");
        var maskedHost = (FrameworkElement)masked.Template.FindName("PART_ContentHost", masked);
        Assert.IsTrue(maskedHost.ActualHeight >= masked.FontFamily.LineSpacing * masked.FontSize);
        ((System.Windows.Controls.Primitives.ToggleButton)secret.FindName("RevealButton")).IsChecked = true;
        await screenshots.CaptureAsync("multiline-and-revealed");
        var revealed = (TextBox)secret.FindName("RevealedInput");
        var revealedHost = (ScrollViewer)revealed.Template.FindName("PART_ContentHost", revealed);
        var presenter = Descendants(revealedHost).OfType<ScrollContentPresenter>().Single();
        Rect viewport = presenter.TransformToAncestor(revealed).TransformBounds(new Rect(presenter.RenderSize));
        Rect line = revealed.GetRectFromCharacterIndex(0);
        Assert.IsTrue(!line.IsEmpty && line.Top >= viewport.Top - 0.5 && line.Bottom <= viewport.Bottom + 0.5,
            $"Revealed line {line} must fit viewport {viewport}.");
    });

    [TestMethod]
    public async Task SettingsPlaceholdersKeepLocalizedBindingsAndSharedDisabledAppearanceAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var view = new SettingsView();
        var window = new Window { Width = 1100, Height = 900, Content = view };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var proxy = (TextBox)view.FindName("ProxyAddress");
        var instructions = (TextBox)view.FindName("InstructionsPath");
        foreach (bool enabled in new[] { false, true })
        {
            view.DataContext = new LayoutSettings { IsNetworkProxyEnabled = enabled, IsWindowsSandboxEnabled = enabled };
            proxy.BringIntoView();
            await screenshots.CaptureAsync(enabled ? "placeholder-on" : "placeholder-off");
            var placeholder = (TextBlock)proxy.Template.FindName("Placeholder", proxy);
            Assert.AreEqual(Lang.Current.NetworkProxyAddressPlaceholder, placeholder.Text);
            Assert.IsTrue(placeholder.IsVisible);
            Assert.AreEqual(Lang.Current.InstructionsPlaceholder, TextBoxPlaceholder.GetText(instructions));
            Assert.AreEqual(enabled, proxy.IsEnabled);
        }
    });

    private sealed class LayoutSettings
    {
        public bool IsNetworkProxyEnabled { get; set; }
        public bool IsWindowsSandboxEnabled { get; set; }
        public string NetworkProxyAddress { get; set; } = string.Empty;
        public string WindowsSandboxServerAddress { get; set; } = string.Empty;
        public string WindowsSandboxToolPath { get; set; } = string.Empty;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
