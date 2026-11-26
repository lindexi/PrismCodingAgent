using System.Globalization;
using PrismCodingAgent.CodingShell.Infrastructure;
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
            using var vm = new SettingsViewModel(new CodingChatSettingsService(paths), () => { });
            vm.LanguageTag = language;
            await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
            using var saved = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(paths.ShellSettingsFile.FullName));
            Assert.AreEqual(language, saved.RootElement.GetProperty("LanguageTag").GetString());
            Assert.AreEqual(language, Lang.Current.IetfLanguageTag);
            var startupSettings = await new CodingChatSettingsService(paths).LoadInitialShellSettingsAsync();
            Assert.AreEqual(language, startupSettings.LanguageTag);
            using var restored = new SettingsViewModel(new CodingChatSettingsService(paths), () => { });
            await restored.LoadAsync();
            Assert.AreEqual(language, restored.LanguageTag);
        }
        finally
        {
            Lang.SetCurrent(original);
        }
    }

    [TestMethod]
    [DataRow(null, "en-US", "en")]
    [DataRow(null, "fr-FR", "fr")]
    [DataRow(null, "ar-SA", "zh-Hans")]
    [DataRow("", "en-US", "zh-Hans")]
    [DataRow(" \t ", "en-US", "zh-Hans")]
    [DataRow("legacy-invalid", "en-US", "zh-Hans")]
    [DataRow("ar", "en-US", "zh-Hans")]
    public async Task WhenLoadingLegacyLanguageThenExpectedFallbackIsUsed(string? language, string cultureName, string expected)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var paths = CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            paths.EnsureDirectories();
            await File.WriteAllTextAsync(paths.ShellSettingsFile.FullName,
                System.Text.Json.JsonSerializer.Serialize(new { LanguageTag = language }));
            var service = new CodingChatSettingsService(paths);
            using var vm = new SettingsViewModel(service, () => { });

            await vm.LoadAsync();
            var startupSettings = await service.LoadInitialShellSettingsAsync();

            Assert.AreEqual((expected, expected), (vm.LanguageTag, startupSettings.LanguageTag));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
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
