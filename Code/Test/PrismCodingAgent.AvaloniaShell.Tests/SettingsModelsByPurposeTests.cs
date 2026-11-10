using AgentLib.Coding.Sandboxes;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.ViewModels;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class SettingsModelsByPurposeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WhenSavingOtherSettingsThenLoadedPurposeMappingsPersistAsync()
    {
        var service = CreateService();
        var mappings = new Dictionary<string, string>
        {
            ["summary"] = "test/model-a",
            ["custom-purpose"] = "other/model-b",
        };
        await service.SaveAsync(Configuration(mappings), ShellSettings());
        using var viewModel = new SettingsViewModel(service, static () => { });
        await viewModel.LoadAsync();
        viewModel.BypassProxyOnLocal = false;
        viewModel.Providers[0].ApiKey = "updated-key";

        await ((SimpleAsyncCommand)viewModel.SaveCommand).ExecuteAsync();
        Assert.IsFalse(viewModel.IsStatusError, viewModel.StatusMessage);
        using var reloaded = new SettingsViewModel(service, static () => { });
        await reloaded.LoadAsync();
        await ((SimpleAsyncCommand)reloaded.SaveCommand).ExecuteAsync();
        Assert.IsFalse(reloaded.IsStatusError, reloaded.StatusMessage);

        CollectionAssert.AreEquivalent(mappings.ToArray(),
            (await service.LoadAsync()).ModelConfiguration?.ModelsByPurpose?.ToArray());
    }

    [TestMethod]
    public async Task WhenSavingEmptyPurposeMappingsThenEmptyDictionaryPersistsAsync()
    {
        var service = CreateService();
        await service.SaveAsync(Configuration(new Dictionary<string, string>()), ShellSettings());
        using var viewModel = new SettingsViewModel(service, static () => { });
        await viewModel.LoadAsync();

        await ((SimpleAsyncCommand)viewModel.SaveCommand).ExecuteAsync();
        Assert.IsFalse(viewModel.IsStatusError, viewModel.StatusMessage);

        Assert.AreEqual(0, (await service.LoadAsync()).ModelConfiguration?.ModelsByPurpose?.Count);
    }

    [TestMethod]
    public async Task WhenSavingLegacyConfigurationThenPurposeMappingsRemainNullAsync()
    {
        var service = CreateService();
        await service.SaveAsync(Configuration(null), ShellSettings());
        using var viewModel = new SettingsViewModel(service, static () => { });
        await viewModel.LoadAsync();

        await ((SimpleAsyncCommand)viewModel.SaveCommand).ExecuteAsync();
        Assert.IsFalse(viewModel.IsStatusError, viewModel.StatusMessage);

        Assert.IsNull((await service.LoadAsync()).ModelConfiguration?.ModelsByPurpose);
    }

    [TestMethod]
    [DataRow("test/model-a")]
    [DataRow(null)]
    public async Task WhenReloadingConfigurationThenLatestPurposeMappingsReplacePreviousMappingsAsync(string? model)
    {
        var service = CreateService();
        await service.SaveAsync(Configuration(new Dictionary<string, string> { ["summary"] = "test/old-model" }), ShellSettings());
        using var viewModel = new SettingsViewModel(service, static () => { });
        await viewModel.LoadAsync();
        IReadOnlyDictionary<string, string>? mappings = model is null
            ? null
            : new Dictionary<string, string> { ["summary"] = model };
        await service.SaveAsync(Configuration(mappings), ShellSettings());
        await viewModel.LoadAsync();

        await ((SimpleAsyncCommand)viewModel.SaveCommand).ExecuteAsync();
        Assert.IsFalse(viewModel.IsStatusError, viewModel.StatusMessage);

        CollectionAssert.AreEquivalent(mappings?.ToArray(),
            (await service.LoadAsync()).ModelConfiguration?.ModelsByPurpose?.ToArray());
    }

    private CodingChatSettingsService CreateService()
    {
        string root = Path.Join(Path.GetTempPath(), "PrismPurposeMappingTests", Guid.NewGuid().ToString("N"));
        TestContext.WriteLine(root);
        return new CodingChatSettingsService(CodingChatRoomPaths.Create(root),
            new WindowsSandboxToolSource(false, string.Empty, string.Empty));
    }

    private static CodingChatShellSettings ShellSettings() => new() { LanguageTag = Lang.Current.IetfLanguageTag };

    private static AgentApiManagerConfiguration Configuration(IReadOnlyDictionary<string, string>? mappings) => new()
    {
        PrimaryModel = "test/model-a",
        ModelsByPurpose = mappings,
        OpenAIConfigurationList = [new OpenAIProtocolLanguageModelConfiguration("https://purpose-mapping.invalid/v1", "test-key")
        {
            ModelDefinitions =
            [
                new ModelDefinition { Provider = "test", ModelName = "model-a" },
                new ModelDefinition { Provider = "test", ModelName = "old-model" },
                new ModelDefinition { Provider = "other", ModelName = "model-b" },
            ],
        }],
    };
}
