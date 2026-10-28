using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Infrastructure;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实设置页输入验证本地代理选项保存、重进保留及恢复默认。</summary>
[AvaloniaTestClass]
public sealed class ShellOptionsScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProxyOptionsShouldPersistAndReturnToDefaultsThroughUserInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        var navigation = session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "SettingsButton");
        await session.ClickAndWaitAsync(navigation);
        var settings = session.Window.GetVisualDescendants().OfType<SettingsView>().Single();
        Assert.IsInstanceOfType<SettingsViewModel>(settings.DataContext);
        var scroll = settings.GetVisualDescendants().OfType<ScrollViewer>().First();
        var save = settings.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("SaveSettings"));
        var back = settings.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("SettingsBack"));
        var paths = CodingChatRoomPaths.Create(Path.Combine(session.DirectoryPath, "AppData"));
        const string proxyAddress = "http://127.0.0.1:18765";

        Border ProxyCard() => settings.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Text == "NET").GetVisualAncestors().OfType<Border>()
            .First(border => border.Classes.Contains("SettingsCard"));

        void ScrollTo(Control control)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                session.Window.UpdateLayout();
                Point position = control.TranslatePoint(default, scroll)
                    ?? throw new InvalidOperationException("Settings control is not attached.");
                if (position.Y >= 0 && position.Y + control.Bounds.Height <= scroll.Bounds.Height)
                {
                    return;
                }

                Point wheelPoint = scroll.TranslatePoint(new Point(scroll.Bounds.Width - 30,
                    scroll.Bounds.Height / 2), session.Window)
                    ?? throw new InvalidOperationException("Settings scroll viewer is not attached.");
                session.Window.MouseMove(wheelPoint);
                session.Window.MouseWheel(wheelPoint, new Vector(0, position.Y < 0 ? 3 : -3));
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Fail("Could not scroll the settings control into view using the mouse wheel.");
        }

        void ReplaceText(TextBox input, string value)
        {
            ScrollTo(input);
            session.Click(input);
            session.Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            session.Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
            session.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            session.Window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            session.Window.KeyTextInput(value);
            Dispatcher.UIThread.RunJobs();
        }

        async Task AssertPersistedAsync(bool enabled, string? address)
        {
            using var persisted = JsonDocument.Parse(await File.ReadAllTextAsync(paths.ShellSettingsFile.FullName));
            Assert.AreEqual(enabled, persisted.RootElement.GetProperty("IsNetworkProxyEnabled").GetBoolean());
            Assert.AreEqual(address, persisted.RootElement.GetProperty("NetworkProxyAddress").GetString());
            Assert.IsTrue(persisted.RootElement.GetProperty("BypassProxyOnLocal").GetBoolean());
        }

        async Task ReenterAsync()
        {
            session.Click(back);
            Assert.IsFalse(settings.IsEffectivelyVisible);
            session.Capture("main-after-leaving-settings");
            await session.ClickAndWaitAsync(navigation);
            settings = session.Window.GetVisualDescendants().OfType<SettingsView>().Single();
            scroll = settings.GetVisualDescendants().OfType<ScrollViewer>().First();
            save = settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SaveSettings"));
            back = settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SettingsBack"));
            Assert.IsTrue(settings.IsEffectivelyVisible);
        }

        var card = ProxyCard();
        var toggle = card.GetVisualDescendants().OfType<ToggleSwitch>().Single();
        var address = card.GetVisualDescendants().OfType<TextBox>().Single();
        ScrollTo(card);
        session.Capture("proxy-default-disabled");
        Assert.IsFalse(toggle.IsChecked);
        Assert.IsFalse(address.IsEffectivelyEnabled);
        Assert.IsTrue(string.IsNullOrEmpty(address.Text));

        session.Click(toggle);
        Assert.IsTrue(address.IsEffectivelyEnabled);
        ReplaceText(address, proxyAddress);
        session.Capture("proxy-edited-before-save");
        Assert.AreEqual(proxyAddress, address.Text);
        await session.ClickAndWaitAsync(save);
        session.Capture("proxy-saved");
        await AssertPersistedAsync(true, proxyAddress);

        await ReenterAsync();
        card = ProxyCard();
        toggle = card.GetVisualDescendants().OfType<ToggleSwitch>().Single();
        address = card.GetVisualDescendants().OfType<TextBox>().Single();
        ScrollTo(card);
        session.Capture("proxy-reentered-retained");
        Assert.IsTrue(toggle.IsChecked);
        Assert.IsTrue(address.IsEffectivelyEnabled);
        Assert.AreEqual(proxyAddress, address.Text);

        ReplaceText(address, string.Empty);
        ScrollTo(toggle);
        session.Click(toggle);
        Assert.IsFalse(toggle.IsChecked);
        Assert.IsFalse(address.IsEffectivelyEnabled);
        await session.ClickAndWaitAsync(save);
        session.Capture("proxy-defaults-restored-saved");
        await AssertPersistedAsync(false, null);

        await ReenterAsync();
        card = ProxyCard();
        ScrollTo(card);
        session.Capture("proxy-reentered-defaults");
        Assert.IsFalse(card.GetVisualDescendants().OfType<ToggleSwitch>().Single().IsChecked);
        address = card.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.IsFalse(address.IsEffectivelyEnabled);
        Assert.IsTrue(string.IsNullOrEmpty(address.Text));
        Assert.IsTrue(card.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked);
    }
}
