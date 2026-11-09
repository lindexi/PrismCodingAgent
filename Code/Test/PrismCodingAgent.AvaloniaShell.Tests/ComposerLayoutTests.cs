using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ComposerLayoutTests
{
    /// <summary>测量真实输入视图在空闲和运行可见状态下的操作顺序。</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoopIterationStaysLeftOfComposerActions(bool isRunning)
    {
        using var model = new ChatViewModel();
        var view = new ChatView { DataContext = model };
        var window = new Window { Content = view, Width = 1440, Height = 900 };
        try
        {
            window.Show();
            var loop = view.FindControl<CheckBox>("LoopIterationCheckBox")!;
            var send = view.FindControl<Button>("SendButton")!;
            var stop = view.GetVisualDescendants().OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, model.StopCommand));
            stop.IsVisible = isRunning;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var loopRight = loop.TranslatePoint(new Point(loop.Bounds.Width, 0), view)!.Value.X;
            var sendLeft = send.TranslatePoint(default, view)!.Value.X;
            Assert.IsTrue(loop.Bounds.Width > 0 && send.Bounds.Width > 0 && loopRight <= sendLeft);
            if (isRunning)
            {
                var stopLeft = stop.TranslatePoint(default, view)!.Value.X;
                var stopRight = stop.TranslatePoint(new Point(stop.Bounds.Width, 0), view)!.Value.X;
                Assert.IsTrue(stop.Bounds.Width > 0 && loopRight <= stopLeft && stopRight <= sendLeft);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
