using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.ViewModels;
using SettingsView = PrismCodingAgent.CodingShell.Views.SettingsView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("zh-Hans")]
public sealed class PurposeModelControlTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(480, 1)]
    [DataRow(720, 1)]
    [DataRow(1120, 2)]
    public void WhenExpandedThenPurposesWrapWithoutOverflow(int width, int columns)
    {
        PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent("zh-Hans");
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = 2100 };
        try
        {
            window.Show();
            var expander = view.FindControl<Expander>("PurposeModelsExpander")!;
            Assert.IsFalse(expander.IsExpanded);
            expander.IsExpanded = true;
            Layout();
            var list = view.FindControl<ItemsControl>("PurposeModelsList")!;
            var selectors = list.GetVisualDescendants().OfType<ComboBox>().ToArray();
            var positions = selectors.Select(selector => selector.TranslatePoint(default, list)!.Value).ToArray();
            Assert.AreEqual(columns, positions.Count(position => Math.Abs(position.Y - positions[0].Y) < 1));
            for (int i = 0; i < selectors.Length; i++)
            {
                var fields = (Grid)selectors[i].Parent!;
                var label = (TextBlock)fields.Children[0];
                var labelPosition = label.TranslatePoint(default, list)!.Value;
                Assert.AreEqual(8, positions[i].X - labelPosition.X - label.Bounds.Width, 1);
                Assert.AreEqual(positions[i % columns].X, positions[i].X, 1);
                Assert.AreEqual(labelPosition.Y + label.Bounds.Height / 2, positions[i].Y + 18, 1);
                Assert.AreEqual(36, selectors[i].Bounds.Height, 1);
                Assert.IsTrue(label.TextLayout.Width <= 64);
                Assert.IsTrue(positions[i].X + selectors[i].Bounds.Width <= list.Bounds.Width + 1);
            }
            Assert.AreEqual(8, positions[columns].Y - positions[0].Y - 36, 1);
            model.AddPurposeModelCommand.Execute(null);
            model.AddPurposeModelCommand.Execute(null);
            model.PurposeModels.Last().PurposeKey = "ArchitectureReviewWithExtendedContextAndLongPurposeName";
            model.AddPurposeModelCommand.Execute(null);
            var invalid = model.PurposeModels.Last();
            invalid.PurposeKey = "旧配置分析";
            var unavailable = new PurposeModelOptionViewModel("missing/model", "missing/model（不可用）", false);
            invalid.ModelOptions.Add(unavailable);
            invalid.SelectedModel = unavailable;
            Layout();
            var customRows = list.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("CustomPurpose")).ToArray();
            Assert.HasCount(3, customRows);
            foreach (var border in customRows)
            {
                var fields = (Grid)border.Child!;
                var input = (TextBox)fields.Children[1];
                var selector = (ComboBox)fields.Children[2];
                var remove = (Button)fields.Children[5];
                Assert.AreEqual("用途名称", input.PlaceholderText);
                Assert.AreEqual(new Thickness(0), border.BorderThickness);
                Assert.AreEqual(input.Bounds.Center.Y, remove.Bounds.Center.Y, 1);
                Assert.AreEqual(width == 1120 ? 0 : 2, Grid.GetRow(selector));
                Assert.IsTrue(remove.Bounds.Left - (width == 1120 ? selector.Bounds.Right : input.Bounds.Right) <= 16);
                foreach (var error in fields.Children.OfType<TextBlock>().Where(text => text.IsVisible && Grid.GetRow(text) > 0))
                {
                    var control = Grid.GetColumn(error) == 0 && Grid.GetRow(error) == 1 ? (Control)input : selector;
                    Assert.IsTrue(error.Bounds.Top >= control.Bounds.Bottom - 1);
                }
            }
            for (int i = 1; i < customRows.Length; i++)
                Assert.IsTrue(customRows[i].TranslatePoint(default, list)!.Value.Y >= customRows[i - 1].TranslatePoint(default, list)!.Value.Y + customRows[i - 1].Bounds.Height);
            Capture($"purpose-expanded-{width}.png");
            Capture($"purpose-error-{width}.png");
            expander.IsExpanded = false;
            Layout();
            Assert.IsFalse(view.FindControl<Button>("AddPurposeModelButton")!.IsEffectivelyVisible);
            Capture($"purpose-collapsed-{width}.png");

            void Layout()
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            void Capture(string name)
            {
                Layout();
                var heading = view.FindControl<TextBlock>("ModelConfigurationHeading")!;
                var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Vector(0, heading.TranslatePoint(default, scroll)!.Value.Y + scroll.Offset.Y - 8);
                Layout();
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "PrismCodingAgent", "Code"))) root = root.Parent;
                string directory = Path.Combine(root?.FullName ?? AppContext.BaseDirectory, "PrismCodingAgent", "artifacts", "ui-review", "purpose-models");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, name);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
                image.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                TestContext.AddResultFile(path);
            }
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void WhenHeaderReceivesSpaceThenContentExpandsAndCollapses()
    {
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model };
        var window = new Window { Content = view, Width = 720, Height = 1000 };
        try
        {
            window.Show();
            var expander = view.FindControl<Expander>("PurposeModelsExpander")!;
            var header = expander.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(button => button.Classes.Contains("PurposeHeader"));
            header.Focus();
            Assert.IsTrue(header.IsFocused);
            window.KeyPress(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Space, " ");
            window.KeyRelease(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Space, " ");
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(view.FindControl<Button>("AddPurposeModelButton")!.IsEffectivelyVisible);
            window.KeyPress(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Space, " ");
            window.KeyRelease(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Space, " ");
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(view.FindControl<Button>("AddPurposeModelButton")!.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void WhenEditingPurposeKeyThenDraftUpdates()
    {
        WithCustomRow((view, model, row) =>
        {
            var input = view.GetVisualDescendants().OfType<TextBox>().Single(control => control.DataContext == row && control.Classes.Contains("SettingsInput"));
            input.Text = "CustomReview";
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("CustomReview", row.PurposeKey);
        });
    }

    [TestMethod]
    public void WhenSelectingModelThenDraftUpdates()
    {
        WithCustomRow((view, model, row) =>
        {
            var selector = view.GetVisualDescendants().OfType<ComboBox>().Single(control => control.DataContext == row);
            var option = new PurposeModelOptionViewModel("test/model", "Test model", true);
            row.ModelOptions.Add(option);
            selector.SelectedItem = option;
            Dispatcher.UIThread.RunJobs();
            Assert.AreSame(option, row.SelectedModel);
        });
    }

    [TestMethod]
    public void WhenRemovingCustomRowThenParentCommandRemovesDraft()
    {
        WithCustomRow((view, model, row) =>
        {
            var remove = view.GetVisualDescendants().OfType<Button>().Single(control => control.DataContext == row && control.IsVisible);
            remove.Command!.Execute(remove.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(model.PurposeModels.Contains(row));
        });
    }

    [TestMethod]
    public void WhenWindowIsNarrowThenModelSelectorFitsRow()
    {
        WithCustomRow((view, model, row) =>
        {
            var selector = view.GetVisualDescendants().OfType<ComboBox>().Single(control => control.DataContext == row);
            Assert.IsTrue(selector.Bounds.Width > 100 && selector.Bounds.Right <= ((Grid)selector.Parent!).Bounds.Width);
        });
    }

    private static void WithCustomRow(Action<SettingsView, SettingsViewModel, PurposeModelSettingsViewModel> verify)
    {
        using var model = new SettingsViewModel();
        var view = new SettingsView { DataContext = model };
        var window = new Window { Content = view, Width = 720, Height = 1000 };
        try
        {
            window.Show();
            view.FindControl<Expander>("PurposeModelsExpander")!.IsExpanded = true;
            model.AddPurposeModelCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            verify(view, model, model.PurposeModels.Last());
        }
        finally { window.Close(); }
    }
}
