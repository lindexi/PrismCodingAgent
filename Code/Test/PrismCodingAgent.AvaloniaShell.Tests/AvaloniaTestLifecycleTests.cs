using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using PrismCodingAgent.AvaloniaShell.Localizations;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class AvaloniaTestLifecycleTests : IDisposable
{
    private int _initializationThread;
    private string? _language;
    private bool _disposed;

    /// <summary>标记实例已清理，防止下一语言误用已释放的实例。</summary>
    public void Dispose() => _disposed = true;

    [TestInitialize]
    public void RememberUiThread()
    {
        Dispatcher.UIThread.VerifyAccess();
        _initializationThread = Environment.CurrentManagedThreadId;
        Assert.IsFalse(_disposed);
        Assert.IsNull(_language, "Each language must receive a newly initialized test instance.");
        _language = Lang.Current.IetfLanguageTag;
        CollectionAssert.Contains(Lang.SupportedLanguageTags.ToArray(), _language);
        Assert.AreEqual((_language, _language),
            (CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name));
    }

    [TestCleanup]
    public Task VerifyCleanupThreadAsync() => Dispatcher.UIThread.InvokeAsync(async () =>
    {
        await Task.Yield();
        Dispatcher.UIThread.VerifyAccess();
        Assert.IsFalse(_disposed);
        Assert.AreEqual(_language, Lang.Current.IetfLanguageTag);
        Assert.AreEqual(_initializationThread, Environment.CurrentManagedThreadId);
    });

    [TestMethod]
    [DataRow(1, DisplayName = "first lifecycle row")]
    [DataRow(2, DisplayName = "second lifecycle row")]
    public void WhenTestIsParameterizedThenInitializationBelongsToThisLanguage(int value)
    {
        Assert.IsTrue(value is 1 or 2);
        Assert.IsFalse(_disposed);
        Assert.AreEqual(_language, Lang.Current.IetfLanguageTag);
    }

    [TestMethod]
    public void WhenTestIsSynchronousThenItRunsOnInitializationThread()
    {
        Dispatcher.UIThread.VerifyAccess();
        Assert.AreEqual(_initializationThread, Environment.CurrentManagedThreadId);
    }

    [TestMethod]
    public async Task WhenTestAwaitsThenUiAccessIsPreserved()
    {
        await Task.Yield();
        Dispatcher.UIThread.VerifyAccess();
        var window = new Window { Content = new TextBlock { Text = "ready" } };
        window.Show();
        try
        {
            Assert.AreEqual(_initializationThread, Environment.CurrentManagedThreadId);
        }
        finally
        {
            window.Close();
        }
    }
}
