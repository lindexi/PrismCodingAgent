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
        => new UiTestMethodAttribute(testMethodAttribute)
        {
            DisplayName = testMethodAttribute.DisplayName,
            UnfoldingStrategy = testMethodAttribute.UnfoldingStrategy,
        };

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
                    var results = new List<TestResult>();
                    foreach (string language in Lang.SupportedLanguageTags)
                    {
                        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language);
                        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                        Lang.SetCurrent(language);
                        try
                        {
                            foreach (TestResult result in await inner.ExecuteAsync(testMethod))
                            {
                                string name = result.DisplayName ?? inner.DisplayName ?? testMethod.TestMethodName;
                                if (result.DisplayName is null && testMethod.Arguments is { Length: > 0 } arguments)
                                {
                                    DataRowAttribute? row = testMethod.GetAttributes<DataRowAttribute>()
                                        .FirstOrDefault(candidate => candidate.Data.SequenceEqual(arguments));
                                    name = row?.GetDisplayName(testMethod.MethodInfo, arguments)
                                        ?? $"{name} ({string.Join(", ", arguments.Select(argument => argument?.ToString() ?? "null"))})";
                                }

                                result.DisplayName = $"{name} [{language}]";
                                results.Add(result);
                            }
                        }
                        catch (Exception exception)
                        {
                            results.Add(new TestResult
                            {
                                DisplayName = $"{testMethod.TestMethodName} [{language}]",
                                Outcome = UnitTestOutcome.Failed,
                                TestFailureException = exception,
                            });
                        }
                    }

                    return results.ToArray();
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
