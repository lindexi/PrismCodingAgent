using AgentLib.Model;
using Avalonia;
using Avalonia.Markup.Xaml.Templates;
using PrismCodingAgent.CodingShell.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using ChatView = PrismCodingAgent.CodingShell.Views.ChatView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ChatOutputVisualTests
{
    [AssemblyCleanup]
    public static Task CleanupAvalonia() => AvaloniaTestClassAttribute.Session.DisposeAsync().AsTask();

    [TestMethod]
    public void ChatViewShouldRegisterOriginalMessageItemSelector()
    {
        var view = new ChatView();

        Assert.IsInstanceOfType<ChatMessageItemTemplateSelector>(view.DataTemplates.Single());
    }

    [TestMethod]
    [DataRow("ReadFile", true)]
    [DataRow("readfile", false)]
    [DataRow("OtherTool", false)]
    public void ToolTemplateShouldRequireAnExactToolName(string toolName, bool useCustomTemplate)
    {
        var fallback = new DataTemplate();
        var custom = new DataTemplate();
        var selector = new ChatMessageItemTemplateSelector { ToolItemTemplate = fallback };
        selector.ToolItemTemplates.Add("ReadFile", custom);

        var selected = selector.SelectTemplate(new CopilotChatToolItem("call", toolName, null));

        Assert.AreSame(useCustomTemplate ? custom : fallback, selected);
    }

    [TestMethod]
    public void ToolTemplateShouldNotReplaceApprovalTemplate()
    {
        var approval = new DataTemplate();
        var selector = new ChatMessageItemTemplateSelector { ApprovalToolItemTemplate = approval };
        selector.ToolItemTemplates.Add("ReadFile", new DataTemplate());

        var selected = selector.SelectTemplate(new CopilotChatApprovalToolItem("call", "ReadFile", null));

        Assert.AreSame(approval, selected);
    }

    [TestMethod]
    [DataRow("WriteFileContent")]
    [DataRow("ReplaceStringInFile")]
    [DataRow("MultiReplaceStringInFile")]
    public void FileToolTemplateIsRegisteredByName(string toolName)
    {
        var selector = (ChatMessageItemTemplateSelector)new ChatView().DataTemplates.Single();

        var selected = selector.SelectTemplate(new CopilotChatToolItem("call", toolName, null));

        Assert.AreSame(selector.ToolItemTemplates[toolName], selected);
    }

    [TestMethod]
    [DataRow("WriteFileContent", "{\"filePath\":\"a.cs\",\"content\":\"first line\\n\\n  second line\"}", 0, "a.cs|first line\n\n  second line")]
    [DataRow("ReplaceStringInFile", "{\"filePath\":\"a.cs\",\"oldString\":\"context\\nold\",\"newString\":\"context\\nnew\"}", 0, "a.cs|−|old|+|new")]
    [DataRow("MultiReplaceStringInFile", "{\"explanation\":\"Batch note\",\"replacements\":[{\"FilePath\":\"a.cs\",\"OldString\":\"old\",\"NewString\":\"new\",\"Explanation\":\"Edit note\"},{\"FilePath\":\"b.cs\",\"OldString\":\"before\",\"NewString\":\"\"}]}", 1, "Batch note|a.cs|Edit note|−|old|+|new|b.cs|−|before")]
    public void ExpandedFileToolRendersDedicatedLayout(string toolName, string input, int listCount, string expected)
    {
        InspectFileTool(toolName, input, (window, expander) =>
        {
            var controls = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(expander).ToArray();
            var text = controls.OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text)
                .Where(value => !string.IsNullOrEmpty(value)).ToArray();
            var expectedParts = expected.Split('|');
            var previewParts = expectedParts.Concat(["−", "+", "context"]).ToHashSet();
            CollectionAssert.AreEqual(expectedParts, text.Where(value => value is not null && previewParts.Contains(value)).ToArray());
            Assert.AreEqual(listCount, controls.OfType<ItemsControl>().Count());

            using var bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("无法捕获文件工具预览截图。");
            string path = GetScreenshotPath($"{toolName}-{Guid.NewGuid():N}.png");
            bitmap.Save(path, PngBitmapEncoderOptions.Default);
            Console.WriteLine(path);
        });
    }

    [TestMethod]
    [DataRow("WriteFileContent")]
    [DataRow("ReplaceStringInFile")]
    [DataRow("MultiReplaceStringInFile")]
    public void ExpandedFileToolKeepsUnstructuredInput(string toolName)
    {
        const string input = "filePath: a.cs\nnot structured";
        InspectFileTool(toolName, input, (_, expander) =>
        {
            var text = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(expander).OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible).Select(block => block.Text);
            CollectionAssert.Contains(text.ToArray(), input);
        });
    }

    private static void InspectFileTool(string toolName, string input, Action<Window, DeferredExpander> inspect)
    {
        using var model = new ChatViewModel();
        var chat = new CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "Editing files.");
        chat.MessageItems.Add(new CopilotChatToolItem("call", toolName, input, "OK"));
        using var message = new MessageItemViewModel(chat);
        model.Messages.Add(message);
        var view = new ChatView { DataContext = model };
        var window = new Window { Width = 1150, Height = 700, Content = view };
        window.Show();
        try
        {
            var expander = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<DeferredExpander>().Single();
            expander.IsExpanded = true;
            window.UpdateLayout();
            inspect(window, expander);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task MessageTextMenuShouldKeepSelectionCopyAndMessageCommandsAsync()
    {
        using var model = new ChatViewModel();
        using var message = new MessageItemViewModel(new AgentLib.Model.CopilotChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, "body text"));
        model.Messages.Add(message);
        var view = new ChatView { DataContext = model };
        var window = new Window { Width = 1150, Height = 700, Content = view };
        window.Show();
        try
        {
            var textBox = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view)
                .OfType<TextBox>().Single(control => control.Classes.Contains("ChatOutput"));
            textBox.Focus();
            textBox.SelectionStart = 0;
            textBox.SelectionEnd = 4;
            var menu = textBox.ContextMenu!;
            menu.Open(textBox);
            var items = menu.Items.OfType<MenuItem>().ToArray();
            Assert.AreSame(message, textBox.Tag, $"Tag={textBox.Tag?.GetType().Name}; Menu={menu.DataContext?.GetType().Name}");
            Assert.IsFalse(items[0].IsEnabled);
            Assert.IsFalse(items[2].IsEnabled);
            Assert.IsNotNull(items[1].Command);
            Assert.AreSame(message, items[3].CommandParameter);
            Assert.AreSame(message, items[4].CommandParameter);
            Assert.AreSame(message.CreateSessionFromSnapshotCommand, items[5].Command);
            menu.Close();
            window.KeyPress(Avalonia.Input.Key.C, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.C, "c");
            Assert.AreEqual("body", await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(window.Clipboard!));
            Assert.AreEqual("body text", textBox.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void FocusedChatOutputShouldNotRenderBlueOuterBorder()
    {
        var textBox = new TextBox
        {
            Classes = { "ChatOutput" },
            Text = "选择这段聊天消息时，只应显示柔和的文本选区，不应显示蓝色 TextBox 外框。",
            Width = 680,
        };
        var bubble = new Border
        {
            Classes = { "MessageBubbleAssistant" },
            Child = textBox,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24),
        };
        var window = new Window
        {
            Width = 760,
            Height = 180,
            Background = Brushes.White,
            Content = bubble,
        };

        window.Show();
        textBox.Focus();
        textBox.SelectionStart = 0;
        textBox.SelectionEnd = textBox.Text?.Length ?? 0;

        using WriteableBitmap bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("无法捕获聊天消息视觉测试截图。");
        string screenshotPath = GetScreenshotPath();
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        bitmap.Save(screenshotPath, PngBitmapEncoderOptions.Default);

        Assert.IsFalse(ContainsBlueFocusBorder(bitmap, textBox, window));

        window.Close();
    }

    private static bool ContainsBlueFocusBorder(WriteableBitmap bitmap, TextBox textBox, Window window)
    {
        Point origin = textBox.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("无法获取聊天文本框的截图坐标。");
        PixelRect bounds = PixelRect.FromRect(new Rect(origin, textBox.Bounds.Size), 1);
        using ILockedFramebuffer framebuffer = bitmap.Lock();
        unsafe
        {
            byte* address = (byte*)framebuffer.Address;
            for (int y = bounds.Y; y < bounds.Bottom; y++)
            {
                if (IsFocusBlue(address, framebuffer.RowBytes, bounds.X, y)
                    || IsFocusBlue(address, framebuffer.RowBytes, bounds.Right - 1, y))
                {
                    return true;
                }
            }

            for (int x = bounds.X; x < bounds.Right; x++)
            {
                if (IsFocusBlue(address, framebuffer.RowBytes, x, bounds.Y)
                    || IsFocusBlue(address, framebuffer.RowBytes, x, bounds.Bottom - 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe bool IsFocusBlue(byte* address, int rowBytes, int x, int y)
    {
        byte* pixel = address + (y * rowBytes) + (x * 4);
        byte blue = pixel[0];
        byte green = pixel[1];
        byte red = pixel[2];
        return blue >= 180 && blue > green + 20 && green > red + 20;
    }

    private static string GetScreenshotPath(string fileName = "ChatOutputFocused.png")
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Screenshots",
            Lang.Current.IetfLanguageTag);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    [TestMethod]
    public void ModelAndReasoningSelectorsShouldRemainAdjacent()
    {
        var chatView = new ChatView
        {
            DataContext = new ChatViewModel(),
        };
        var window = new Window
        {
            Width = 1150,
            Height = 700,
            Content = chatView,
        };

        window.Show();

        ComboBox modelSelector = chatView.FindControl<ComboBox>("ModelSelector")
            ?? throw new InvalidOperationException("未找到模型选择器。");
        ComboBox reasoningSelector = chatView.FindControl<ComboBox>("ReasoningEffortSelector")
            ?? throw new InvalidOperationException("未找到思考强度选择器。");
        Point modelOrigin = modelSelector.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("无法获取模型选择器坐标。");
        Point reasoningOrigin = reasoningSelector.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("无法获取思考强度选择器坐标。");
        double horizontalGap = reasoningOrigin.X - (modelOrigin.X + modelSelector.Bounds.Width);

        Assert.AreEqual(8, horizontalGap, 0.5);

        window.Close();
    }

    [TestMethod]
    public void SelectedUserMessageTextShouldHaveHighContrast()
    {
        var textBox = new TextBox
        {
            Classes = { "ChatOutput", "UserChatOutput" },
            Text = "总结对话",
        };
        var bubble = new Border
        {
            Classes = { "MessageBubbleUser" },
            Child = textBox,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(24),
        };
        var window = new Window
        {
            Width = 340,
            Height = 180,
            Background = Brushes.White,
            Content = bubble,
        };

        window.Show();
        textBox.Focus();
        textBox.SelectionStart = 2;
        textBox.SelectionEnd = 4;

        using WriteableBitmap bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("无法捕获用户消息选区视觉测试截图。");
        bitmap.Save(
            Path.Combine(Path.GetDirectoryName(GetScreenshotPath())!, "UserChatOutputSelection.png"),
            PngBitmapEncoderOptions.Default);

        var selectionBrush = textBox.SelectionBrush as ISolidColorBrush;
        var selectionForegroundBrush = textBox.SelectionForegroundBrush as ISolidColorBrush;
        Assert.AreEqual(Color.Parse("#1976D2"), selectionBrush?.Color);
        Assert.AreEqual(Colors.White, selectionForegroundBrush?.Color);

        window.Close();
    }
}
