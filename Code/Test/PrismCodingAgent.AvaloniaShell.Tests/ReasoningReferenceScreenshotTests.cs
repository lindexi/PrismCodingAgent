using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.ViewModels;
using ChatView = PrismCodingAgent.CodingShell.Views.ChatView;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ReasoningReferenceScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProductionReasoningTemplateStartsCollapsedAndExpandsThroughRealInputWithoutExecution()
    {
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer 第一行\nSecond line 第二行 " + new string('x', 240));
        message.MessageItems.Insert(0, new CopilotChatReasoningItem("Reasoning 推理过程\nCheck each condition 检查每个条件。"));
        using var projection = new MessageItemViewModel(message);
        var view = new ChatView();
        var content = new ContentControl
        {
            Content = projection,
            ContentTemplate = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate,
            Margin = new Thickness(24),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        view.Content = content;
        var window = new Window { Width = 800, Height = 440, Content = view, Background = Brushes.White };
        string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", TestContext.TestName!,
            Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
        try
        {
            window.Show();
            window.UpdateLayout();
            var bubble = content.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Classes.Contains("MessageBubbleAssistant"));
            Assert.AreEqual(752d, bubble.Bounds.Width, 1d);
            var expander = content.GetVisualDescendants().OfType<Expander>().Single();
            Assert.IsFalse(expander.IsExpanded);
            await CaptureAsync("01-reasoning-collapsed-bilingual.png");

            var toggle = expander.GetVisualDescendants().OfType<ToggleButton>().First();
            Point point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)
                ?? throw new InvalidOperationException("Reasoning header is not attached to the reference window.");
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.IsTrue(expander.IsExpanded);
            Assert.IsTrue(expander.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == "Reasoning 推理过程\nCheck each condition 检查每个条件。"));
            var answer = content.GetVisualDescendants().OfType<TextBox>()
                .Single(text => text.Text?.StartsWith("Answer 第一行", StringComparison.Ordinal) == true);
            Point origin = answer.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("Answer is not attached to the reference window.");
            var bounds = new Rect(origin, answer.Bounds.Size);
            Assert.IsTrue(bounds.Left >= 24 && bounds.Right <= window.ClientSize.Width - 24
                && bounds.Top >= 0 && bounds.Bottom <= window.ClientSize.Height,
                $"Long text layout was clipped: {bounds}; window={window.ClientSize}.");
            Assert.IsGreaterThanOrEqualTo(answer.FontSize * 3, answer.Bounds.Height);
            await CaptureAsync("02-reasoning-expanded-bilingual.png");
            message.AppendUsageDetails(new UsageDetails { InputTokenCount = 12, OutputTokenCount = 8, TotalTokenCount = 20 });
            await CaptureAsync("03-reasoning-usage-public-projection-not-execution.png");
            Assert.IsTrue(content.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == projection.UsageSummaryText));
        }
        finally
        {
            window.Close();
        }

        async Task CaptureAsync(string name)
        {
            window.MouseMove(new Point(790, 430));
            // Capture the settled normal state rather than the Fluent chevron transition or hover.
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            using var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("No reasoning reference frame.");
            frame.Save(path, PngBitmapEncoderOptions.Default);
            TestContext.WriteLine(path);
            TestContext.AddResultFile(path);
        }
    }
}
