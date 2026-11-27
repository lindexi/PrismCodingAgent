using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using AgentLib.Coding.SubAgents;
using AgentLib.Core;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.ViewModels;

public sealed partial class SettingsViewModel
{
    private static readonly string[] BuiltInPurposes =
    [
        ModelPurposes.DocumentGeneration, ModelPurposes.ImageAnalysis, ModelPurposes.UserCommunication,
        ModelPurposes.Reasoning, CodingModelPurposes.CodeDesign, CodingModelPurposes.CodeWriting,
        CodingModelPurposes.CodeReview, CodingModelPurposes.CodeExploration, ModelPurposes.SessionTitle,
        ModelPurposes.HistoryCompression,
    ];
    private readonly HashSet<ModelSettingsViewModel> _purposeModelSubscriptions = [];
    private readonly HashSet<PurposeModelSettingsViewModel> _purposeRowSubscriptions = [];
    private bool _refreshingPurposeModels;
    private bool _purposeMappingWasNull;

    /// <summary>获取用途模型草稿行；内置用途始终列出。</summary>
    public ObservableCollection<PurposeModelSettingsViewModel> PurposeModels { get; } = [];
    /// <summary>获取添加自定义用途草稿的命令。</summary>
    public ICommand AddPurposeModelCommand { get; private set; } = null!;
    /// <summary>获取删除自定义用途的命令，参数为用途行。</summary>
    public ICommand RemovePurposeModelCommand { get; private set; } = null!;

    /// <summary>获取用途错误状态，供折叠标题提示。</summary>
    public bool HasPurposeErrors => PurposeModels.Any(row => row.HasErrors);

    private void InitializePurposeModels()
    {
        AddPurposeModelCommand = new SimpleCommand(() => PurposeModels.Add(new(string.Empty, false)));
        RemovePurposeModelCommand = new SimpleCommand<PurposeModelSettingsViewModel>(row =>
        {
            if (row is { CanRemove: true }) PurposeModels.Remove(row);
        }, row => row is { CanRemove: true } && PurposeModels.Contains(row));
        PurposeModels.CollectionChanged += OnPurposeRowsChanged;
        LoadPurposeModels(null);
        _purposeMappingWasNull = false;
        ((INotifyPropertyChanged)Lang.Current).PropertyChanged += OnPurposeLanguageChanged;
    }

    private void OnPurposeLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var row in PurposeModels)
            foreach (var option in row.ReasoningEffortOptions)
                option.RefreshDisplayName();
        RefreshPurposeModels();
    }

    private string[] GetPurposeModelReferences() => Providers.SelectMany(provider => provider.Models)
        .Where(model => !string.IsNullOrWhiteSpace(model.ModelName))
        .Select(model => $"{model.Provider}/{model.ModelName}").Distinct(StringComparer.Ordinal).ToArray();

    private void LoadPurposeModels(AgentApiManagerConfiguration? configuration)
    {
        var mapping = configuration?.ModelsByPurpose;
        _purposeMappingWasNull = mapping is null;
        PurposeModels.Clear();
        foreach (string key in BuiltInPurposes.Union(mapping?.Keys ?? [], StringComparer.Ordinal))
        {
            var settings = mapping?.GetValueOrDefault(key);
            string? reference = settings?.ModelSpecifier;
            var row = new PurposeModelSettingsViewModel(key, BuiltInPurposes.Contains(key), settings?.ReasoningEffort);
            // 兼容既有配置中的无 Provider 短名称，但新选择统一保存完整引用。
            if (reference is not null && !reference.Contains('/'))
                reference = GetPurposeModelReferences().FirstOrDefault(item => item.EndsWith("/" + reference, StringComparison.Ordinal)) ?? reference;
            row.RefreshModels(GetPurposeModelReferences(), reference);
            PurposeModels.Add(row);
        }
    }

    private void OnPurposeRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in _purposeRowSubscriptions.Where(row => !PurposeModels.Contains(row)).ToArray())
        {
            row.PropertyChanged -= OnPurposeRowChanged;
            _purposeRowSubscriptions.Remove(row);
        }
        foreach (var row in PurposeModels)
            if (_purposeRowSubscriptions.Add(row)) row.PropertyChanged += OnPurposeRowChanged;
        RefreshPurposeModels();
    }

    private void OnPurposeModelsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshPurposeModels();

    private void OnPurposeModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(ModelSettingsViewModel.Provider) or nameof(ModelSettingsViewModel.ModelName))
            RefreshPurposeModels();
    }

    private void OnPurposeRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_refreshingPurposeModels && e.PropertyName is nameof(PurposeModelSettingsViewModel.PurposeKey) or nameof(PurposeModelSettingsViewModel.SelectedModel))
            ValidatePurposeModels();
    }

    private void RefreshPurposeModels()
    {
        var models = Providers.SelectMany(provider => provider.Models).ToHashSet();
        foreach (var model in _purposeModelSubscriptions.Where(model => !models.Contains(model)).ToArray())
        {
            model.PropertyChanged -= OnPurposeModelChanged;
            _purposeModelSubscriptions.Remove(model);
        }
        foreach (var model in models)
            if (_purposeModelSubscriptions.Add(model)) model.PropertyChanged += OnPurposeModelChanged;
        var references = GetPurposeModelReferences();
        _refreshingPurposeModels = true;
        try
        {
            foreach (var row in PurposeModels)
                row.RefreshModels(references, row.SelectedModel?.ModelReference);
        }
        finally
        {
            _refreshingPurposeModels = false;
        }
        ValidatePurposeModels();
    }

    private void ValidatePurposeModels()
    {
        var references = GetPurposeModelReferences();
        foreach (var row in PurposeModels)
            row.Validate(PurposeModels.Count(other => string.Equals(other.PurposeKey?.Trim(), row.PurposeKey?.Trim(), StringComparison.Ordinal)) > 1, references);
        OnPropertyChanged(nameof(HasPurposeErrors));
    }

    private IReadOnlyDictionary<string, PurposeModelConfiguration>? CreatePurposeMapping()
    {
        ValidatePurposeModels();
        if (PurposeModels.Any(row => row.HasErrors))
            throw new ArgumentException(Lang.Current.PurposeValidationFailed);

        var mapping = new Dictionary<string, PurposeModelConfiguration>(StringComparer.Ordinal);
        foreach (var row in PurposeModels)
        {
            var model = row.SelectedModel?.ModelReference;
            var effort = row.SelectedReasoningEffort.Value;
            if (model is not null || effort is not null)
                mapping.Add(row.PurposeKey.Trim(), new(model, effort));
        }
        return _purposeMappingWasNull && mapping.Count == 0 ? null : mapping;
    }

    private void DisposePurposeModels()
    {
        ((INotifyPropertyChanged)Lang.Current).PropertyChanged -= OnPurposeLanguageChanged;
        PurposeModels.CollectionChanged -= OnPurposeRowsChanged;
        foreach (var row in _purposeRowSubscriptions) row.PropertyChanged -= OnPurposeRowChanged;
        foreach (var model in _purposeModelSubscriptions) model.PropertyChanged -= OnPurposeModelChanged;
        _purposeRowSubscriptions.Clear();
        _purposeModelSubscriptions.Clear();
    }
}
