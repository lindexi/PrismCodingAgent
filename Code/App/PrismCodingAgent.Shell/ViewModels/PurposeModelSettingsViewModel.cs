using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AgentLib.Core;
using AgentLib.Coding;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.ViewModels;

/// <summary>用途模型下拉选项；空引用表示不保存用途映射。</summary>
public sealed record PurposeModelOptionViewModel(string? ModelReference, string DisplayName, bool IsAvailable);

/// <summary>WPF 和 Avalonia 共用的用途模型草稿行。</summary>
public sealed class PurposeModelSettingsViewModel : ViewModelBase
{
    private string _purposeKey;
    private PurposeModelOptionViewModel? _selectedModel;
    private string? _purposeError;
    private string? _modelError;
    private ObservableCollection<PurposeModelOptionViewModel> _modelOptions = [];

    internal PurposeModelSettingsViewModel(string purposeKey, bool isBuiltIn)
    {
        _purposeKey = purposeKey;
        IsBuiltIn = isBuiltIn;
    }

    /// <summary>获取或编辑自定义用途键。内置键不可修改。</summary>
    public string PurposeKey
    {
        get => _purposeKey;
        set
        {
            if (!IsBuiltIn && SetField(ref _purposeKey, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>获取内置用途的本地化名称或自定义键。</summary>
    public string DisplayName => PurposeKey switch
    {
        ModelPurposes.DocumentGeneration when IsBuiltIn => Lang.Current.PurposeDocumentGeneration,
        ModelPurposes.ImageAnalysis when IsBuiltIn => Lang.Current.PurposeImageAnalysis,
        ModelPurposes.UserCommunication when IsBuiltIn => Lang.Current.PurposeUserCommunication,
        ModelPurposes.Reasoning when IsBuiltIn => Lang.Current.PurposeReasoning,
        CodingModelPurposes.CodeDesign when IsBuiltIn => Lang.Current.PurposeCodeDesign,
        CodingModelPurposes.CodeReview when IsBuiltIn => Lang.Current.PurposeCodeReview,
        CodingModelPurposes.CodeExploration when IsBuiltIn => Lang.Current.PurposeCodeExploration,
        ModelPurposes.SessionTitle when IsBuiltIn => Lang.Current.PurposeSessionTitle,
        ModelPurposes.HistoryCompression when IsBuiltIn => Lang.Current.PurposeHistoryCompression,
        _ => PurposeKey,
    };

    /// <summary>获取是否为内置用途。</summary>
    public bool IsBuiltIn { get; }
    /// <summary>获取是否允许编辑用途键。</summary>
    public bool CanEditPurpose => !IsBuiltIn;
    /// <summary>获取是否允许删除此行。</summary>
    public bool CanRemove => !IsBuiltIn;
    /// <summary>获取模型选项，包含未指定项及当前失效的旧引用。</summary>
    public ObservableCollection<PurposeModelOptionViewModel> ModelOptions => _modelOptions;
    /// <summary>获取或设置草稿选择；未指定项不写入映射。</summary>
    public PurposeModelOptionViewModel? SelectedModel
    {
        get => _selectedModel;
        set => SetField(ref _selectedModel, value);
    }
    /// <summary>获取用途键的就地校验消息。</summary>
    public string? PurposeError => _purposeError;
    /// <summary>获取模型的就地校验消息。</summary>
    public string? ModelError => _modelError;
    /// <summary>获取是否存在校验错误。</summary>
    public bool HasErrors => PurposeError is not null || ModelError is not null;

    internal void RefreshModels(IReadOnlyList<string> references, string? selectedReference)
    {
        var options = new ObservableCollection<PurposeModelOptionViewModel>
        {
            new(null, Lang.Current.PurposeUnspecified, true),
        };
        foreach (string reference in references)
            options.Add(new(reference, reference, true));
        if (selectedReference is not null && !references.Contains(selectedReference, StringComparer.Ordinal))
            options.Add(new(selectedReference, Lang.Format(Lang.Current.PurposeUnavailable, selectedReference), false));

        _modelOptions = options;
        // 在通知列表变更前更新选择，避免控件清空旧列表时丢失草稿引用。
        _selectedModel = options.First(option => option.ModelReference == selectedReference);
        OnPropertyChanged(nameof(ModelOptions));
        OnPropertyChanged(nameof(SelectedModel));
        OnPropertyChanged(nameof(DisplayName));
    }

    internal void Validate(bool duplicate, IReadOnlyList<string> references)
    {
        string? keyError = string.IsNullOrWhiteSpace(PurposeKey) ? Lang.Current.PurposeKeyRequired
            : duplicate ? Lang.Current.PurposeKeyDuplicate : null;
        string? modelError = SelectedModel?.ModelReference is { } reference
            && !references.Contains(reference, StringComparer.Ordinal) ? Lang.Current.PurposeModelInvalid : null;
        SetField(ref _purposeError, keyError, nameof(PurposeError));
        SetField(ref _modelError, modelError, nameof(ModelError));
        OnPropertyChanged(nameof(HasErrors));
    }
}
