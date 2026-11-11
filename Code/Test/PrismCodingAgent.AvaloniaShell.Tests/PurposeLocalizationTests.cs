using System.Text.RegularExpressions;
using AgentLib.Coding;
using AgentLib.Coding.Sandboxes;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass("en")]
public sealed class PurposeLocalizationTests
{
    public static IEnumerable<object[]> Languages => new[]
    {
        "zh-Hans", "en", "zh-Hant", "de", "es", "fr", "it", "ja", "ko", "nl", "pt-BR", "ru",
    }.Select(language => new object[] { language });

    [TestMethod]
    public void ResourceLanguagesShouldMatchGeneratedSupportedLanguages()
    {
        CollectionAssert.AreEquivalent(Languages.Select(row => (string)row[0]).ToArray(), Lang.SupportedLanguageTags.ToArray());
    }

    [DataTestMethod]
    [DynamicData(nameof(Languages))]
    public void ResourceKeysShouldMatchChinese(string language)
    {
        CollectionAssert.AreEquivalent(ReadResources("zh-Hans").Keys.ToArray(), ReadResources(language).Keys.ToArray());
    }

    [DataTestMethod]
    [DynamicData(nameof(Languages))]
    public void PurposePlaceholdersShouldMatchChinese(string language)
    {
        var expected = ReadResources("zh-Hans");
        foreach (var (key, value) in ReadResources(language).Where(pair => pair.Key.StartsWith("Purpose", StringComparison.Ordinal)))
        {
            CollectionAssert.AreEqual(Placeholders(expected[key]), Placeholders(value), $"{language}: {key}");
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(Languages))]
    public void PurposeResourcesShouldNotExposeTechnicalNamesOrEnglishPlaceholders(string language)
    {
        var english = ReadResources("en");
        // Model is also the correct Dutch word; this is not an untranslated placeholder.
        var untranslated = ReadResources(language)
            .Where(pair => pair.Key.StartsWith("Purpose", StringComparison.Ordinal))
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value)
                || pair.Value.Contains("CustomPurpose", StringComparison.Ordinal)
                || pair.Value.Contains("PascalCase", StringComparison.Ordinal)
                || pair.Value.Contains("Purpose key", StringComparison.Ordinal)
                || (language != "en" && !(language == "nl" && pair.Key == "PurposeModelLabel")
                    && pair.Value == english[pair.Key]))
            .Select(pair => pair.Key).ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), untranslated, language);
    }

    [DataTestMethod]
    [DynamicData(nameof(Languages))]
    public void ExistingPurposeRowsShouldRefreshAllDisplayTextWithoutChangingReferences(string language)
    {
        string original = Lang.Current.IetfLanguageTag;
        try
        {
            Lang.SetCurrent(language == "en" ? "zh-Hans" : "en");
            using var vm = new SettingsViewModel();
            vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
            var builtIn = vm.PurposeModels[0];
            builtIn.SelectedModel = builtIn.ModelOptions.Last();
            vm.Providers.Clear();
            vm.AddPurposeModelCommand.Execute(null);
            var empty = vm.PurposeModels.Last();
            vm.AddPurposeModelCommand.Execute(null);
            var duplicate = vm.PurposeModels.Last();
            duplicate.PurposeKey = " DocumentGeneration ";
            vm.AddPurposeModelCommand.Execute(null);
            var custom = vm.PurposeModels.Last();
            custom.PurposeKey = "我的用途 / My purpose";
            var changes = new List<string?>();
            builtIn.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            Lang.SetCurrent(language);

            var resources = ReadResources(language);
            CollectionAssert.AreEqual(new[]
            {
                resources["PurposeDocumentGeneration"], resources["PurposeUnspecified"],
                resources["PurposeUnavailable"].Replace("{0}", "Provider/Model", StringComparison.Ordinal),
                resources["PurposeModelInvalid"], resources["PurposeKeyRequired"], resources["PurposeKeyDuplicate"],
                "DocumentGeneration", "Provider/Model", "我的用途 / My purpose", bool.TrueString,
            }, new[]
            {
                builtIn.DisplayName, builtIn.ModelOptions[0].DisplayName, builtIn.SelectedModel!.DisplayName,
                builtIn.ModelError, empty.PurposeError, duplicate.PurposeError,
                builtIn.PurposeKey, builtIn.SelectedModel.ModelReference, custom.PurposeKey,
                changes.Contains(nameof(PurposeModelSettingsViewModel.DisplayName)).ToString(),
            });
        }
        finally
        {
            Lang.SetCurrent(original);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(Languages))]
    public async Task InvalidDraftShouldKeepSavedCustomPurposeAcrossLanguages(string language)
    {
        string original = Lang.Current.IetfLanguageTag;
        try
        {
            var service = new CodingChatSettingsService(
                CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
                new WindowsSandboxToolSource(false, string.Empty, string.Empty));
            using var vm = new SettingsViewModel(service, () => { });
            vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
            vm.AddPurposeModelCommand.Execute(null);
            var custom = vm.PurposeModels.Last();
            custom.PurposeKey = "  我的用途 / My purpose  ";
            custom.SelectedModel = custom.ModelOptions.Last();
            await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
            Lang.SetCurrent(language);
            vm.AddPurposeModelCommand.Execute(null);
            vm.PurposeModels.Last().PurposeKey = " \t ";
            custom.SelectedModel = custom.ModelOptions.First();
            await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
            using var reloaded = new SettingsViewModel(service, () => { });
            await reloaded.LoadAsync();

            Assert.AreEqual("Provider/Model", reloaded.PurposeModels.Single(row => row.PurposeKey == "我的用途 / My purpose").SelectedModel!.ModelReference);
        }
        finally
        {
            Lang.SetCurrent(original);
        }
    }

    private static Dictionary<string, string> ReadResources(string language)
    {
        using var stream = typeof(PurposeLocalizationTests).Assembly.GetManifestResourceStream($"PurposeResources.{language}.toml")
            ?? throw new InvalidOperationException($"Missing resource: {language}");
        using var reader = new StreamReader(stream);
        return Regex.Matches(reader.ReadToEnd(), "^([A-Za-z][A-Za-z0-9]*) = \"(.*)\"\\r?$", RegexOptions.Multiline)
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value, StringComparer.Ordinal);
    }

    private static string[] Placeholders(string value) => Regex.Matches(value, @"\{\d+\}")
        .Select(match => match.Value).Order(StringComparer.Ordinal).ToArray();
}
