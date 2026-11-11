using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;
using PrismCodingAgent.WpfShell.Tests;

namespace WpfSandboxRunner;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] != "--remote-sandbox")
            return 2;

        string output = Path.Combine(AppContext.BaseDirectory, "Diagnostics");
        Directory.CreateDirectory(output);
        var results = new List<CaseResult>();
        var app = new App(ownsStartup: false);
        Application.LoadComponent(app, new Uri("/PrismCodingAgent.WpfShell;component/App.xaml", UriKind.Relative));
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        foreach (string language in new[] { "zh-Hans", "en", "de" })
        {
            Lang.SetCurrent(language);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            foreach (int width in new[] { 480, 720, 1120 })
            {
                var result = new CaseResult(language, width);
                results.Add(result);
                try
                {
                    Diagnose(result, output);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // A diagnostic boundary preserves other cases and the original exception.
                    result.Errors.Add(exception.ToString());
                }
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
                {
                    TestAssembly = typeof(WpfPurposeLayoutTests).Assembly.FullName,
                    Mode = "Public API STA diagnostics; no MSTest TestContext simulation",
                    Results = results,
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        app.Shutdown();
        return results.All(result => result.Errors.Count == 0) ? 0 : 1;
    }

    private static void Diagnose(CaseResult result, string output)
    {
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model, Background = Brushes.White };
        var window = new Window { Content = view, Width = result.Width, Height = 1000, Background = Brushes.White };
        try
        {
            window.Show();
            Pump(window);
            var expander = (Expander)view.FindName("PurposeModelsExpander");
            var add = (Button)view.FindName("AddPurposeModelButton");
            Check(!expander.IsExpanded && !add.IsVisible, "InitiallyCollapsed", result);
            expander.IsExpanded = true;
            Pump(window);
            var list = (ItemsControl)view.FindName("PurposeModelsList");
            var rows = Descendants(list).OfType<Border>().Where(row => row.Name == "PurposeRow").ToArray();
            var selectors = Descendants(list).OfType<ComboBox>().ToArray();
            Check(rows.Length == 9 && selectors.Length == 9, "NineBuiltInPurposes", result);
            result.ListWidth = list.ActualWidth;
            result.Columns = rows.Count(row => Math.Abs(row.TranslatePoint(default, list).Y - rows[0].TranslatePoint(default, list).Y) < 1);
            Check(result.Columns == (result.Width == 1120 ? 2 : 1), "ExpectedColumns", result);
            Check(selectors.All(selector => selector.TranslatePoint(default, list).X >= -1 && selector.TranslatePoint(default, list).X + selector.ActualWidth <= list.ActualWidth + 1), "NoSelectorOverflow", result);
            Capture(expander, $"{result.Language}-{result.Width}-expanded.png", output, result);
            add.Command.Execute(null);
            Pump(window);
            var draft = model.PurposeModels.Last();
            var name = Descendants(list).OfType<TextBox>().Single(input => ReferenceEquals(input.DataContext, draft) && !string.IsNullOrEmpty(PrismCodingAgent.CodingShell.Infrastructure.TextBoxPlaceholder.GetText(input)));
            Check(name.Text == string.Empty && draft.PurposeError is not null, "EmptyPurposeError", result);
            name.Text = model.PurposeModels[0].PurposeKey;
            Check(draft.PurposeError is not null, "DuplicatePurposeError", result);
            name.Text = "ArchitectureReview";
            Check(draft.PurposeError is null, "ValidPurposeClearsError", result);
            var selector = Descendants(list).OfType<ComboBox>().Single(input => ReferenceEquals(input.DataContext, draft));
            var unavailable = new PurposeModelOptionViewModel("missing/model", "missing/model", false);
            draft.ModelOptions.Add(unavailable);
            selector.SelectedItem = unavailable;
            Pump(window);
            Check(draft.ModelError is not null && name.Text == "ArchitectureReview", "UnavailableModelPreservesDraft", result);
            Check(Grid.GetRow((UIElement)VisualTreeHelper.GetParent(selector)) == (list.ActualWidth < 632 ? 1 : 0), "ResponsiveCustomRow", result);
            result.PurposeError = draft.PurposeError;
            result.ModelError = draft.ModelError;
            Capture(expander, $"{result.Language}-{result.Width}-error.png", output, result);
            model.RemovePurposeModelCommand.Execute(draft);
            Check(model.PurposeModels.Count == 9, "RemoveCustomPurpose", result);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Check(bool passed, string check, CaseResult result)
    {
        result.Checks.Add(new CheckResult(check, passed));
        if (!passed)
            result.Errors.Add(check);
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static void Capture(FrameworkElement surface, string name, string output, CaseResult result)
    {
        surface.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(surface);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(surface.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name));
        encoder.Save(stream);
        result.Screenshots.Add(name);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private sealed record CheckResult(string Name, bool Passed);
    private sealed record CaseResult(string Language, int Width)
    {
        public double ListWidth { get; set; }
        public int Columns { get; set; }
        public string? PurposeError { get; set; }
        public string? ModelError { get; set; }
        public List<CheckResult> Checks { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Screenshots { get; } = [];
    }
}
