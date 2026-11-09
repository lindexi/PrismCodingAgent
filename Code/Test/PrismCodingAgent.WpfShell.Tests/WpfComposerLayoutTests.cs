using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfComposerLayoutTests
{
    /// <summary>测量主窗口实际使用的输入视图在空闲和运行可见状态下的操作顺序。</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoopIterationStaysLeftOfComposerActionsAsync(bool isRunning)
        => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Closing += (_, args) => args.Cancel = false;
        try
        {
            window.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var view = (ChatView)window.FindName("ChatComposer");
            var loop = (CheckBox)view.FindName("LoopIterationCheckBox");
            var send = (Button)view.FindName("SendButton");
            var stop = (Button)view.FindName("StopButton");
            stop.SetCurrentValue(UIElement.VisibilityProperty, isRunning ? Visibility.Visible : Visibility.Collapsed);
            window.Measure(new Size(1440, 900));
            window.Arrange(new Rect(0, 0, 1440, 900));
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            double loopRight = loop.TranslatePoint(new Point(loop.ActualWidth, 0), view).X;
            double sendLeft = send.TranslatePoint(default, view).X;
            Assert.IsTrue(loop.ActualWidth > 0 && send.ActualWidth > 0 && loopRight <= sendLeft);
            if (isRunning)
            {
                double stopLeft = stop.TranslatePoint(default, view).X;
                double stopRight = stop.TranslatePoint(new Point(stop.ActualWidth, 0), view).X;
                Assert.IsTrue(stop.ActualWidth > 0 && loopRight <= stopLeft && stopRight <= sendLeft);
            }
        }
        finally
        {
            window.Close();
            await model.DisposeAsync();
        }
    });
}
