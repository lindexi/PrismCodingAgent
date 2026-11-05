using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;

namespace PrismCodingAgent.CodingShell.ViewModels;

public sealed class ProviderSettingsViewModel : ViewModelBase
{
    private string _endPoint = string.Empty;
    private string _apiKey = string.Empty;
    public ProviderSettingsViewModel()
    {
        AddModelCommand = new SimpleCommand(AddModel);
        RemoveModelCommand = new SimpleCommand<ModelSettingsViewModel>(RemoveModel);
    }

    public string EndPoint
    {
        get => _endPoint;
        set => SetField(ref _endPoint, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetField(ref _apiKey, value);
    }

    public ObservableCollection<ModelSettingsViewModel> Models { get; } = [];

    public ICommand AddModelCommand { get; }

    public ICommand RemoveModelCommand { get; }

    internal OpenAIProtocolLanguageModelConfiguration ToConfiguration()
    {
        return new OpenAIProtocolLanguageModelConfiguration(EndPoint, ApiKey)
        {
            ModelDefinitions = Models.Where(model => !string.IsNullOrWhiteSpace(model.ModelName))
                .Select(model => model.ToDefinition()).ToArray(),
        };
    }

    internal static ProviderSettingsViewModel FromConfiguration(OpenAIProtocolLanguageModelConfiguration configuration)
    {
        var viewModel = new ProviderSettingsViewModel
        {
            EndPoint = configuration.EndPoint,
            ApiKey = configuration.Key,
        };

        if (configuration.ModelDefinitions is { } definitions)
        {
            foreach (ModelDefinition definition in definitions)
            {
                viewModel.Models.Add(ModelSettingsViewModel.FromDefinition(definition));
            }
        }

        return viewModel;
    }

    private void AddModel() => Models.Add(new ModelSettingsViewModel());

    private void RemoveModel(ModelSettingsViewModel? model)
    {
        if (model is not null)
        {
            Models.Remove(model);
        }
    }
}