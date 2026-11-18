using AgentLib.Coding.Sandboxes;
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
        using var settings = new SettingsViewModel(new CodingChatSettingsService(paths, sandbox), () => { });
        settings.IsAutomaticSessionTitleEnabled = false;
        await ((SimpleAsyncCommand)settings.SaveCommand).ExecuteAsync();

        using var restored = new SettingsViewModel(new CodingChatSettingsService(paths, sandbox), () => { });
        await restored.LoadAsync();

        Assert.IsFalse(restored.IsAutomaticSessionTitleEnabled);
    }

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
