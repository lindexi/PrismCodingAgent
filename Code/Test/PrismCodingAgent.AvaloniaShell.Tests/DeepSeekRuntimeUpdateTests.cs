using System.Net;
using System.Text.Json;
using AgentLib;
using AgentLib.Coding;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.Abilities;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.Services.WorkTasks;
using PrismCodingAgent.CodingShell.ViewModels;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class DeepSeekRuntimeUpdateTests
{
    [TestMethod]
    public async Task SavingSettingsShouldUpdateNewFunctionsAcrossExistingRuntimes()
    {
        var paths = CreatePaths();
        using var firstHttp = new HttpClient(new OfflineHandler());
        using var secondHttp = new HttpClient(new OfflineHandler());
        var dispatcher = new Dispatcher();
        var first = await CodingWorkTaskRuntimeFactory.InitializeAsync(paths, dispatcher);
        var second = await CodingWorkTaskRuntimeFactory.InitializeAsync(paths, dispatcher);
        var abilities = new AbilityCatalog(paths.AbilitiesDirectory);
        var firstTask = MainViewModel.CreateRuntimeTask(first, new WorkTaskRecord(Guid.NewGuid(), "First", null, null, null), abilities);
        var secondTask = MainViewModel.CreateRuntimeTask(second, new WorkTaskRecord(Guid.NewGuid(), "Second", null, null, null), abilities);
        await using var main = MainViewModel.Create([firstTask, secondTask], [], first.SettingsService,
            new WorkTaskStore(paths), () => CodingWorkTaskRuntimeFactory.InitializeAsync(paths, dispatcher), abilities, paths);
        await main.InitializeSettingsAsync();
        var settings = main.SettingsViewModel ?? throw new InvalidOperationException();
        AIFunction[] RegisterFunctions() => [Search(first.SettingsService, firstHttp), Search(second.SettingsService, secondHttp)];
        var originalFunctions = RegisterFunctions();
        var arguments = new AIFunctionArguments { ["query"] = "offline" };
        foreach (var function in originalFunctions)
        {
            StringAssert.Contains(Text(await function.InvokeAsync(arguments)), "尚未配置");
        }

        settings.LanguageTag = Lang.Current.IetfLanguageTag;
        settings.DeepSeekApiKey = "saved-test-key";
        await ((SimpleAsyncCommand)settings.SaveCommand).ExecuteAsync();

        foreach (var function in RegisterFunctions())
        {
            Assert.AreEqual("{\"results\":[]}", Text(await function.InvokeAsync(arguments)));
        }
        foreach (var function in originalFunctions)
        {
            StringAssert.Contains(Text(await function.InvokeAsync(arguments)), "尚未配置");
        }
        settings.DeepSeekApiKey = null;
        await ((SimpleAsyncCommand)settings.SaveCommand).ExecuteAsync();
        foreach (var function in RegisterFunctions())
        {
            StringAssert.Contains(Text(await function.InvokeAsync(arguments)), "尚未配置");
        }
    }

    [TestMethod]
    public async Task SavingServiceShouldUpdateNewFunctionsWithoutShellEvent()
    {
        var paths = CreatePaths();
        using var http = new HttpClient(new OfflineHandler());
        var service = new CodingChatSettingsService(paths);
        var arguments = new AIFunctionArguments { ["query"] = "offline" };
        async Task<string?> SearchNewRunAsync() => Text(await Search(service, http).InvokeAsync(arguments));
        StringAssert.Contains(await SearchNewRunAsync(), "尚未配置");

        await service.SaveAsync(new AgentApiManagerConfiguration(), new PrismCodingAgentShellSettings { DeepSeekApiKey = "saved-test-key", LanguageTag = Lang.Current.IetfLanguageTag });
        Assert.AreEqual("{\"results\":[]}", await SearchNewRunAsync());
        await service.SaveAsync(new AgentApiManagerConfiguration
        {
            OpenAIConfigurationList = [new OpenAIProtocolLanguageModelConfiguration("https://api.deepseek.com/v1", "saved-test-key")],
        }, new PrismCodingAgentShellSettings { LanguageTag = Lang.Current.IetfLanguageTag });
        Assert.AreEqual("{\"results\":[]}", await SearchNewRunAsync());
        await service.SaveAsync(new AgentApiManagerConfiguration(), new PrismCodingAgentShellSettings { LanguageTag = Lang.Current.IetfLanguageTag });
        StringAssert.Contains(await SearchNewRunAsync(), "尚未配置");
    }

    private static CodingChatRoomPaths CreatePaths()
    {
        var paths = CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), "DeepSeekRuntimeTests", Guid.NewGuid().ToString("N")));
        Console.WriteLine(paths.RootDirectory);
        return paths;
    }

    private static AIFunction Search(CodingChatSettingsService service, HttpClient httpClient) =>
        DeepSeekWebToolSource.CreateToolRegistrations(
                ToolRegistrationTestContext.Create(httpClient: httpClient, settings: service.AppliedShellSettings))
            .Select(registration => (AIFunction)registration.Tool).Single(tool => tool.Name == "Search");

    private static string? Text(object? value) => Assert.IsInstanceOfType<JsonElement>(value).GetString();

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("saved-test-key", request.Headers.Authorization?.Parameter);
            Assert.AreEqual("https://api.deepseek.com/v1/tools/search", request.RequestUri?.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"results\":[]}") });
        }
    }

    private sealed class Dispatcher : IMainThreadDispatcher
    {
        public Task InvokeAsync(Func<Task> action) => action();
        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
        public bool CheckAccess() => true;
    }
}
