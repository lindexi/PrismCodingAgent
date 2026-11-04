using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.WpfShell.Tests;

/// <summary>Exercises the infrastructure, not migrated business pages.</summary>
[WpfTestClass]
public sealed class WpfInfrastructureTests
{
    private Task OnUiAsync(Func<Task> action)
        => WpfTestClassAttribute.RunUiAsync(async () =>
        {
            VerifyUiAccess();
            TestContext.WriteLine($"UI enter: thread={Environment.CurrentManagedThreadId}; apartment={Thread.CurrentThread.GetApartmentState()}; context={SynchronizationContext.Current?.GetType().Name}; dispatcher={Application.Current.Dispatcher.CheckAccess()}");
            await action();
            VerifyUiAccess();
        });

    private Task OnUiAsync(Action action) => OnUiAsync(() =>
    {
        action();
        return Task.CompletedTask;
    });

    private int _phase;
    private int _initializedThread;
    private string _initializedLanguage = "";
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Records the actual MSTest initialization context.</summary>
    [TestInitialize]
    public async Task InitializeAsync() => await OnUiAsync(async () =>
    {
        _phase = 1;
        VerifyUiAccess();
        _initializedThread = Environment.CurrentManagedThreadId;
        _initializedLanguage = Lang.Current.IetfLanguageTag;
        await Dispatcher.Yield();
        VerifyUiAccess();
        Assert.AreEqual(_initializedThread, Environment.CurrentManagedThreadId);
        _phase = 2;
        TestContext.WriteLine("Initialization continuation completed");
    });

