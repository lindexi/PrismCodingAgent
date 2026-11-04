using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Localizations;
using SettingsView = PrismCodingAgent.CodingShell.Views.SettingsView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过离线输入取得代理和沙盒配置的真实双语参考图，不触发连接测试。</summary>
[AvaloniaTestClass]
public sealed class ShellGeneralSettingsScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GeneralSettingsReferenceThroughUserInputAsync()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        await session.ClickAndWaitAsync(session.Window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SettingsButton"));
        var settings = session.Window.GetVisualDescendants().OfType<SettingsView>().Single();
        var scroll = settings.GetVisualDescendants().OfType<ScrollViewer>().First();
        var toggles = settings.GetVisualDescendants().OfType<ToggleSwitch>().ToArray();
        session.Capture("language-default");
        void ScrollTo(Control control)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                session.Window.UpdateLayout();
                var position = control.TranslatePoint(default, scroll)!.Value;
                if (position.Y >= 0 && position.Y + control.Bounds.Height <= scroll.Bounds.Height) return;
                var point = scroll.TranslatePoint(new Point(scroll.Bounds.Width - 30, scroll.Bounds.Height / 2), session.Window)!.Value;
                session.Window.MouseMove(point);
                session.Window.MouseWheel(point, new Vector(0, position.Y < 0 ? 3 : -3));
                Dispatcher.UIThread.RunJobs();
            }
            Assert.Fail("Could not scroll to reference control.");
        }
        ScrollTo(toggles[1]);
        Assert.IsFalse(toggles[1].IsChecked);
        session.Capture("proxy-disabled");
        session.Click(toggles[1]);
        Dispatcher.UIThread.RunJobs();
        var proxy = settings.GetVisualDescendants().OfType<TextBox>().Single(t => t.Watermark?.ToString()?.Contains("7890") == true);
        ScrollTo(proxy);
        session.Click(proxy);
        session.Window.KeyTextInput("http://127.0.0.1:18888");
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual("http://127.0.0.1:18888", session.Model.SettingsViewModel!.NetworkProxyAddress);
        var bypass = settings.GetVisualDescendants().OfType<CheckBox>().Single();
        ScrollTo(bypass);
        session.Click(bypass);
        Assert.IsFalse(bypass.IsChecked);
        var save = settings.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("SaveSettings"));
        await session.ClickAndWaitAsync(save);
        session.Capture("proxy-enabled");
        session.Click(toggles[1]);
        await session.ClickAndWaitAsync(save);
        Assert.IsFalse(proxy.IsEffectivelyEnabled);
        Assert.AreEqual("http://127.0.0.1:18888", proxy.Text);
        session.Capture("proxy-disabled-retained");
        ScrollTo(toggles[0]);
        session.Capture("sandbox-config");
        var bottom = scroll.TranslatePoint(new Point(scroll.Bounds.Width - 30, scroll.Bounds.Height / 2), session.Window)!.Value;
        var lsp = settings.GetVisualDescendants().OfType<Button>().Single(b => b.Command is null && b.Classes.Contains("accent") && b.Content?.ToString() == Lang.Current.InstallOrUpdateLanguageServerText);
        ScrollTo(lsp);
        session.Window.MouseMove(bottom);
        session.Window.MouseWheel(bottom, new Vector(0, -3));
        Dispatcher.UIThread.RunJobs();
        session.Capture("lsp-bottom-complete");
        for (int step = 0; step < 20; step++)
        {
            session.Window.MouseMove(bottom);
            session.Window.MouseWheel(bottom, new Vector(0, -3));
            Dispatcher.UIThread.RunJobs();
        }
        session.Capture("instructions-models-bottom");
    }
}
