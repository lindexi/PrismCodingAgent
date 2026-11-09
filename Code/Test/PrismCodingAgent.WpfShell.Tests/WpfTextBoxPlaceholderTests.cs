using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell.Infrastructure;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfTextBoxPlaceholderTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(null, "Hint", true)]
    [DataRow("", "Hint", true)]
    [DataRow("value", "Hint", false)]
    [DataRow(" ", "Hint", false)]
    [DataRow("", "", false)]
    [DataRow("", null, false)]
    public async Task SharedTemplateShowsOnlyNonemptyHintForEmptyTextAsync(string? text, string? hint, bool visible)
        => await WpfTestClassAttribute.RunUiAsync(async () =>
        {
            var box = new TextBox { Text = text };
            box.SetValue(TextBoxPlaceholder.TextProperty, hint);
            var window = new Window { Width = 500, Height = 160, Content = box };
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("placeholder-state");
            var placeholder = (TextBlock)box.Template.FindName("Placeholder", box);
            Assert.AreEqual(visible, placeholder.IsVisible);
        });

    [TestMethod]
    public async Task SharedPlaceholderTracksTextAndHintChangesAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var box = new TextBox { Margin = new Thickness(20) };
        var window = new Window { Width = 500, Height = 160, Content = box };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        TextBoxPlaceholder.SetText(box, "Hint");
        await screenshots.CaptureAsync("empty");
        var placeholder = (TextBlock)box.Template.FindName("Placeholder", box);
        Assert.IsTrue(placeholder.IsVisible);
        box.Text = "Real text gyp";
        await screenshots.CaptureAsync("text");
        Assert.IsFalse(placeholder.IsVisible);
        box.Clear();
        await screenshots.CaptureAsync("cleared");
        Assert.IsTrue(placeholder.IsVisible);
        TextBoxPlaceholder.SetText(box, string.Empty);
        await screenshots.CaptureAsync("no-hint");
        Assert.IsFalse(placeholder.IsVisible);
    });

    [TestMethod]
    public async Task PlaceholderAlignsWithTextAndUsesSharedDisabledSurfaceAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var box = new TextBox { Text = "gyp", Margin = new Thickness(20) };
        TextBoxPlaceholder.SetText(box, "Hint");
        var window = new Window { Width = 500, Height = 160, Content = new StackPanel { Children = { box } } };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        await screenshots.CaptureAsync("real-text");
        Rect line = box.GetRectFromCharacterIndex(0);
        box.Clear();
        await screenshots.CaptureAsync("hint");
        var placeholder = (TextBlock)box.Template.FindName("Placeholder", box);
        Point origin = placeholder.TranslatePoint(new Point(placeholder.Padding.Left, placeholder.Padding.Top), box);
        Assert.AreEqual(line.Left, origin.X, 1.0);
        Assert.AreEqual(line.Top, origin.Y, 1.0);
        Assert.IsFalse(placeholder.IsHitTestVisible);
        box.IsEnabled = false;
        await screenshots.CaptureAsync("disabled-hint");
        var surface = (Border)box.Template.FindName("Surface", box);
        Assert.AreEqual(0.45, surface.Opacity);
        Assert.AreEqual(Colors.White, ((SolidColorBrush)surface.Background).Color);
    });

    [TestMethod]
    public async Task ClickingPlaceholderFocusesTextBoxAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var box = new TextBox { Margin = new Thickness(20) };
        TextBoxPlaceholder.SetText(box, "Click to enter text");
        var window = new Window { Width = 500, Height = 180, Content = new StackPanel { Children = { new Button { Content = "Other focus" }, box } } };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        await screenshots.CaptureAsync("before-click");
        var placeholder = (TextBlock)box.Template.FindName("Placeholder", box);
        Assert.IsTrue(placeholder.IsVisible);
        await WpfInput.ClickAsync(box);
        await WpfInput.WaitAsync(() => box.IsKeyboardFocused, "Clicking the placeholder must focus its TextBox.");
        await screenshots.CaptureAsync("focused");
        Assert.IsTrue(box.IsKeyboardFocused);
    });
}
