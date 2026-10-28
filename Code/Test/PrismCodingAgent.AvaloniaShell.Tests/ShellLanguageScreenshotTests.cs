using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Infrastructure;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实设置页的鼠标键盘输入验证语言保存、界面更新和再次加载。</summary>
[AvaloniaTestClass]
public sealed class ShellLanguageScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LanguageShouldRoundTripThroughSettingsAndMainUsingUserInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        var navigation = session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "SettingsButton");
        string initialLanguage = Lang.Current.IetfLanguageTag;
        session.Capture($"main-initial-{initialLanguage}");
        Assert.AreEqual(Lang.Current.SettingsNavigationText,
            navigation.GetVisualDescendants().OfType<TextBlock>().Single().Text);

        var transitions = new[]
        {
            (From: "zh-Hans", To: "en", Save: "Save changes", Navigation: "Settings", Key: PhysicalKey.ArrowDown),
            (From: "en", To: "zh-Hans", Save: "保存更改", Navigation: "设置", Key: PhysicalKey.ArrowUp),
        };
        foreach (var transition in initialLanguage == "zh-Hans" ? transitions : transitions.Reverse())
        {
            await session.ClickAndWaitAsync(navigation);
            var settings = session.Window.GetVisualDescendants().OfType<SettingsView>().Single();
            Assert.IsTrue(settings.IsEffectivelyVisible);
            var selector = settings.GetVisualDescendants().OfType<ComboBox>()
                .Single(control => control.Name == "LanguageSelector");
            Assert.AreEqual(transition.From, selector.SelectedItem);
            var scroll = settings.GetVisualDescendants().OfType<ScrollViewer>().First();
            var save = settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SaveSettings"));
            var back = settings.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("SettingsBack"));

            for (int attempt = 0; attempt < 80; attempt++)
            {
                session.Window.UpdateLayout();
                Point position = selector.TranslatePoint(default, scroll)
                    ?? throw new InvalidOperationException("Language selector is not attached.");
                if (position.Y >= 0 && position.Y + selector.Bounds.Height <= scroll.Bounds.Height)
                {
                    break;
                }

                Point wheelPoint = scroll.TranslatePoint(new Point(scroll.Bounds.Width - 30,
                    scroll.Bounds.Height / 2), session.Window)
                    ?? throw new InvalidOperationException("Settings scroll viewer is not attached.");
                session.Window.MouseMove(wheelPoint);
                session.Window.MouseWheel(wheelPoint, new Vector(0, position.Y < 0 ? 3 : -3));
                Dispatcher.UIThread.RunJobs();
            }

            Point visiblePosition = selector.TranslatePoint(default, scroll)
                ?? throw new InvalidOperationException("Language selector is not attached.");
            Assert.IsTrue(visiblePosition.Y >= 0 &&
                visiblePosition.Y + selector.Bounds.Height <= scroll.Bounds.Height);
            session.Capture($"settings-opened-{transition.From}");
            session.Click(selector);
            Assert.IsTrue(selector.IsDropDownOpen);
            session.Window.KeyPressQwerty(transition.Key, RawInputModifiers.None);
            session.Window.KeyReleaseQwerty(transition.Key, RawInputModifiers.None);
            session.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            session.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            session.Capture($"language-selected-{transition.To}-before-save");
            Assert.AreEqual(transition.To, selector.SelectedItem);
            Assert.IsFalse(selector.IsDropDownOpen);

            await session.ClickAndWaitAsync(save);
            session.Capture($"settings-saved-{transition.To}");
            Assert.AreEqual(transition.Save, save.Content?.ToString());
            Assert.AreEqual(transition.Navigation, settings.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Classes.Contains("SettingsPageTitle")).Text);
            var paths = CodingChatRoomPaths.Create(Path.Combine(session.DirectoryPath, "AppData"));
            using (var persisted = JsonDocument.Parse(await File.ReadAllTextAsync(paths.ShellSettingsFile.FullName)))
            {
                Assert.AreEqual(transition.To, persisted.RootElement.GetProperty("LanguageTag").GetString());
            }

            session.Click(back);
            session.Capture($"main-{transition.To}");
            Assert.IsFalse(settings.IsEffectivelyVisible);
            Assert.AreEqual(transition.Navigation,
                navigation.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        }
    }
}
