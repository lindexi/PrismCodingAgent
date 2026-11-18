using AgentLib.Coding.Sandboxes;
using Avalonia.Controls;
using PrismCodingAgent.CodingShell.Views;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("en")]
public sealed class AutomaticSessionTitleSettingsTests
{
    [TestMethod]
    public async Task WhenSavingDisabledThenNewSettingsRestoreIt()
    {
        var paths = CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var sandbox = new WindowsSandboxToolSource(false, string.Empty, string.Empty);
        var service = new CodingChatSettingsService(paths, sandbox);
        using var settings = new SettingsViewModel(service, IgnoreNavigation);
        await settings.LoadAsync();
        settings.ShellSettings.IsAutomaticSessionTitleEnabled = false;
        Assert.IsTrue(service.AppliedShellSettings.IsAutomaticSessionTitleEnabled);
        await ((SimpleAsyncCommand)settings.SaveCommand).ExecuteAsync();

        using var restored = new SettingsViewModel(new CodingChatSettingsService(paths, sandbox), IgnoreNavigation);
        await restored.LoadAsync();

        Assert.IsFalse(restored.ShellSettings.IsAutomaticSessionTitleEnabled);
        Assert.IsFalse(service.AppliedShellSettings.IsAutomaticSessionTitleEnabled);
    }

    [TestMethod]
    public void WhenTogglingSwitchThenTheLoadedSettingsObjectIsEdited()
    {
        using var settings = new SettingsViewModel();
        var view = new SettingsView { DataContext = settings };
        var toggle = view.FindControl<ToggleSwitch>("AutomaticSessionTitleToggle")!;
        Assert.IsTrue(toggle.IsChecked);

        toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, false);

        Assert.IsFalse(settings.ShellSettings.IsAutomaticSessionTitleEnabled);
    }

    private static void IgnoreNavigation() { }

    [TestMethod]
    public async Task WhenLegacySettingsOmitOptionThenAutomaticTitlesRemainEnabled()
    {
        var paths = CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ShellSettingsFile.FullName, "{}");
        var service = new CodingChatSettingsService(paths, new WindowsSandboxToolSource(false, string.Empty, string.Empty));

        var settings = await service.LoadInitialShellSettingsAsync();

        Assert.IsTrue(settings.IsAutomaticSessionTitleEnabled);
    }
}
