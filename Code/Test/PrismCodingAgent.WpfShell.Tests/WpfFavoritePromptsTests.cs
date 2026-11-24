using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfFavoritePromptsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WheelScrollsWithinAnExpandedPromptAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        TestContext.WriteLine($"Test data: {root}");
        string prompt = string.Join("\r\n", Enumerable.Range(1, 200).Select(line => $"Prompt line {line}"));
        await File.WriteAllTextAsync(Path.Combine(root, "Prompts.json"), JsonSerializer.Serialize(new[] { prompt }), WpfTestSession.Cancellation);
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var composer = (ChatView)window.FindName("ChatComposer");
        var open = (Button)composer.FindName("OpenFavoritePromptsButton");
        var panel = (FavoritePromptsView)window.FindName("FavoritePromptsPanel");
        var list = (ListBox)panel.FindName("PromptList");
        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, open));
        await WpfInput.WaitAsync(() => panel.IsVisible && list.Items.Count == 1, "Favorite prompt did not load.");
        window.UpdateLayout();
        var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        var expand = FindVisualChild<Button>(row) ?? throw new InvalidOperationException("Missing expand button.");
        expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, expand));
        window.UpdateLayout();
        var scroll = FindVisualChild<ScrollViewer>(list) ?? throw new InvalidOperationException("Missing list scroll viewer.");
        scroll.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, -120)
        {
            RoutedEvent = System.Windows.Input.Mouse.MouseWheelEvent,
        });
        window.UpdateLayout();

        Assert.IsTrue(scroll.VerticalOffset > 0 && scroll.VerticalOffset < scroll.ScrollableHeight,
            $"One wheel tick should stop inside the prompt. Offset: {scroll.VerticalOffset}, maximum: {scroll.ScrollableHeight}.");
    });

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if ((child as T ?? FindVisualChild<T>(child)) is { } result) return result;
        }

        return null;
    }

    [TestMethod]
    public async Task ApplyingSelectedFavoriteAppendsFullPromptAndClosesPanelAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "FavoritePromptsData", Guid.NewGuid().ToString("N"))).FullName;
        TestContext.WriteLine($"Test data: {root}");
        var cancellationToken = WpfTestSession.Cancellation;
        string prompt = new string('x', 125) + "\r\nKeep this last line.";
        await File.WriteAllTextAsync(Path.Combine(root, "Prompts.json"), JsonSerializer.Serialize(new[] { "Other prompt", prompt }), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"Offline model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Offline model","ModelId":"offline","Provider":"openai"}]}]}""", cancellationToken);

        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = ((MainViewModel)window.DataContext).ChatViewModel;
        var composer = (ChatView)window.FindName("ChatComposer");
        var input = (TextBox)composer.FindName("MessageInputTextBox");
        var open = (Button)composer.FindName("OpenFavoritePromptsButton");
        var panel = (FavoritePromptsView)window.FindName("FavoritePromptsPanel");
        var list = (ListBox)panel.FindName("PromptList");
        var apply = (Button)panel.FindName("ApplyPromptButton");
        const string draft = "Existing draft: ";
        model.InputText = draft;
        input.CaretIndex = 3;

        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, open));
        await WpfInput.WaitAsync(() => panel.IsVisible && list.Items.Count == 2, "Favorite prompts did not load.");
        Assert.IsFalse(apply.IsEnabled);
        list.SelectedIndex = 1;
        Assert.IsTrue(apply.IsEnabled);
        apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));

        Assert.AreEqual(draft + prompt, model.InputText);
        Assert.AreEqual(Visibility.Collapsed, panel.Visibility);
    });
}
