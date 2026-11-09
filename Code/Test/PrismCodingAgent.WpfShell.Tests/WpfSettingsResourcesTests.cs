using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Input;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfSettingsResourcesTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GlobalSettingsRenderSharedControlsAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var window = new Window { Content = new SettingsView { DataContext = new PrismCodingAgent.CodingShell.ViewModels.SettingsViewModel() }, Width = 1200, Height = 900 };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            await screenshots.CaptureAsync("shared-global-settings");
        }
        finally { window.Close(); }
    });
    [TestMethod]
    public async Task SettingsShareComboBoxAndScrollBarResourcesAsync() => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var chat = new ChatSettingsView();
        var general = new SettingsView();
        var combo = new ComboBox { Style = (Style)general.FindResource(typeof(ComboBox)) };
        var chatCombo = new ComboBox { Style = (Style)chat.FindResource(typeof(ComboBox)) };
        Assert.AreSame(combo.Template, chatCombo.Template);
        Assert.AreSame(chat.FindResource("SettingsScrollBar"), general.FindResource("SettingsScrollBar"));
        return Task.CompletedTask;
    });

    [TestMethod]
    public async Task ComboBoxOpensSelectsCentersAndSupportsEditingAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var combo = new ComboBox { Width = 240, Height = 32, ItemsSource = new[] { "First", new string('L', 120), "Third" }, SelectedIndex = 0 };
        var window = new Window { Content = combo, Width = 320, Height = 160 };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var presenter = (ContentPresenter)combo.Template.FindName("SelectionPresenter", combo);
            Assert.AreEqual(VerticalAlignment.Center, presenter.VerticalAlignment);
            Assert.AreEqual((combo.ActualHeight - presenter.ActualHeight) / 2, presenter.TranslatePoint(default, combo).Y, 1);
            window.Activate();
            combo.Focus();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Down) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual(1, combo.SelectedIndex);
            combo.IsDropDownOpen = true;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            Assert.IsTrue(popup.IsOpen);
            Assert.AreEqual(SystemParameters.ComboBoxPopupAnimation, popup.PopupAnimation);
            await screenshots.CaptureAsync("shared-combo-open");
            var item = (ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(1);
            item.IsSelected = true;
            combo.IsDropDownOpen = false;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(1, combo.SelectedIndex);
            Assert.AreEqual(combo.SelectedItem, presenter.Content);
            combo.Focus();
            Assert.IsTrue(combo.IsKeyboardFocusWithin);
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Down) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual(2, combo.SelectedIndex, "Keyboard navigation must still work after closing the popup.");
            combo.IsEditable = true;
            window.UpdateLayout();
            var editor = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
            Assert.IsTrue(editor.IsVisible);
            editor.Text = "Edited";
            Assert.AreEqual("Edited", combo.Text);
            combo.IsEnabled = false;
            Assert.IsFalse(editor.IsEnabled);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ComboBoxRepeatedNavigationMatchesDefaultAsync(bool useDefault) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var combo = new ComboBox { ItemsSource = new[] { "First", "Second", "Third" }, SelectedIndex = 0 };
        if (useDefault) combo.Style = null;
        var window = new Window { Content = combo, Width = 320, Height = 160 };
        window.Show();
        try
        {
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);

            window.Activate();
            combo.Focus();
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Down) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual(1, combo.SelectedIndex);
            combo.IsDropDownOpen = true;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            combo.IsDropDownOpen = false;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Down) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual(2, combo.SelectedIndex, "Keyboard navigation must still work after closing the popup.");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    [DataRow(Orientation.Vertical)]
    [DataRow(Orientation.Horizontal)]
    public async Task ScrollBarTrackUsesRequestedDirectionAsync(Orientation orientation) => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var bar = new ScrollBar { Orientation = orientation, Minimum = 0, Maximum = 100, Value = 25, ViewportSize = 10 };
        var window = new Window { Content = bar, Width = 320, Height = 240 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var track = (Track)bar.Template.FindName("PART_Track", bar);
            Assert.AreEqual(orientation, track.Orientation);
            Assert.AreEqual(orientation == Orientation.Vertical, track.IsDirectionReversed);
            Assert.IsNotNull(track.Thumb.Template);
            bar.Value = 65;
            Assert.AreEqual(65d, track.Value);
            track.Thumb.RaiseEvent(new DragDeltaEventArgs(10, 10) { RoutedEvent = Thumb.DragDeltaEvent });
            Assert.IsGreaterThan(65d, bar.Value, "Dragging toward the increasing end must update the scroll value.");
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public async Task SecondaryButtonRetainsSharedTemplateAndDisabledStateAsync() => await WpfTestClassAttribute.RunUiAsync(() =>
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("ShellSecondaryButton"), Content = "Action" };
        var primary = new Button { Style = (Style)Application.Current.FindResource("PrimaryButton") };
        var window = new Window { Content = button, Width = 320, Height = 160 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.AreSame(primary.Template, button.Template);
            button.Focus();
            Assert.IsTrue(button.IsKeyboardFocused);
            var focusedSurface = (Border)button.Template.FindName("Surface", button);
            Assert.AreEqual(Application.Current.FindResource("PrimaryBrush"), focusedSurface.BorderBrush);
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.IsTrue(button.IsPressed);
            Assert.AreEqual(0.65, focusedSurface.Opacity);
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
            button.IsEnabled = false;
            var surface = (Border)button.Template.FindName("Surface", button);
            Assert.AreEqual(0.45, surface.Opacity);
            Assert.AreNotEqual(primary.Background, button.Background);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });
}
