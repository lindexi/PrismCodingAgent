using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using AgentLib.Core;
using AgentLib.Coding;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.ViewModels;

public sealed partial class SettingsViewModel
{
    private static readonly string[] BuiltInPurposes =
    [
        ModelPurposes.DocumentGeneration, ModelPurposes.ImageAnalysis, ModelPurposes.UserCommunication,
        ModelPurposes.Reasoning, CodingModelPurposes.CodeDesign, CodingModelPurposes.CodeReview,
        CodingModelPurposes.CodeExploration, ModelPurposes.SessionTitle, ModelPurposes.HistoryCompression,
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
    }

    private string[] GetPurposeModelReferences() => Providers.SelectMany(provider => provider.Models)
        .Where(model => !string.IsNullOrWhiteSpace(model.ModelName))
        .Select(model => $"{model.Provider}/{model.ModelName}").Distinct(StringComparer.Ordinal).ToArray();

    private void LoadPurposeModels(IReadOnlyDictionary<string, string>? mapping)
    {
        _purposeMappingWasNull = mapping is null;
        PurposeModels.Clear();
        foreach (string key in BuiltInPurposes.Concat(mapping?.Keys.Where(key => !BuiltInPurposes.Contains(key)) ?? []))
        {
            var row = new PurposeModelSettingsViewModel(key, BuiltInPurposes.Contains(key));
            string? reference = mapping is not null && mapping.TryGetValue(key, out string? model) ? model : null;
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

    private IReadOnlyDictionary<string, string>? CreatePurposeMapping()
    {
        ValidatePurposeModels();
        if (PurposeModels.Any(row => row.HasErrors))
            throw new ArgumentException(Lang.Current.PurposeValidationFailed);
        if (_purposeMappingWasNull && PurposeModels.All(row => row.SelectedModel?.ModelReference is null))
            return null;
        return PurposeModels.Where(row => row.SelectedModel?.ModelReference is not null)
            .ToDictionary(row => row.PurposeKey.Trim(), row => row.SelectedModel!.ModelReference!, StringComparer.Ordinal);
    }

    private void DisposePurposeModels()
    {
        PurposeModels.CollectionChanged -= OnPurposeRowsChanged;
        foreach (var row in _purposeRowSubscriptions) row.PropertyChanged -= OnPurposeRowChanged;
        foreach (var model in _purposeModelSubscriptions) model.PropertyChanged -= OnPurposeModelChanged;
        _purposeRowSubscriptions.Clear();
        _purposeModelSubscriptions.Clear();
    }
}
