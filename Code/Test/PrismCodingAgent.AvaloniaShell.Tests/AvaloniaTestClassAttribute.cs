using System.Runtime.CompilerServices;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Headless;
using TKCode.Arc;
using TKCode.Arc.Fonts;

namespace PrismCodingAgent.AvaloniaShell.Tests;

internal sealed class AvaloniaTestClassAttribute : TestClassAttribute
{
    private static readonly Lazy<HeadlessUnitTestSession> SharedSession = new(
        () => HeadlessUnitTestSession.StartNew(typeof(TestApplication)));

    internal static HeadlessUnitTestSession Session => SharedSession.Value;

    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        => new UiTestMethodAttribute(testMethodAttribute);

    private sealed class UiTestMethodAttribute(
        TestMethodAttribute inner,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = -1) : TestMethodAttribute(callerFilePath, callerLineNumber)
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
            // Headless completes dispatch inline. MSTest must not run assembly cleanup on
            // that thread: disposing the session would wait for its own dispatch loop.
            => Session.Dispatch(() => inner.ExecuteAsync(testMethod), CancellationToken.None)
                .ContinueWith(static completed => completed.GetAwaiter().GetResult(),
                    CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Default);
    }

    private sealed class TestApplication
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .WithArcFonts()
            .UseAtomUI(builder =>
            {
                builder.UseDesktopControls();
                builder.UseArc();
                builder.UseArcSpecial();
            });
    }
}
