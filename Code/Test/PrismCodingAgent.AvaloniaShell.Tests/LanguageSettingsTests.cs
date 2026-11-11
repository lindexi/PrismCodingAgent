using PrismCodingAgent.CodingShell.Infrastructure;
using AgentLib.Coding.Sandboxes;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("en")]
public sealed class LanguageSettingsTests
{
    public static IEnumerable<object[]> Languages => Lang.SupportedLanguageTags.Select(tag => new object[] { tag });

    [TestMethod]
    public void WhenOpeningSettingsThenAllResourceLanguagesAreSelectable()
    {
        using var vm = new SettingsViewModel();
        CollectionAssert.AreEquivalent(Lang.SupportedLanguageTags.ToArray(), vm.LanguageTags);
    }

    [DataTestMethod]
    [DynamicData(nameof(Languages))]
    public async Task WhenSavingLanguageThenNewSettingsRestoreIt(string language)
    {
        string original = Lang.Current.IetfLanguageTag;
        try
        {
            var paths = CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            var source = new WindowsSandboxToolSource(false, string.Empty, string.Empty);
            using var vm = new SettingsViewModel(new CodingChatSettingsService(paths, source), () => { });
            vm.LanguageTag = language;
            await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
            using var saved = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(paths.ShellSettingsFile.FullName));
            Assert.AreEqual(language, saved.RootElement.GetProperty("LanguageTag").GetString());
            Assert.AreEqual(language, Lang.Current.IetfLanguageTag);
            var startupSettings = await new CodingChatSettingsService(paths, source).LoadInitialShellSettingsAsync();
            Assert.AreEqual(language, startupSettings.LanguageTag);
            using var restored = new SettingsViewModel(new CodingChatSettingsService(paths, source), () => { });
            await restored.LoadAsync();
            Assert.AreEqual(language, restored.LanguageTag);
        }
        finally
        {
            Lang.SetCurrent(original);
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t ")]
    [DataRow("legacy-invalid")]
    [DataRow("ar")]
    public async Task WhenLoadingLegacyUnsupportedLanguageThenChineseIsUsed(string? language)
    {
        var paths = CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ShellSettingsFile.FullName,
            System.Text.Json.JsonSerializer.Serialize(new { LanguageTag = language }));
        using var vm = new SettingsViewModel(new CodingChatSettingsService(paths,
            new WindowsSandboxToolSource(false, string.Empty, string.Empty)), () => { });
        await vm.LoadAsync();
        Assert.AreEqual("zh-Hans", vm.LanguageTag);
        var startupSettings = await new CodingChatSettingsService(paths,
            new WindowsSandboxToolSource(false, string.Empty, string.Empty)).LoadInitialShellSettingsAsync();
        Assert.AreEqual("zh-Hans", startupSettings.LanguageTag);
    }

    [DataTestMethod]
    [DataRow("EN", "en")]
    [DataRow("pt-br", "pt-BR")]
    [DataRow("ZH-hANT", "zh-Hant")]
    [DataRow("legacy-invalid", "zh-Hans")]
    public void WhenSelectingLanguageThenCanonicalSupportedTagIsUsed(string language, string expected)
    {
        using var vm = new SettingsViewModel();
        vm.LanguageTag = language;
        Assert.AreEqual(expected, vm.LanguageTag);
    }
}
