using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfPurposeLayoutTests
{
    [TestMethod]
    [DataRow(480, 1)]
    [DataRow(720, 1)]
    [DataRow(1120, 2)]
    public Task WhenExpandedThenPurposesWrapWithoutOverflow(int width, int columns) => WpfTestClassAttribute.RunUiAsync(() =>
    {
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = 1000 };
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
            Assert.AreEqual(9, selectors.Length);
            var positions = selectors.Select(selector => selector.TranslatePoint(default, list)).ToArray();
            Assert.AreEqual(columns, positions.Count(position => Math.Abs(position.Y - positions[0].Y) < 1));
            Assert.IsTrue(selectors.All(selector => selector.TranslatePoint(default, list).X + selector.ActualWidth <= list.ActualWidth + 1));
        }
        finally
        {
            window.Close();
        }
        return Task.CompletedTask;
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
