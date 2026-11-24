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
