using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ChatOverlayInteractionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FixedLayerPreservesInputNavigationAndMessageUpdatesAsync()
    {
        const string configuration = """{"PrimaryModel":"Offline","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline","ModelDefinitions":[{"ModelName":"Offline","ModelId":"offline","Provider":"openai"}]}]}""";
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext, configuration);
        var window = session.Window;
        var chat = window.GetVisualDescendants().OfType<ChatView>().Single();
        var layer = chat.FindControl<Grid>("ChatOverlayLayer")!;
        var panel = chat.FindControl<ChatSettingsView>("ChatSettingsPanel")!;
        var input = chat.FindControl<TextBox>("MessageInputTextBox")!;
        session.Capture("empty-layer");
        Assert.IsNull(layer.Background);
        Assert.IsTrue(layer.IsVisible);
        Assert.IsFalse(panel.IsVisible);
        var point = input.TranslatePoint(new Point(input.Bounds.Width / 2, input.Bounds.Height / 2), window)!.Value;
        Assert.IsTrue(IsWithin(window.InputHitTest(point) as Visual, input));
        session.Click(input);
        window.KeyTextInput("overlay draft");
        Assert.AreEqual("overlay draft", input.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.AreEqual("overlay draft".Length - 1, input.CaretIndex);
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        Assert.AreEqual("overlay draft", input.SelectedText);
        for (int i = 0; i < 40; i++)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Assert.IsFalse(IsWithin(window.FocusManager!.GetFocusedElement() as Visual, panel));
        }
        Assert.IsFalse(panel.FindControl<ToggleSwitch>("ChatAutomaticCompressionToggle")!.Focus());
        session.Click(chat.FindControl<Button>("OpenChatSettingsButton")!);
        Assert.IsTrue(panel.IsVisible);
        Assert.IsTrue(layer.IsVisible);
        Assert.IsTrue(panel.Background is ISolidColorBrush brush && brush.Color.A == 255 && brush.Opacity == 1);
        Assert.IsTrue(IsWithin(window.InputHitTest(point) as Visual, panel));
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.KeyTextInput("blocked");
        Assert.AreEqual("overlay draft", input.Text);
        int previousCount = session.Model.ChatViewModel.Messages.Count;
        session.Model.ChatViewModel.StopLanguageServerCommand.Execute(null);
        for (int i = 0; i < 100 && session.Model.ChatViewModel.Messages.Count == previousCount; i++)
            await Task.Delay(20);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var history = chat.FindControl<ItemsControl>("MessagesItemsControl")!;
        Assert.IsGreaterThan(previousCount, history.Items.Count);
        Assert.IsNotNull(history.ContainerFromIndex(history.Items.Count - 1));
        session.Click(panel.FindControl<Button>("CloseChatSettingsButton")!);
        session.Click(input);
        window.KeyTextInput("resumed");
        Assert.IsTrue(input.IsFocused);
        Assert.Contains("resumed", input.Text!);
        Assert.IsTrue(IsWithin(window.InputHitTest(point) as Visual, input));
        session.Capture("input-restored");
    }

    private static bool IsWithin(Visual? target, Visual ancestor)
        => target == ancestor || target?.GetVisualAncestors().Contains(ancestor) == true;
}
