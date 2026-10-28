using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using PrismCodingAgent.AvaloniaShell.Localizations;

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
            => Session.Dispatch(async () =>
            {
                string originalLanguage = Lang.Current.IetfLanguageTag;
                CultureInfo originalCulture = CultureInfo.CurrentCulture;
                CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-Hans");
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
                    Lang.SetCurrent("zh-Hans");
                    return await inner.ExecuteAsync(testMethod);
                }
                finally
                {
                    Lang.SetCurrent(originalLanguage);
                    CultureInfo.CurrentCulture = originalCulture;
                    CultureInfo.CurrentUICulture = originalUICulture;
                }
            }, CancellationToken.None)
                .ContinueWith(static completed => completed.GetAwaiter().GetResult(),
                    CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Default);
    }

    private sealed class TestApplication
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
    }
}