    /// <summary>Checks asynchronous cleanup and removes leftover test windows.</summary>
    [TestCleanup]
    public async Task CleanupAsync() => await OnUiAsync(async () =>
    {
        Assert.AreEqual(2, _phase);
        VerifyUiAccess();
        await Dispatcher.Yield();
        VerifyUiAccess();
        Assert.AreEqual(_initializedThread, Environment.CurrentManagedThreadId);
        Assert.AreEqual(_initializedLanguage, Lang.Current.IetfLanguageTag);
        WpfTestSession.CloseWindows();
        Assert.AreEqual(0, Application.Current.Windows.Count);
        _phase = 3;
        TestContext.WriteLine("Cleanup continuation completed");
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "lifecycle-evidence.txt"),
            $"{TestContext.TestName}; language={_initializedLanguage}; phase={_phase}; thread={Environment.CurrentManagedThreadId}; STA={Thread.CurrentThread.GetApartmentState()}; context={SynchronizationContext.Current?.GetType().Name}; windows={Application.Current.Windows.Count}{Environment.NewLine}");
    });

    /// <summary>Checks that awaits resume on the initialized STA Dispatcher.</summary>
    [TestMethod]
    public async Task ContinuationReturnsToStaDispatcher() => await OnUiAsync(async () =>
    {
        Assert.AreEqual(2, _phase);
        VerifyUiAccess();
        await Task.Delay(15);
        VerifyUiAccess();
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        Assert.AreEqual(_initializedThread, Environment.CurrentManagedThreadId);
        Assert.IsInstanceOfType<DispatcherSynchronizationContext>(SynchronizationContext.Current);
    });

    /// <summary>Checks both MSTest data rows under both language passes.</summary>
    [TestMethod]
    [DataRow(7, DisplayName = "Row seven")]
    [DataRow(19, DisplayName = "Row nineteen")]
    public async Task ParameterRowsRetainValues(int value) => await OnUiAsync(() =>
    {
        Assert.IsTrue(value is 7 or 19);
        Assert.AreEqual(_initializedLanguage, CultureInfo.CurrentUICulture.Name);
        TestContext.WriteLine($"Row={value}; language={_initializedLanguage}; thread={_initializedThread}");
    });

    /// <summary>Injects a failure into the same language runner used by the adapter.</summary>
    [TestMethod]
    public async Task LanguageFailureRestoresCultureAndClosesWindows() => await OnUiAsync(async () =>
    {
        string language = Lang.Current.IetfLanguageTag;
        CultureInfo culture = CultureInfo.CurrentCulture;
        var visited = new List<string>();
        TestResult[] results = await WpfTestClassAttribute.ExecuteLanguagesAsync(async current =>
        {
            VerifyUiAccess();
            Assert.AreEqual(0, Application.Current.Windows.Count);
            Assert.AreEqual(current, CultureInfo.CurrentCulture.Name);
            Assert.AreEqual(current, CultureInfo.CurrentUICulture.Name);
            await Dispatcher.Yield();
            VerifyUiAccess();
            visited.Add(current);
            new Window { Content = new TextBlock { Text = "Failure cleanup probe" } }.Show();
            if (visited.Count == 1)
            {
                throw new InvalidOperationException("Intentional language-runner probe");
            }
            Assert.AreEqual(1, Application.Current.Windows.Count);
            return new[] { new TestResult { Outcome = UnitTestOutcome.Passed } };
        });
        CollectionAssert.AreEqual(Lang.SupportedLanguageTags.ToArray(), visited.ToArray());
        Assert.AreEqual(UnitTestOutcome.Failed, results[0].Outcome);
        Assert.AreEqual(UnitTestOutcome.Passed, results[1].Outcome);
        Assert.AreEqual(language, Lang.Current.IetfLanguageTag);
        Assert.AreEqual(culture, CultureInfo.CurrentCulture);
        Assert.AreEqual(0, Application.Current.Windows.Count);
    });

    /// <summary>Checks resource resolution in the real WPF App without business startup.</summary>
    [TestMethod]
    public async Task ApplicationResourcesResolveAndWindowsClose() => await OnUiAsync(() =>
    {
        Assert.AreEqual(0, Application.Current.Windows.Count);
        Assert.IsInstanceOfType<App>(Application.Current);
        VerifyUiAccess();
        Assert.AreEqual(Color.FromRgb(243, 243, 243),
            ((SolidColorBrush)Application.Current.FindResource("ShellBackgroundBrush")).Color);
        Assert.AreEqual(_initializedThread, Application.Current.Dispatcher.Thread.ManagedThreadId);
        var resource = Application.GetResourceStream(new Uri("/PrismCodingAgent.WpfShell;component/mainwindow.xaml", UriKind.Relative));
        Assert.IsNotNull(resource);
        using (resource.Stream)
        {
            Assert.IsGreaterThan(0L, resource.Stream.Length);
        }
        const string key = "InfrastructureProbeBrush";
        try
        {
            Application.Current.Resources.Add(key, Brushes.MediumPurple);
            var window = new Window { Content = new Border { Width = 200, Height = 100 } };
            window.Show();
            Assert.AreSame(Brushes.MediumPurple, window.FindResource(key));
            window.Close();
            Assert.AreEqual(0, Application.Current.Windows.Count);
        }
        finally
        {
            Application.Current.Resources.Remove(key);
        }
    });

    /// <summary>Checks deadline-based waiting rather than infinite polling.</summary>
    [TestMethod]
    public async Task WaitHasDeadline()
        => await OnUiAsync(async () => await Assert.ThrowsAsync<TimeoutException>(() => WpfInput.WaitAsync(() => false, "probe", TimeSpan.FromMilliseconds(60))));

    /// <summary>Checks cooperative host cancellation and reuse after the UI work has stopped.</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HostDeadlineCancelsWorkBeforeReuse(bool useInputWait)
    {
        bool stopped = false;
        CancellationToken token = default;
        await Assert.ThrowsAsync<TimeoutException>(() => WpfTestClassAttribute.RunUiAsync(async () =>
        {
            VerifyUiAccess();
            token = WpfTestSession.Cancellation;
            try
            {
                if (useInputWait)
                {
                    await WpfInput.WaitAsync(() => false, "Cancellation probe", TimeSpan.FromSeconds(10));
                }
                else
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
            }
            finally
            {
                VerifyUiAccess();
                stopped = true;
            }
        }, TimeSpan.FromMilliseconds(150)));
        await OnUiAsync(() =>
        {
            Assert.IsTrue(token.IsCancellationRequested);
            Assert.IsTrue(stopped);
            Assert.IsFalse(WpfTestSession.Cancellation.IsCancellationRequested);
            Assert.AreEqual(_initializedThread, Environment.CurrentManagedThreadId);
            Assert.AreEqual(0, Application.Current.Windows.Count);
            string evidence = $"Cancellation: language={_initializedLanguage}; inputWait={useInputWait}; requested={token.IsCancellationRequested}; stopped={stopped}; hostReusable=True; thread={Environment.CurrentManagedThreadId}";
            TestContext.WriteLine(evidence);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "cancellation-evidence.txt"), evidence + Environment.NewLine);
        });
    }

    /// <summary>Uses OS mouse and keyboard input and saves actual content, Popup and menu surfaces.</summary>
    [TestMethod]
    public async Task RealInputAndRenderedSurfaces() => await OnUiAsync(async () =>
    {
        var panel = new StackPanel { Margin = new Thickness(24), Background = Brushes.White };
        panel.Children.Add(new TextBlock { Text = $"WPF infrastructure / {Lang.Current.IetfLanguageTag}", FontSize = 24, Foreground = Brushes.Navy });
        var text = new TextBox { Margin = new Thickness(0, 16, 0, 16), Height = 40, FontSize = 20 };
        panel.Children.Add(text);
        var popupButton = new Button { Content = "Open Popup", Height = 44 };
        panel.Children.Add(popupButton);
        var popup = new Popup { PlacementTarget = popupButton, Placement = PlacementMode.Bottom, StaysOpen = true,
            Child = new Border { Background = Brushes.Gold, Padding = new Thickness(24), Child = new TextBlock { Text = "POPUP / actual separate surface", FontSize = 20 } } };
        popupButton.Click += (_, _) => popup.IsOpen = true;
        var menuButton = new Button { Content = "Open menu", Height = 44, Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(menuButton);
        var menu = new ContextMenu { PlacementTarget = menuButton, Style = (Style)Application.Current.FindResource("TaskMenu") };
        var menuItem = new MenuItem { Header = "MENU / keyboard target", Style = (Style)Application.Current.FindResource("TaskMenuItem") };
        menu.Items.Add(menuItem);
        menu.Items.Add(new MenuItem { Header = "Disabled", IsEnabled = false, Style = (Style)Application.Current.FindResource("TaskMenuItem") });
        menu.Items.Add(new MenuItem { Header = "Danger", Style = (Style)Application.Current.FindResource("DangerTaskMenuItem") });
        menuButton.Click += (_, _) => menu.IsOpen = true;
        var window = new Window { Title = "Infrastructure verification only", Width = 620, Height = 400, Left = 80, Top = 80, Content = panel };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            await WpfInput.WaitAsync(() => window.IsLoaded && text.ActualWidth > 0, "Layout unavailable.");
            await WpfInput.ClickAsync(text);
            await WpfInput.WaitAsync(() => text.IsKeyboardFocused, "Mouse did not focus text box.");
            WpfInput.Text("Input 42");
            await WpfInput.WaitAsync(() => text.Text == "Input 42", "Unicode keyboard input missing.");
            WpfInput.Key(0x08);
            await WpfInput.WaitAsync(() => text.Text == "Input 4", "Backspace did not reach text box.");
            VerifyPng(await screenshots.CaptureAsync("typed-content"));
            await WpfInput.ClickAsync(popupButton);
            await WpfInput.WaitAsync(() => popup.IsOpen && popup.Child.IsVisible, "Mouse did not open Popup.");
            VerifyPng(await screenshots.CaptureSurfaceAsync((FrameworkElement)popup.Child, "popup-surface"));
            popup.IsOpen = false;
            await WpfInput.ClickAsync(menuButton);
            await WpfInput.WaitAsync(() => menu.IsOpen && menu.ActualWidth > 0, "Mouse did not open menu.");
            Assert.IsNotNull(menu.Template);
            Assert.IsTrue(menuItem.ActualHeight >= 36);
            Assert.AreEqual(Color.FromRgb(250, 250, 250), ((SolidColorBrush)menu.Background).Color);
            VerifyPng(await screenshots.CaptureSurfaceAsync(menu, "menu-surface"));
            WpfInput.Key(0x1B);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Escape did not dismiss menu.");
        }
        finally
        {
            popup.IsOpen = false;
            menu.IsOpen = false;
        }
    });

    private static void VerifyUiAccess()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        Assert.IsTrue(Application.Current.Dispatcher.CheckAccess());
        Assert.IsInstanceOfType<DispatcherSynchronizationContext>(SynchronizationContext.Current);
    }

    private static void VerifyPng(string path)
    {
        using FileStream stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        Assert.IsTrue(frame.PixelWidth > 50 && frame.PixelHeight > 20);
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        Assert.IsTrue(Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[i * 4 + 3] != 0));
        Assert.IsTrue(Enumerable.Range(1, pixels.Length / 4 - 1).Any(i => !pixels.AsSpan(i * 4, 4).SequenceEqual(pixels.AsSpan(0, 4))));
    }
}
