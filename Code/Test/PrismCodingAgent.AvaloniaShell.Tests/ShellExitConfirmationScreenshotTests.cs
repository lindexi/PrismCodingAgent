using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过系统关闭入口和真实输入检查退出确认，并保留关闭前各步骤截图。</summary>
[AvaloniaTestClass]
public sealed class ShellExitConfirmationScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExitConfirmationShouldCancelThroughMouseAndEscapeThenCloseThroughMouse()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        session.Capture("main-window");
        Border overlay = session.Window.FindControl<Border>("ExitOverlay")!;
        Control mainContent = session.Window.FindControl<Control>("MainContent")!;

        session.Window.Close();
        session.Capture("exit-confirmation");
        Assert.IsTrue(overlay.IsVisible && !mainContent.IsEnabled && session.Window.IsVisible);

        session.Click(session.Window.FindControl<Button>("CancelExitButton")!);
        session.Capture("main-after-mouse-cancel");
        Assert.IsTrue(!overlay.IsVisible && mainContent.IsEnabled && session.Window.IsVisible);

        session.Window.Close();
        session.Capture("exit-confirmation-before-escape");
        session.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        session.Capture("main-after-escape-cancel");
        Assert.IsTrue(!overlay.IsVisible && mainContent.IsEnabled && session.Window.IsVisible);

        session.Window.Close();
        session.Capture("exit-confirmation-before-confirm");
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, EventArgs args) => closed.TrySetResult();
        session.Window.Closed += OnClosed;
        try
        {
            session.Click(session.Window.FindControl<Button>("ConfirmExitButton")!);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.IsFalse(session.Window.IsVisible);
        }
        finally
        {
            session.Window.Closed -= OnClosed;
        }
    }
}
