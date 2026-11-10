using AgentLib.Coding;
using AgentLib.Coding.Sandboxes;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class PurposeModelSettingsTests
{
    [TestMethod]
    public void BuiltInPurposesShouldUseStableKeys()
    {
        using var vm = new SettingsViewModel();
        CollectionAssert.AreEqual(new[] { "DocumentGeneration", "ImageAnalysis", "UserCommunication", "Reasoning", "CodeDesign", "CodeReview", "CodeExploration", "SessionTitle", "HistoryCompression" }, vm.PurposeModels.Select(row => row.PurposeKey).ToArray());
    }

    [TestMethod]
    public void BuiltInKeyShouldBeReadOnly()
    {
        using var vm = new SettingsViewModel();
        vm.PurposeModels[0].PurposeKey = "Changed";
        Assert.AreEqual("DocumentGeneration", vm.PurposeModels[0].PurposeKey);
    }

    [TestMethod]
    public void BuiltInPurposeShouldNotBeRemovable()
    {
        using var vm = new SettingsViewModel();
        Assert.IsFalse(vm.RemovePurposeModelCommand.CanExecute(vm.PurposeModels[0]));
    }

    [TestMethod]
    public void NewlyAddedPurposeShouldBeEmptyAndInvalid()
    {
        using var vm = new SettingsViewModel();
        vm.AddPurposeModelCommand.Execute(null);
        Assert.AreEqual(new { Key = string.Empty, Invalid = true }, new { Key = vm.PurposeModels.Last().PurposeKey, Invalid = vm.PurposeModels.Last().PurposeError is not null });
    }

    [TestMethod]
    public async Task EmptyPurposeShouldBlockSaveAndKeepPersistedMapping()
    {
        var service = CreateService();
        using var vm = new SettingsViewModel(service, () => { });
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        vm.PurposeModels[0].SelectedModel = vm.PurposeModels[0].ModelOptions.Last();
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        vm.AddPurposeModelCommand.Execute(null);
        vm.PurposeModels[0].SelectedModel = vm.PurposeModels[0].ModelOptions.First();
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        Assert.AreEqual("Provider/Model", (await service.LoadAsync()).ModelConfiguration!.ModelsByPurpose!["DocumentGeneration"]);
    }

    [TestMethod]
    public void CustomPurposeShouldBeRemovable()
    {
        using var vm = new SettingsViewModel();
        vm.AddPurposeModelCommand.Execute(null);
        vm.RemovePurposeModelCommand.Execute(vm.PurposeModels.Last());
        Assert.HasCount(9, vm.PurposeModels);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("DocumentGeneration")]
    [DataRow(" DocumentGeneration ")]
    public void InvalidPurposeShouldHaveInlineError(string key)
    {
        using var vm = new SettingsViewModel();
        vm.AddPurposeModelCommand.Execute(null);
        var row = vm.PurposeModels.Last();
        row.PurposeKey = key;
        Assert.IsNotNull(row.PurposeError);
    }

    [TestMethod]
    public void ResolvingDuplicateShouldClearBothErrors()
    {
        using var vm = new SettingsViewModel();
        vm.AddPurposeModelCommand.Execute(null);
        vm.PurposeModels.Last().PurposeKey = "CustomPurpose";
        vm.AddPurposeModelCommand.Execute(null);
        vm.PurposeModels.Last().PurposeKey = "AnotherPurpose";
        Assert.IsTrue(vm.PurposeModels.All(row => !row.HasErrors));
    }

    [TestMethod]
    public void ModelOptionShouldDisplayProviderAndName()
    {
        using var vm = new SettingsViewModel();
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        Assert.AreEqual("Provider/Model", vm.PurposeModels[0].ModelOptions.Last().DisplayName);
    }

    [TestMethod]
    public void DeletedModelShouldRetainUnavailableSelection()
    {
        using var vm = new SettingsViewModel();
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        var row = vm.PurposeModels[0];
        row.SelectedModel = row.ModelOptions.Last();
        vm.Providers.Clear();
        Assert.AreEqual(new { ModelReference = (string?)"Provider/Model", IsAvailable = false, HasError = true }, new { row.SelectedModel!.ModelReference, row.SelectedModel.IsAvailable, HasError = row.ModelError is not null });
    }

    [TestMethod]
    public void RestoredModelShouldClearError()
    {
        using var vm = new SettingsViewModel();
        var model = new ModelSettingsViewModel { Provider = "Provider", ModelName = "Model" };
        vm.Providers[0].Models.Add(model);
        var row = vm.PurposeModels[0];
        row.SelectedModel = row.ModelOptions.Last();
        model.ModelName = "Renamed";
        model.ModelName = "Model";
        Assert.IsFalse(row.HasErrors);
    }

    [TestMethod]
    public async Task UnspecifiedShouldNotSaveMappingOrUsePrimaryModel()
    {
        var service = CreateService();
        using var vm = new SettingsViewModel(service, () => { });
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        vm.PrimaryModel = "Provider/Model";
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        Assert.IsEmpty((await service.LoadAsync()).ModelConfiguration!.ModelsByPurpose!);
    }

    [TestMethod]
    public async Task DraftShouldNotMutateSavedMapping()
    {
        var service = CreateService();
        using var vm = new SettingsViewModel(service, () => { });
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        var row = vm.PurposeModels[0];
        row.SelectedModel = row.ModelOptions.Last();
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        row.SelectedModel = row.ModelOptions.First();
        Assert.AreEqual("Provider/Model", (await service.LoadAsync()).ModelConfiguration!.ModelsByPurpose!["DocumentGeneration"]);
    }

    [TestMethod]
    public async Task InvalidModelShouldBlockSaveAndKeepPersistedMapping()
    {
        var service = CreateService();
        using var vm = new SettingsViewModel(service, () => { });
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        vm.PurposeModels[0].SelectedModel = vm.PurposeModels[0].ModelOptions.Last();
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        vm.Providers.Clear();
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        Assert.AreEqual("Provider/Model", (await service.LoadAsync()).ModelConfiguration!.ModelsByPurpose!["DocumentGeneration"]);
    }

    [TestMethod]
    public async Task SavedCustomPurposeShouldReload()
    {
        var service = CreateService();
        using var vm = new SettingsViewModel(service, () => { });
        vm.Providers[0].Models.Add(new() { Provider = "Provider", ModelName = "Model" });
        vm.AddPurposeModelCommand.Execute(null);
        vm.PurposeModels.Last().PurposeKey = "CustomAnalysis";
        vm.PurposeModels.Last().SelectedModel = vm.PurposeModels.Last().ModelOptions.Last();
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        using var reloaded = new SettingsViewModel(service, () => { });
        await reloaded.LoadAsync();
        Assert.AreEqual("Provider/Model", reloaded.PurposeModels.Single(row => row.PurposeKey == "CustomAnalysis").SelectedModel!.ModelReference);
    }

    [DataTestMethod]
    [DataRow(" ")]
    [DataRow("DocumentGeneration")]
    public async Task InvalidPurposeShouldBlockSave(string key)
    {
        var service = CreateService();
        using var vm = new SettingsViewModel(service, () => { });
        vm.AddPurposeModelCommand.Execute(null);
        vm.PurposeModels.Last().PurposeKey = key;
        await ((SimpleAsyncCommand)vm.SaveCommand).ExecuteAsync();
        Assert.IsTrue(vm.IsStatusError);
    }

    [TestMethod]
    public async Task MissingPersistedModelShouldRemainVisibleAfterLoad()
    {
        var service = CreateService();
        await service.SaveAsync(new AgentApiManagerConfiguration
        {
            ModelsByPurpose = new Dictionary<string, string> { ["Reasoning"] = "Deleted/Model" },
        }, new CodingChatShellSettings());
        using var vm = new SettingsViewModel(service, () => { });
        await vm.LoadAsync();
        var row = vm.PurposeModels.Single(row => row.PurposeKey == "Reasoning");
        Assert.AreEqual(new { ModelReference = (string?)"Deleted/Model", IsAvailable = false, HasErrors = true },
            new { row.SelectedModel!.ModelReference, row.SelectedModel.IsAvailable, row.HasErrors });
    }

    private static CodingChatSettingsService CreateService() => new(
        CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
        new WindowsSandboxToolSource(false, string.Empty, string.Empty));
}
