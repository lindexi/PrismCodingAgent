using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Converters;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfBasicControlsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BinaryImageRendersAttachmentAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var image = new BinaryImage { Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Data = CreateImageData() };
        var window = new Window { Width = 160, Height = 120, Content = image, Background = Brushes.White };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        await screenshots.CaptureAsync("binary-image");
        var rendered = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(image);
        byte[] pixel = new byte[4];
        rendered.CopyPixels(new Int32Rect(24, 24, 1, 1), pixel, 4, 0);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, pixel);
    });

    [TestMethod]
    public async Task BinaryImageClearsInvalidAndMissingDataAsync() => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var image = new BinaryImage { Data = CreateImageData() };
        image.Data = BinaryData.FromString("not an image");
        Assert.IsNull(image.Source);
        image.Data = CreateImageData();
        image.Data = null;
        Assert.IsNull(image.Source);
        return Task.CompletedTask;
    });

    [TestMethod]
    public async Task SecretInputMasksRevealsAndUpdatesBindingAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var source = new TextBox { Text = "sample-key" };
        var input = new SecretInput { Width = 300, Height = 32 };
        input.SetBinding(SecretInput.TextProperty, new Binding(nameof(TextBox.Text)) { Source = source, Mode = BindingMode.TwoWay });
        var window = new Window { Width = 420, Height = 160, Content = new Grid { Background = Brushes.White, Children = { input } } };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var masked = (PasswordBox)input.FindName("MaskedInput");
        var revealed = (TextBox)input.FindName("RevealedInput");
        var toggle = (ToggleButton)input.FindName("RevealButton");
        await screenshots.CaptureAsync("secret-masked");
        Assert.IsTrue(masked.IsVisible);
        Assert.AreEqual('●', masked.PasswordChar);
        Assert.AreEqual(string.Empty, revealed.Text);
        await WpfInput.ClickAsync(masked);
        WpfInput.Text("x");
        await WpfInput.WaitAsync(() => source.Text.Contains('x'), "Masked input did not update binding.");
        await WpfInput.ClickAsync(toggle);
        await WpfInput.WaitAsync(() => revealed.IsVisible, "Secret did not reveal.");
        Assert.AreEqual(source.Text, revealed.Text);
        revealed.Text = "changed-key";
        Assert.AreEqual("changed-key", source.Text);
        await screenshots.CaptureAsync("secret-revealed");
        await WpfInput.ClickAsync(toggle);
        await WpfInput.WaitAsync(() => masked.IsVisible, "Secret did not mask again.");
        Assert.AreEqual(string.Empty, revealed.Text);
        Assert.AreEqual("changed-key", masked.Password);
    });

    [TestMethod]
    public async Task ConvertersPreserveReferenceSemanticsAsync() => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var inverse = (InverseBoolConverter)Application.Current.FindResource("InverseBoolConverter");
        Assert.AreEqual(false, inverse.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.AreEqual(true, inverse.ConvertBack(false, typeof(bool), null, CultureInfo.InvariantCulture));
        var title = (WorkspaceTitleConverter)Application.Current.FindResource("WorkspaceTitleConverter");
        Assert.AreEqual("App - Task - C:\\Work - Session", title.Convert(["C:\\Work", "Session", "Task", "App"], typeof(string), "", CultureInfo.InvariantCulture));
        Assert.AreEqual("App", title.Convert([null!, " ", "", "App"], typeof(string), "", CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    });

    [TestMethod]
    public async Task TemplateSelectorsChooseMessageSemanticsAsync() => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var text = new DataTemplate();
        var image = new DataTemplate();
        var selector = new ChatMessageItemTemplateSelector { TextItemTemplate = text, ImageItemTemplate = image };
        Assert.AreSame(text, selector.SelectTemplate(new CopilotChatTextItem("text"), new ContentControl()));
        Assert.AreSame(image, selector.SelectTemplate(new CopilotChatImageItem(CreateImageData(), "image/png"), new ContentControl()));
        Assert.IsNull(selector.SelectTemplate(new object(), new ContentControl()));
        var user = new DataTemplate();
        var assistant = new DataTemplate();
        var system = new DataTemplate();
        var entries = new ChatEntryTemplateSelector { UserMessageTemplate = user, AssistantMessageTemplate = assistant, SystemMessageTemplate = system };
        using var userMessage = new MessageItemViewModel(new CopilotChatMessage(ChatRole.User, "hello"));
        using var assistantMessage = new MessageItemViewModel(new CopilotChatMessage(ChatRole.Assistant, "hello"));
        using var systemMessage = new MessageItemViewModel(new CopilotChatMessage(ChatRole.System, "hello"));
        Assert.AreSame(user, entries.SelectTemplate(userMessage, new ContentControl()));
        Assert.AreSame(assistant, entries.SelectTemplate(assistantMessage, new ContentControl()));
        Assert.AreSame(system, entries.SelectTemplate(systemMessage, new ContentControl()));
        return Task.CompletedTask;
    });

    private static BinaryData CreateImageData()
    {
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255 }, 8);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return BinaryData.FromBytes(stream.ToArray());
    }
}
