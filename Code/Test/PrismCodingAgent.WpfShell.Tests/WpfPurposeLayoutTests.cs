using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfPurposeLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(480)]
    [DataRow(720)]
    [DataRow(1120)]
    public Task WhenEditingCustomPurposeThenDraftAndErrorsStayInPlace(int width) => WpfTestClassAttribute.RunUiAsync(async () =>
    {
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = 1000 };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        window.Show();
        window.UpdateLayout();
        var expander = (Expander)view.FindName("PurposeModelsExpander");
        var add = (Button)view.FindName("AddPurposeModelButton");
        Assert.IsFalse(add.IsVisible);
        expander.IsExpanded = true;
        window.UpdateLayout();
        var header = (System.Windows.Controls.Primitives.ToggleButton)expander.Template.FindName("PurposeHeader", expander);
        Assert.IsTrue(header.Focusable);
        header.BringIntoView();
        window.Activate();
        await WpfInput.WaitAsync(() => window.IsActive, "Settings window should activate.");
        Assert.IsTrue(header.Focus());
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WpfInput.WaitAsync(() => header.IsKeyboardFocused, "Purpose header should own keyboard focus.");
        WpfInput.Key(0x20);
        await WpfInput.WaitAsync(() => !expander.IsExpanded, "Space should collapse purposes.");
        WpfInput.Key(0x20);
        await WpfInput.WaitAsync(() => expander.IsExpanded, "Space should expand purposes.");
        add.Command.Execute(null);
        window.UpdateLayout();
        var list = (ItemsControl)view.FindName("PurposeModelsList");
        var row = model.PurposeModels.Last();
        var name = Descendants(list).OfType<TextBox>().Single(input => ReferenceEquals(input.DataContext, row) && !string.IsNullOrEmpty(PrismCodingAgent.CodingShell.Infrastructure.TextBoxPlaceholder.GetText(input)));
        Assert.AreEqual(string.Empty, name.Text);
        Assert.IsFalse(string.IsNullOrWhiteSpace(PrismCodingAgent.CodingShell.Infrastructure.TextBoxPlaceholder.GetText(name)));
        Assert.IsNotNull(row.PurposeError);
        name.Text = model.PurposeModels[0].PurposeKey;
        Assert.IsNotNull(row.PurposeError);
        name.Text = "ArchitectureReview";
        Assert.IsNull(row.PurposeError);
        var selector = Descendants(list).OfType<ComboBox>().Single(input => ReferenceEquals(input.DataContext, row));
        var unavailable = new PurposeModelOptionViewModel("missing/model", "missing/model", false);
        row.ModelOptions.Add(unavailable);
        selector.SelectedItem = unavailable;
        window.UpdateLayout();
        Assert.IsNotNull(row.ModelError);
        Assert.AreEqual(list.ActualWidth < 632 ? 1 : 0, Grid.GetRow((UIElement)VisualTreeHelper.GetParent(selector)));
        expander.BringIntoView();
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await screenshots.CaptureAsync($"purpose-{width}-error");
        await screenshots.CaptureSurfaceAsync(expander, $"purpose-{width}-section");
        model.RemovePurposeModelCommand.Execute(row);
        Assert.HasCount(9, model.PurposeModels);
    });

    [TestMethod]
    [DataRow(480, 1)]
    [DataRow(720, 1)]
    [DataRow(1120, 2)]
    public Task WhenExpandedThenPurposesWrapWithoutOverflow(int width, int columns) => WpfTestClassAttribute.RunUiAsync(async () =>
    {
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model };
        view.Background = Brushes.White;
        var window = new Window { Content = view, Width = width, Height = 1000, Background = Brushes.White };
        try
        {
            window.Show();
            window.UpdateLayout();
            var expander = (Expander)view.FindName("PurposeModelsExpander");
            Assert.IsFalse(expander.IsExpanded);
            expander.IsExpanded = true;
            window.UpdateLayout();
            var list = (ItemsControl)view.FindName("PurposeModelsList");
            var selectors = Descendants(list).OfType<ComboBox>().ToArray();
            Assert.HasCount(9, selectors);
            var rows = Descendants(list).OfType<Border>().Where(border => border.Name == "PurposeRow").ToArray();
            Assert.HasCount(9, rows);
            var positions = rows.Select(row => row.TranslatePoint(default, list)).ToArray();
            Assert.AreEqual(columns, positions.Count(position => Math.Abs(position.Y - positions[0].Y) < 1));
            Assert.IsTrue(selectors.All(selector => selector.TranslatePoint(default, list).X + selector.ActualWidth <= list.ActualWidth + 1));
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            expander.BringIntoView();
            await screenshots.CaptureAsync($"purpose-{width}-expanded");
            await screenshots.CaptureSurfaceAsync(expander, $"purpose-{width}-section");
        }
        finally
        {
            window.Close();
        }
    });

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
}
