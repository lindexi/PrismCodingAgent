using System.Text.Json;

using AgentLib.Core;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;

namespace AgentLib.Tests.Core;

[TestClass]
public class ModelPurposeTests
{
    [DataTestMethod]
    [DataRow(ModelPurposes.DocumentGeneration, "DocumentGeneration")]
    [DataRow(ModelPurposes.ImageAnalysis, "ImageAnalysis")]
    [DataRow(ModelPurposes.UserCommunication, "UserCommunication")]
    [DataRow(ModelPurposes.Reasoning, "Reasoning")]
    [DataRow(ModelPurposes.SessionTitle, "SessionTitle")]
    [DataRow(ModelPurposes.HistoryCompression, "HistoryCompression")]
    public void ModelPurposes_UseStablePersistedIdentifiers(string actual, string expected)
    {
        Assert.AreEqual(expected, actual);
    }

    [DataTestMethod]
    [DataRow("future/shared")]
    [DataRow("future\\shared")]
    [DataRow("future-id")]
    public void LoadConfiguration_WhenModelIsUnknown_StoresOriginalSpecifier(string specifier)
    {
        var manager = new AgentApiEndpointManager();

        manager.LoadConfiguration(CreateConfiguration("review", specifier));

        Assert.AreEqual(specifier, manager.ModelsByPurpose["review"]);
    }

    [DataTestMethod]
    [DataRow("future/shared")]
    [DataRow("future\\shared")]
    [DataRow("future-id")]
    public void SetModelForPurpose_WhenModelIsUnknown_StoresOriginalSpecifier(string specifier)
    {
        var manager = new AgentApiEndpointManager();

        manager.SetModelForPurpose("review", specifier);

        Assert.AreEqual(specifier, manager.ModelsByPurpose["review"]);
    }

    [DataTestMethod]
    [DataRow("future/shared")]
    [DataRow("future\\shared")]
    [DataRow("future-id")]
    public void ReplaceModelsByPurpose_WhenModelIsUnknown_ReplacesMappings(string specifier)
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("old", "secondary");

        manager.ReplaceModelsByPurpose(CreateConfiguration("review", specifier).ModelsByPurpose);

