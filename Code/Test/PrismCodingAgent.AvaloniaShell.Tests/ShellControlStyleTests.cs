using System.Globalization;
using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Converters;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using ArcSelect = TKCode.Arc.Select;
using Markdown = TKCode.Arc.Markdown;
using ChatView = PrismCodingAgent.AvaloniaShell.Views.ChatView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>
/// 守护应用外壳在 Arc 主题下的布局契约：控件之间的相对位置、最小窗口宽度、内容不出界。
/// </summary>
[AvaloniaTestClass]
public sealed class ShellControlStyleTests
{
    [TestMethod]
    public void HistoryIconShouldAlignWithReasoningSelectorBottom()
    {
        var view = new ChatView { DataContext = new ChatViewModel() };
        var window = new Window { Width = 980, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var history = view.FindControl<Button>("TaskHistoryButton") ?? throw new InvalidOperationException("History missing.");
            var reasoning = view.FindControl<ArcSelect>("ReasoningEffortSelector") ?? throw new InvalidOperationException("Reasoning missing.");
            var historyPosition = history.TranslatePoint(default, view) ?? throw new InvalidOperationException("Position missing.");
            var reasoningPosition = reasoning.TranslatePoint(default, view) ?? throw new InvalidOperationException("Position missing.");
            Assert.AreEqual(reasoningPosition.Y + reasoning.Bounds.Height, historyPosition.Y + history.Bounds.Height, 0.5);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void NewSessionShouldFollowLanguageServerButton()
    {
        var view = new ChatView { DataContext = new ChatViewModel() };
        var window = new Window { Width = 980, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var create = view.FindControl<Button>("NewSessionButton") ?? throw new InvalidOperationException("New session missing.");
            var lsp = view.FindControl<Button>("StopLanguageServerButton") ?? throw new InvalidOperationException("LSP missing.");
            Assert.IsGreaterThanOrEqualTo(lsp.Bounds.Right, create.Bounds.Left);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void MainWindowShouldRequestMicaWithOpaqueFallback()
    {
        var window = new PrismCodingAgent.AvaloniaShell.MainWindow();
        try
        {
            CollectionAssert.AreEqual(new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.None }, window.TransparencyLevelHint.ToArray());
            var fallback = window.TransparencyBackgroundFallback as ISolidColorBrush
                ?? throw new InvalidOperationException("窗口透明回退色应来自 Arc 的 Background 令牌。");
            Assert.AreEqual(255, fallback.Color.A, "回退色必须不透明，否则窗口会透出桌面。");
            var background = window.Background as ISolidColorBrush
                ?? throw new InvalidOperationException("Window background is not a solid brush.");
            Assert.AreEqual(Colors.White, background.Color);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void MicaTransparencyShouldUseTransparentWindowBackground()
    {
        var converter = new WindowTransparencyBackgroundConverter();

        var background = (ISolidColorBrush) converter.Convert(WindowTransparencyLevel.Mica, typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(Colors.Transparent, background.Color);
    }

    [TestMethod]
    public void ButtonShouldAcceptKeyboardNavigationFocus()
    {
        var button = new Button { Content = "Action" };
        var window = new Window { Content = button };
        try
        {
            window.Show();
            button.Focus(Avalonia.Input.NavigationMethod.Tab);
            Assert.IsTrue(button.IsFocused);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ArcButtonsShouldAcceptKeyboardNavigationFocus()
    {
        var button = new TKCode.Arc.Button { Content = "Action" };
        var window = new Window { Content = button };
        try
        {
            window.Show();
            button.Focus(Avalonia.Input.NavigationMethod.Tab);
            Assert.IsTrue(button.IsFocused);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ComposerActionsShouldFitAtMinimumWindowWidth()
    {
        var view = new ChatView { DataContext = new ChatViewModel() };
        var window = new Window { Width = 1016, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var checkbox = view.FindControl<CheckBox>("EnableDotNetRunCheckBox") ?? throw new InvalidOperationException("Checkbox missing.");
            var button = view.FindControl<Button>("CompressConversationButton") ?? throw new InvalidOperationException("Button missing.");
            Point checkboxOrigin = checkbox.TranslatePoint(default, view) ?? throw new InvalidOperationException("Checkbox position missing.");
            Point buttonOrigin = button.TranslatePoint(default, view) ?? throw new InvalidOperationException("Button position missing.");
            Assert.IsLessThanOrEqualTo(buttonOrigin.X, checkboxOrigin.X + checkbox.Bounds.Width);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ComposerPanelShouldRemainCompactWithoutAttachments()
    {
        var view = new ChatView { DataContext = new ChatViewModel() };
        var window = new Window { Width = 1016, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var panel = view.FindControl<Border>("ComposerPanel") ?? throw new InvalidOperationException("Composer missing.");
            Assert.IsLessThanOrEqualTo(150, panel.Bounds.Height, $"Composer height: {panel.Bounds.Height}");
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow(640)]
    [DataRow(1160)]
    public void MessageContentShouldFitInsideScrollViewport(int width)
    {
        using var chat = new ChatViewModel();
        chat.Messages.Add(new MessageItemViewModel(CopilotChatMessage.CreateAssistant(
            string.Concat(Enumerable.Repeat("Long message text should wrap inside the viewport. ", 40)), isPresetInfo: false)));
        var view = new ChatView { DataContext = chat };
        var window = new Window { Width = width, Height = 620, Content = view };
        try
        {
            window.Show();
            window.UpdateLayout();
            var scroll = view.FindControl<ScrollViewer>("MessagesScrollViewer")
                ?? throw new InvalidOperationException("Message scroll missing.");
            var items = view.FindControl<ItemsControl>("MessagesItemsControl")
                ?? throw new InvalidOperationException("Message items missing.");
            var presenter = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
            var origin = items.TranslatePoint(default, presenter)
                ?? throw new InvalidOperationException("Message position missing.");

            var viewOrigin = items.TranslatePoint(default, view)
                ?? throw new InvalidOperationException("Message position missing.");
            var markdown = items.GetVisualDescendants().OfType<Markdown>().First();
            var outputOrigin = markdown.TranslatePoint(default, presenter)
                ?? throw new InvalidOperationException("Assistant output position missing.");
            Assert.IsTrue(origin.X >= 0 && origin.X + items.Bounds.Width <= presenter.Bounds.Width + 0.5
                && outputOrigin.X + markdown.Bounds.Width <= presenter.Bounds.Width + 0.5
                && scroll.Margin == default(Thickness)
                && viewOrigin.X >= 24 && viewOrigin.X + items.Bounds.Width <= view.Bounds.Width - 12 + 0.5,
                $"Content x={origin.X}, width={items.Bounds.Width}, viewport={presenter.Bounds.Width}, view x={viewOrigin.X}");
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void PresetGreetingShouldUseWelcomeLayout()
    {
        using var chat = new ChatViewModel();
        chat.Messages.Add(new MessageItemViewModel(CopilotChatMessage.CreateAssistant("Greeting", isPresetInfo: true)));
        Assert.IsTrue(chat.ShowWelcome);
    }

    [TestMethod]
    public void RealAssistantMessageShouldNotBeHiddenByWelcomeLayout()
    {
        using var chat = new ChatViewModel();
        chat.Messages.Add(new MessageItemViewModel(CopilotChatMessage.CreateAssistant("Answer", isPresetInfo: false)));
        Assert.IsFalse(chat.ShowWelcome);
    }
}
