using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfSandboxAndLspTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CopyLspCommandThroughRealClipboardAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase5Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false,
        }));
        var previous = Clipboard.GetDataObject();
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var diagnostics = new List<string>();
        void Record(string message)
        {
            diagnostics.Add(message);
            TestContext.WriteLine(message);
        }
        try
        {
            var content = (FrameworkElement)window.Content;
            window.UpdateLayout();
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            await WpfInput.ClickAsync((Button)window.FindName("SettingsButton"));
            await WpfInput.WaitAsync(() => model.IsSettingsOpen, "Settings did not open.");
            var view = Descendants(window).OfType<SettingsView>().Single();
            var save = (Button)view.FindName("SaveButton");
            Assert.AreSame(model.SettingsViewModel!.SaveCommand, save.Command);
            Assert.AreEqual("IsBusy", save.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            Assert.AreSame(Application.Current.FindResource("InverseBoolConverter"), save.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Converter);
            var progress = (ProgressBar)view.FindName("SettingsProgress");
            Assert.AreEqual("IsBusy", progress.GetBindingExpression(UIElement.VisibilityProperty)!.ParentBinding.Path.Path);
            Assert.AreEqual(Visibility.Collapsed, progress.Visibility);
            var models = Descendants(view).OfType<ModelSettingsView>().Single();
            var editor = (StackPanel)models.Content;
            Assert.AreEqual("IsBusy", editor.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            Assert.AreSame(Application.Current.FindResource("InverseBoolConverter"), editor.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Converter);
            Assert.IsTrue(editor.IsEnabled);
            var address = (TextBox)view.FindName("SandboxAddress");
            Assert.IsFalse(address.IsEnabled);
            Assert.IsFalse(model.SettingsViewModel!.TestWindowsSandboxConnectionCommand.CanExecute(null));
            var copy = (Button)view.FindName("CopyLanguageServerCommandButton");
            copy.BringIntoView();
            window.UpdateLayout();
            var scroll = Descendants(view).OfType<ScrollViewer>().First();
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 100);
            window.UpdateLayout();
            await screenshots.CaptureAsync("lsp-empty-feedback");
            bool clickHandled = false;
            copy.Click += (_, _) =>
            {
                clickHandled = true;
                Record($"Copy Click handled: {ClipboardState()}; status={((TextBlock)view.FindName("LanguageServerStatus")).Text}; active={window.IsActive}; focused={copy.IsKeyboardFocusWithin}; mouseOver={copy.IsMouseOver}");
            };
            Record($"Before copy: {ClipboardState()}; status={((TextBlock)view.FindName("LanguageServerStatus")).Text}");
            await WpfInput.ClickAsync(copy);
            await WpfInput.WaitAsync(() => ((TextBlock)view.FindName("LanguageServerStatus")).Text == Lang.Current.LanguageServerCommandCopied,
                "Copy feedback did not appear.");
            Assert.IsTrue(clickHandled, "The real mouse Click was not handled.");
            string actual = WpfInput.ReadClipboard(TestContext);
            Record($"Before assertion: {ClipboardState()}; textLength={actual.Length}");
            Assert.AreEqual("dotnet tool update -g roslyn-language-server", actual);
            Assert.AreEqual(Lang.Current.InstallOrUpdateLanguageServerText, ((Button)view.FindName("InstallLanguageServerButton")).Content);
            Assert.IsFalse(model.SettingsViewModel.IsSandboxConnectionTestRunning);
            window.UpdateLayout();
            await screenshots.CaptureAsync("lsp-copied-sandbox-disabled");
        }
        finally
        {
            Record($"Before restore (assertion already completed or failed): {ClipboardState()}");
            if (previous is null) Clipboard.Clear();
            else Clipboard.SetDataObject(previous, true);
            Record($"After restore: {ClipboardState()}");
            string diagnosticPath = Path.Combine(screenshots.DirectoryPath, "clipboard-diagnostics.txt");
            await File.WriteAllLinesAsync(diagnosticPath, diagnostics);
            TestContext.AddResultFile(diagnosticPath);
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Window did not close.");
            await model.DisposeAsync();
        }
    });

    private static string ClipboardState()
    {
        nint owner = GetClipboardOwner();
        GetWindowThreadProcessId(owner, out uint ownerProcessId);
        return $"sequence={GetClipboardSequenceNumber()}; owner=0x{owner:X}; ownerPid={ownerProcessId}; testPid={Environment.ProcessId}";
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern nint GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
