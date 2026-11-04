using System.Globalization;
using System.Runtime.CompilerServices;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.WpfShell.Tests;

internal sealed class WpfTestClassAttribute : TestClassAttribute
{
    private static readonly Lazy<WpfTestSession> SharedSession = new(() => new WpfTestSession());

    internal static async Task RunUiAsync(Func<Task> action, TimeSpan? timeout = null)
        => await SharedSession.Value.ExecuteAsync(async () =>
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo uiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(Lang.Current.IetfLanguageTag);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(Lang.Current.IetfLanguageTag);
                await action();
                return true;
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;
            }
        }, timeout).ConfigureAwait(false);

    internal static void Shutdown()
    {
        if (SharedSession.IsValueCreated)
        {
            SharedSession.Value.Dispose();
        }
    }

    internal static async Task<TestResult[]> ExecuteLanguagesAsync(Func<string, Task<TestResult[]>> action)
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        var results = new List<TestResult>();
        try
        {
            foreach (string language in Lang.SupportedLanguageTags)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                Lang.SetCurrent(language);
                try
                {
                    results.AddRange(await action(language));
                }
                catch (Exception exception)
                {
                    results.Add(new TestResult
                    {
                        DisplayName = $"Language execution [{language}]",
                        Outcome = UnitTestOutcome.Failed,
                        TestFailureException = exception,
                    });
                }
                finally
                {
                    try
                    {
                        await WpfTestSession.CloseWindowsAsync();
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
                    {
                        results.Add(new TestResult
                        {
                            DisplayName = $"Window cleanup [{language}]",
                            Outcome = UnitTestOutcome.Failed,
                            TestFailureException = exception,
                        });
                    }
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
    }

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
            => SharedSession.Value.ExecuteAsync(() => ExecuteLanguagesAsync(async language =>
            {
                TestResult[] results = await inner.ExecuteAsync(testMethod);
                foreach (TestResult result in results)
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
                }
                return results;
            }));
    }
}