        CollectionAssert.AreEqual(new[] { "review", specifier },
            manager.ModelsByPurpose.SelectMany(pair => new[] { pair.Key, pair.Value }).ToArray());
    }

    [DataTestMethod]
    [DataRow("future/shared")]
    [DataRow("future\\shared")]
    [DataRow("future-id")]
    public void LoadConfiguration_WhenModelIsRegisteredLater_ResolvesModel(string specifier)
    {
        var manager = new AgentApiEndpointManager();
        manager.LoadConfiguration(CreateConfiguration("review", specifier));
        var expected = CreateModel("future", "shared", "future-id");

        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([expected]));

        Assert.AreSame(expected, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("future/shared")]
    [DataRow("future\\shared")]
    [DataRow("future-id")]
    public void SetModelForPurpose_WhenModelIsRegisteredLater_ResolvesModel(string specifier)
    {
        var manager = new AgentApiEndpointManager();
        manager.SetModelForPurpose("review", specifier);
        var expected = CreateModel("future", "shared", "future-id");

        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([expected]));

        Assert.AreSame(expected, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("future/shared")]
    [DataRow("future\\shared")]
    [DataRow("future-id")]
    public void ReplaceModelsByPurpose_WhenModelIsRegisteredLater_ResolvesModel(string specifier)
    {
        var manager = new AgentApiEndpointManager();
        manager.ReplaceModelsByPurpose(CreateConfiguration("review", specifier).ModelsByPurpose);
        var expected = CreateModel("future", "shared", "future-id");

        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([expected]));

        Assert.AreSame(expected, manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ResolveModel_WhenConfiguredModelWasNeverRegistered_ReturnsNull()
    {
        var (manager, _, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("review", "future/shared"));

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ReplaceConfiguration_WhenPurposeModelIsUnknown_StoresMappingWithoutResolving()
    {
        var (manager, _, _) = CreateManager();
        manager.ReplaceConfiguration(CreateReplacementConfiguration() with
        {
            ModelsByPurpose = new Dictionary<string, string> { ["review"] = "future/shared" },
        });

        Assert.AreEqual("future/shared", manager.ModelsByPurpose["review"]);
        Assert.IsNull(manager.ResolveModel("review"));

        var expected = CreateModel("future", "shared", "future-id");
        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([expected]));

        Assert.AreSame(expected, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("alpha/shared")]
    [DataRow("alpha\\shared")]
    [DataRow("alpha-id")]
    public void ResolveModel_WhenPurposeIsConfigured_UsesExistingModelResolution(string modelSpecifier)
    {
        var manager = new AgentApiEndpointManager();
        var other = CreateModel("beta", "shared", "beta-id");
        var expected = CreateModel("alpha", "shared", "alpha-id");
        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([other, expected]));
        manager.LoadConfiguration(CreateConfiguration("review", modelSpecifier));

        Assert.AreSame(expected, manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ResolveModel_WhenPurposeModelNameIsUnqualified_ResolvesByModelName()
    {
        var (manager, primary, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("review", "primary"));

        Assert.AreSame(primary, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("Review")]
    [DataRow("REVIEW")]
    [DataRow(" review")]
    [DataRow("review ")]
    public void ResolveModel_WhenPurposeKeyDoesNotMatchOrdinally_ReturnsNull(string purpose)
    {
        var (manager, _, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("review", "secondary"));

        Assert.IsNull(manager.ResolveModel(purpose));
    }

    [TestMethod]
    public void ResolveModel_WhenPurposeKeysDifferOnlyByCase_KeepsDistinctMappings()
    {
        var (manager, primary, secondary) = CreateManager();
        manager.LoadConfiguration(new AgentApiManagerConfiguration
        {
            ModelsByPurpose = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["review"] = "primary",
                ["Review"] = "secondary",
            },
        });

        CollectionAssert.AreEqual(
            new[] { primary, secondary },
            new[] { manager.ResolveModel("review"), manager.ResolveModel("Review") });
    }

    [TestMethod]
    public void ResolveModel_WhenPurposeSourceDictionaryIgnoresCase_StillUsesOrdinalKeys()
    {
        var (manager, _, _) = CreateManager();
        manager.LoadConfiguration(new AgentApiManagerConfiguration
        {
            ModelsByPurpose = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["review"] = "secondary",
            },
        });

        Assert.IsNull(manager.ResolveModel("Review"));
    }

    [TestMethod]
    public void ResolveModel_WhenPurposeIsNotConfigured_ReturnsNull()
    {
        var (manager, _, _) = CreateManager();

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ResolveModel_WhenNoModelsAreRegistered_ReturnsNull()
    {
        var manager = new AgentApiEndpointManager();

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t\r\n")]
    public void ResolveModel_WhenSpecifierIsNullOrWhiteSpace_ReturnsNull(string? modelSpecifier)
    {
        var (manager, _, _) = CreateManager();

        Assert.IsNull(manager.ResolveModel(modelSpecifier!));
    }

    [TestMethod]
    public void ResolveModel_WhenPrimaryChanges_UnconfiguredPurposeStillReturnsNull()
    {
        var (manager, _, secondary) = CreateManager();

        manager.PrimaryModel = secondary;

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("", "secondary")]
    [DataRow(" ", "secondary")]
    [DataRow("\t\r\n", "secondary")]
    [DataRow("review", null)]
    [DataRow("review", "")]
    [DataRow("review", " ")]
    [DataRow("review", "\t\r\n")]
    public void LoadConfiguration_WhenPurposeMappingIsInvalid_ThrowsArgumentException(string purpose, string? modelSpecifier)
    {
        var (manager, _, _) = CreateManager();
        var configuration = CreateConfiguration(purpose, modelSpecifier!);

        Assert.Throws<ArgumentException>(() => manager.LoadConfiguration(configuration));
    }

    [TestMethod]
    public void LoadConfiguration_WhenSuccessful_ReplacesRatherThanMergesPurposeMappings()
    {
        var (manager, primary, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("old", "secondary"));

        manager.LoadConfiguration(CreateConfiguration("new", "primary"));

        CollectionAssert.AreEqual(
            new FakeLanguageModel?[] { null, primary },
            new[] { manager.ResolveModel("old"), manager.ResolveModel("new") });
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void LoadConfiguration_WhenMappingsAreNullOrEmpty_ClearsPreviousMappings(bool useNull)
    {
        var (manager, _, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("review", "secondary"));

        manager.LoadConfiguration(new AgentApiManagerConfiguration
        {
            ModelsByPurpose = useNull ? null : new Dictionary<string, string>(),
        });

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [TestMethod]
    public void LoadConfiguration_WhenMappingValidationFails_PreservesAllPreviousMappings()
    {
        var (manager, _, secondary) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("old", "secondary"));
        var configuration = new AgentApiManagerConfiguration
        {
            ModelsByPurpose = new Dictionary<string, string>
            {
                ["new"] = "primary",
                ["invalid"] = " ",
            },
        };
        Assert.Throws<ArgumentException>(() => manager.LoadConfiguration(configuration));

        CollectionAssert.AreEqual(
            new FakeLanguageModel?[] { secondary, null, null },
            new[] { manager.ResolveModel("old"), manager.ResolveModel("new"), manager.ResolveModel("invalid") });
    }

    [TestMethod]
    public void LoadConfiguration_WhenPrimaryValidationFails_PreservesPreviousMappings()
    {
        var (manager, _, secondary) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("review", "secondary"));
        var configuration = CreateConfiguration("review", "primary") with { PrimaryModel = "missing" };
        Assert.Throws<ArgumentException>(() => manager.LoadConfiguration(configuration));

        Assert.AreSame(secondary, manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ReplaceConfiguration_WhenSuccessful_ReplacesModelsPrimaryAndPurposeMappings()
    {
        var (manager, _, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("old", "secondary"));

        // Replacement creates its own providers; constructing this configuration performs no network I/O.
        manager.ReplaceConfiguration(CreateReplacementConfiguration());

        var replacement = manager.ResolveModel("replacement/new");
        CollectionAssert.AreEqual(
            new object?[] { 1, "new", replacement, replacement, null, null },
            new object?[]
            {
                manager.GetSupportedModels().Count,
                manager.PrimaryModel.ModelDefinition.ModelName,
                manager.PrimaryModel,
                manager.ResolveModel("new-purpose"),
                manager.ResolveModel("old"),
                manager.ResolveModel("test/primary"),
            });
    }

    [TestMethod]
    public void ReplaceConfiguration_WhenMappingsAreNull_ClearsPreviousMappings()
    {
        var (manager, _, _) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("old", "secondary"));

        manager.ReplaceConfiguration(CreateReplacementConfiguration() with { ModelsByPurpose = null });

        Assert.IsNull(manager.ResolveModel("old"));
    }

    [DataTestMethod]
    [DataRow("", "replacement/new", "replacement/new")]
    [DataRow("review", " ", "replacement/new")]
    [DataRow("review", "replacement/new", "missing")]
    public void ReplaceConfiguration_WhenValidationFails_PreservesPreviousState(string purpose, string modelSpecifier, string primaryModel)
    {
        var (manager, primary, secondary) = CreateManager();
        manager.LoadConfiguration(CreateConfiguration("old", "secondary"));
        var configuration = CreateReplacementConfiguration() with
        {
            PrimaryModel = primaryModel,
            ModelsByPurpose = new Dictionary<string, string> { [purpose] = modelSpecifier },
        };
        Assert.Throws<ArgumentException>(() => manager.ReplaceConfiguration(configuration));

        CollectionAssert.AreEqual(
            new object?[] { primary, secondary, primary, secondary, null, null },
            new object?[]
            {
                manager.GetSupportedModels()[0],
                manager.GetSupportedModels()[1],
                manager.PrimaryModel,
                manager.ResolveModel("old"),
                manager.ResolveModel("review"),
                manager.ResolveModel("replacement/new"),
            });
    }

    [TestMethod]
    public void UnregisterLanguageModelProvider_WhenCalled_PreservesPurposeMappings()
    {
        var manager = new AgentApiEndpointManager();
        var removed = CreateModel("removed", "shared", "removed-id");
        var retained = CreateModel("retained", "shared", "retained-id");
        var provider = new FakeLanguageModelProvider([removed]);
        manager.RegisterLanguageModelProvider(provider);
        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([retained]));
        manager.PrimaryModel = retained;
        manager.LoadConfiguration(new AgentApiManagerConfiguration
        {
            ModelsByPurpose = new Dictionary<string, string>
            {
                ["removed-name"] = "removed/shared",
                ["removed-id"] = "removed-id",
                ["retained"] = "retained/shared",
            },
        });

        manager.UnregisterLanguageModelProvider(provider);

        CollectionAssert.AreEquivalent(
            new[] { "removed-name", "removed-id", "retained" },
            manager.ModelsByPurpose.Keys.ToArray());
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Configuration_WhenSerializedInMemory_RoundTripsPurposeMappings(bool fromElement)
    {
        var configuration = new AgentApiManagerConfiguration
        {
            PrimaryModel = "test/primary",
            ModelsByPurpose = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["review"] = "test/secondary",
                ["Review"] = "primary-id",
            },
        };
#if NET6_0
        var json = JsonSerializer.Serialize(configuration);
#else
        var json = JsonSerializer.Serialize(configuration,
            JsonConfigurationOpenAIProtocolLanguageModelJsonSerializerContext.Default.AgentApiManagerConfiguration);
#endif
        using var document = JsonDocument.Parse(json);
        var loaded = fromElement
            ? AgentApiManagerConfiguration.FromJsonElement(document.RootElement)
            : AgentApiManagerConfiguration.FromJsonString(json);
        var (manager, primary, secondary) = CreateManager();

        manager.LoadConfiguration(loaded);

        CollectionAssert.AreEqual(
            new[] { primary, secondary, primary },
            new[] { manager.PrimaryModel, manager.ResolveModel("review"), manager.ResolveModel("Review") });
    }

    [DataTestMethod]
    [DataRow("{}", true)]
    [DataRow("{}", false)]
    [DataRow("{\"ModelsByPurpose\":null}", true)]
    [DataRow("{\"ModelsByPurpose\":null}", false)]
    public void Configuration_WhenJsonOmitsOrNullsPurposeMappings_RemainsBackwardCompatible(string json, bool fromElement)
    {
        using var document = JsonDocument.Parse(json);
        var configuration = fromElement
            ? AgentApiManagerConfiguration.FromJsonElement(document.RootElement)
            : AgentApiManagerConfiguration.FromJsonString(json);

        Assert.IsNull(configuration.ModelsByPurpose);
    }

    private static (AgentApiEndpointManager Manager, FakeLanguageModel Primary, FakeLanguageModel Secondary) CreateManager()
    {
        var manager = new AgentApiEndpointManager();
        var primary = CreateModel("test", "primary", "primary-id");
        var secondary = CreateModel("test", "secondary", "secondary-id");
        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([primary, secondary]));
        manager.PrimaryModel = primary;
        return (manager, primary, secondary);
    }

    [TestMethod]
    public void SetModelForPurpose_WhenCalled_StoresOriginalSpecifier()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "test/secondary");

        Assert.AreEqual("test/secondary", manager.ModelsByPurpose["review"]);
    }

    [TestMethod]
    public void SetModelForPurpose_WhenPurposeExists_ReplacesModel()
    {
        var (manager, primary, _) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");
        manager.SetModelForPurpose("review", "primary");

        Assert.AreSame(primary, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow(null, "secondary")]
    [DataRow("", "secondary")]
    [DataRow(" ", "secondary")]
    [DataRow("review", null)]
    [DataRow("review", "")]
    [DataRow("review", " ")]
    [DataRow("\t\r\n", "secondary")]
    [DataRow("review", "\t\r\n")]
    public void SetModelForPurpose_WhenInvalid_PreservesPreviousMapping(string? purpose, string? modelSpecifier)
    {
        var (manager, _, secondary) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");
        Assert.Throws<ArgumentException>(() => manager.SetModelForPurpose(purpose!, modelSpecifier!));

        Assert.AreSame(secondary, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("review", true)]
    [DataRow("Review", false)]
    [DataRow("missing", false)]
    public void RemoveModelForPurpose_WhenCalled_ReturnsWhetherMappingExisted(string purpose, bool expected)
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");

        Assert.AreEqual(expected, manager.RemoveModelForPurpose(purpose));
    }

    [TestMethod]
    public void RemoveModelForPurpose_WhenRemoved_ResolveModelReturnsNull()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");
        manager.RemoveModelForPurpose("review");

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void RemoveModelForPurpose_WhenInvalid_ThrowsArgumentException(string? purpose)
    {
        var (manager, _, _) = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.RemoveModelForPurpose(purpose!));
    }

    [TestMethod]
    public void ReplaceModelsByPurpose_WhenSuccessful_ReplacesRatherThanMerges()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("old", "secondary");
        manager.ReplaceModelsByPurpose(new Dictionary<string, string> { ["new"] = "primary" });

        CollectionAssert.AreEqual(new[] { "new" }, manager.ModelsByPurpose.Keys.ToArray());
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ReplaceModelsByPurpose_WhenNullOrEmpty_ClearsMappings(bool useNull)
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");
        manager.ReplaceModelsByPurpose(useNull ? null : new Dictionary<string, string>());

        Assert.IsEmpty(manager.ModelsByPurpose);
    }

    [DataTestMethod]
    [DataRow("", "primary")]
    [DataRow(" ", "primary")]
    [DataRow("invalid", null)]
    [DataRow("invalid", "")]
    [DataRow("invalid", " ")]
    [DataRow("\t\r\n", "primary")]
    [DataRow("invalid", "\t\r\n")]
    public void ReplaceModelsByPurpose_WhenInvalid_PreservesAllMappings(string purpose, string? specifier)
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("old", "secondary");
        var replacement = new Dictionary<string, string> { ["new"] = "primary", [purpose] = specifier! };
        Assert.Throws<ArgumentException>(() => manager.ReplaceModelsByPurpose(replacement));

        CollectionAssert.AreEqual(new[] { "old" }, manager.ModelsByPurpose.Keys.ToArray());
    }

    [TestMethod]
    public void ModelsByPurpose_WhenCastToMutableDictionary_RejectsExternalModification()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");
        var snapshot = (IDictionary<string, string>) manager.ModelsByPurpose;

        Assert.Throws<NotSupportedException>(() => snapshot["review"] = "primary");
    }

    [TestMethod]
    public void ModelsByPurpose_WhenManagerChanges_PreservesSnapshot()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "secondary");
        var snapshot = manager.ModelsByPurpose;
        manager.SetModelForPurpose("review", "primary");

        Assert.AreEqual("secondary", snapshot["review"]);
    }

    [TestMethod]
    public void ReplaceModelsByPurpose_WhenSourceChanges_DoesNotChangeMapping()
    {
        var (manager, _, _) = CreateManager();
        var source = new Dictionary<string, string> { ["review"] = "secondary" };
        manager.ReplaceModelsByPurpose(source);
        source["review"] = "primary";

        Assert.AreEqual("secondary", manager.ModelsByPurpose["review"]);
    }

    [TestMethod]
    public void LoadConfiguration_WhenPrimaryIsInvalid_PreservesPreviousMappings()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("old", "secondary");
        var configuration = CreateConfiguration("new", "primary") with { PrimaryModel = "missing" };
        Assert.Throws<ArgumentException>(() => manager.LoadConfiguration(configuration));

        CollectionAssert.AreEqual(new[] { "old" }, manager.ModelsByPurpose.Keys.ToArray());
    }

    [DataTestMethod]
    [DataRow("removed/shared")]
    [DataRow("removed-id")]
    public void ResolveModel_WhenConfiguredModelIsUnregistered_ReturnsNull(string specifier)
    {
        var (manager, _, _) = CreateManager();
        var provider = new FakeLanguageModelProvider([CreateModel("removed", "shared", "removed-id")]);
        manager.RegisterLanguageModelProvider(provider);
        manager.SetModelForPurpose("review", specifier);
        manager.UnregisterLanguageModelProvider(provider);

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("removed/shared")]
    [DataRow("removed-id")]
    public void ResolveModel_WhenPurposeModelIsReregistered_ResolvesNewInstance(string specifier)
    {
        var (manager, _, _) = CreateManager();
        var provider = new FakeLanguageModelProvider([CreateModel("removed", "shared", "removed-id")]);
        manager.RegisterLanguageModelProvider(provider);
        manager.SetModelForPurpose("review", specifier);
        manager.UnregisterLanguageModelProvider(provider);
        var replacement = CreateModel("removed", "shared", "removed-id");
        manager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([replacement]));

        Assert.AreSame(replacement, manager.ResolveModel("review"));
    }

    [DataTestMethod]
    [DataRow("secondary")]
    [DataRow("secondary-id")]
    [DataRow("test/secondary")]
    [DataRow("test\\secondary")]
    public void ResolveModel_WhenPurposeDoesNotMatch_ResolvesDirectModelSpecifier(string modelSpecifier)
    {
        var (manager, _, secondary) = CreateManager();
        manager.SetModelForPurpose("review", "primary");

        Assert.AreSame(secondary, manager.ResolveModel(modelSpecifier));
    }

    [DataTestMethod]
    [DataRow("primary")]
    [DataRow("primary-id")]
    [DataRow("test/primary")]
    [DataRow("test\\primary")]
    public void ResolveModel_WhenPurposeConflictsWithModelSpecifier_PurposeTakesPriority(string purpose)
    {
        var (manager, _, secondary) = CreateManager();
        manager.SetModelForPurpose(purpose, "secondary");

        Assert.AreSame(secondary, manager.ResolveModel(purpose));
    }

    [TestMethod]
    public void ResolveModel_WhenConflictingPurposeModelIsMissing_ReturnsNull()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("primary", "missing");

        Assert.IsNull(manager.ResolveModel("primary"));
    }

    [TestMethod]
    public void ResolveModel_WhenMappedModelIsAlsoPurpose_ResolvesModelWithoutFollowingChain()
    {
        var (manager, primary, _) = CreateManager();
        manager.SetModelForPurpose("review", "primary");
        manager.SetModelForPurpose("primary", "secondary");

        Assert.AreSame(primary, manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ResolveModel_WhenPurposeChainHasNoDirectModel_ReturnsNull()
    {
        var (manager, _, _) = CreateManager();
        manager.SetModelForPurpose("review", "other-purpose");
        manager.SetModelForPurpose("other-purpose", "secondary");

        Assert.IsNull(manager.ResolveModel("review"));
    }

    [TestMethod]
    public void ResolveModel_WhenPurposeMapsToItself_ResolvesDirectModel()
    {
        var (manager, primary, _) = CreateManager();
        manager.SetModelForPurpose("primary", "primary");

        Assert.AreSame(primary, manager.ResolveModel("primary"));
    }

    private static FakeLanguageModel CreateModel(string provider, string name, string id)
    {
        return new FakeLanguageModel(new FakeChatClient())
        {
            ModelDefinition = new ModelDefinition { Provider = provider, ModelName = name, ModelId = id },
        };
    }

    private static AgentApiManagerConfiguration CreateConfiguration(string purpose, string modelSpecifier)
    {
        return new AgentApiManagerConfiguration
        {
            ModelsByPurpose = new Dictionary<string, string>(StringComparer.Ordinal) { [purpose] = modelSpecifier },
        };
    }

    private static AgentApiManagerConfiguration CreateReplacementConfiguration()
    {
        return new AgentApiManagerConfiguration
        {
            PrimaryModel = "replacement/new",
            ModelsByPurpose = new Dictionary<string, string> { ["new-purpose"] = "replacement/new" },
            OpenAIConfigurationList =
            [
                new OpenAIProtocolLanguageModelConfiguration("https://example.invalid", "test-key")
                {
                    ModelDefinitions = [new ModelDefinition { Provider = "replacement", ModelName = "new" }],
                },
            ],
        };
    }
}
