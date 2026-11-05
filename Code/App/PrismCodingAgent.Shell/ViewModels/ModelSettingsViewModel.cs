using AgentLib.Core.AgentApiManagers.Contexts;

namespace PrismCodingAgent.CodingShell.ViewModels;

public sealed class ModelSettingsViewModel : ViewModelBase
{
    private string _provider = string.Empty;
    private string _modelName = string.Empty;
    private string? _modelId;
    private int? _contextWindowSize;
    private int? _maxOutputTokens;
    private bool _supportsReasoning;
    private bool _supportsToolCall = true;
    private bool _supportsImageInput;
    private bool _isFlash;

    public string Provider
    {
        get => _provider;
        set => SetField(ref _provider, value);
    }

    public string ModelName
    {
        get => _modelName;
        set => SetField(ref _modelName, value);
    }

    public string? ModelId
    {
        get => _modelId;
        set => SetField(ref _modelId, value);
    }

    public int? ContextWindowSize
    {
        get => _contextWindowSize;
        set => SetField(ref _contextWindowSize, value);
    }

    public int? MaxOutputTokens
    {
        get => _maxOutputTokens;
        set => SetField(ref _maxOutputTokens, value);
    }

    public bool SupportsReasoning
    {
        get => _supportsReasoning;
        set => SetField(ref _supportsReasoning, value);
    }

    public bool SupportsToolCall
    {
        get => _supportsToolCall;
        set => SetField(ref _supportsToolCall, value);
    }

    public bool SupportsImageInput
    {
        get => _supportsImageInput;
        set => SetField(ref _supportsImageInput, value);
    }

    public bool IsFlash
    {
        get => _isFlash;
        set => SetField(ref _isFlash, value);
    }

    internal ModelDefinition ToDefinition()
    {
        return new ModelDefinition
        {
            Provider = Provider,
            ModelName = ModelName,
            ModelId = ModelId,
            ContextWindowSize = ContextWindowSize,
            MaxOutputTokens = MaxOutputTokens,
            Capabilities = new LlmModelCapabilities
            {
                Reasoning = SupportsReasoning,
                ToolCall = SupportsToolCall,
                IsFlash = IsFlash,
                Attachment = SupportsImageInput,
                Input = new LlmModalityCapability { Image = SupportsImageInput },
            },
        };
    }

    internal static ModelSettingsViewModel FromDefinition(ModelDefinition definition)
    {
        return new ModelSettingsViewModel
        {
            Provider = definition.Provider,
            ModelName = definition.ModelName,
            ModelId = definition.ModelId,
            ContextWindowSize = definition.ContextWindowSize,
            MaxOutputTokens = definition.MaxOutputTokens,
            SupportsReasoning = definition.Capabilities?.Reasoning ?? false,
            SupportsToolCall = definition.Capabilities?.ToolCall ?? true,
            SupportsImageInput = definition.Capabilities?.Input.Image ?? false,
            IsFlash = definition.Capabilities?.IsFlash ?? false,
        };
    }
}