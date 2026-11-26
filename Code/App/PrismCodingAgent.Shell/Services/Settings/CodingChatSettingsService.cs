using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentLib.Coding;
using AgentLib.Coding.Sandboxes;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.Services.Settings;

internal sealed class CodingChatSettingsService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly CodingChatRoomPaths _paths;
    private readonly WindowsSandboxToolSource _windowsSandboxToolSource;
    private readonly DeepSeekWebToolSource? _deepSeekWebToolSource;

    internal PrismCodingAgentShellSettings AppliedShellSettings { get; private set; } = new()
    {
        LanguageTag = ShellLanguage.GetDefault(),
    };

    public CodingChatSettingsService
    (
        CodingChatRoomPaths paths,
        WindowsSandboxToolSource windowsSandboxToolSource,
        DeepSeekWebToolSource? deepSeekWebToolSource = null
    )
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(windowsSandboxToolSource);
        _paths = paths;
        _windowsSandboxToolSource = windowsSandboxToolSource;
        _deepSeekWebToolSource = deepSeekWebToolSource;
    }

    public async Task<CodingChatSettingsSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        PrismCodingAgentShellSettings shellSettings = await LoadInitialShellSettingsAsync(cancellationToken).ConfigureAwait(false);
        AgentApiManagerConfiguration? modelConfiguration = null;
        string? modelConfigurationError = null;

        _paths.ConfigurationFile.Refresh();
        if (_paths.ConfigurationFile.Exists)
        {
            try
            {
                modelConfiguration = await AgentApiManagerConfiguration
                    .FromJsonFileAsync(_paths.ConfigurationFile)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                modelConfigurationError = exception.Message;
            }
        }

        return new CodingChatSettingsSnapshot(modelConfiguration, shellSettings, modelConfigurationError);
    }

    public async Task SaveAsync
    (
        AgentApiManagerConfiguration modelConfiguration,
        PrismCodingAgentShellSettings shellSettings,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(modelConfiguration);
        ArgumentNullException.ThrowIfNull(shellSettings);
        cancellationToken.ThrowIfCancellationRequested();

        _paths.EnsureDirectories();

        await modelConfiguration.SaveToFileAsync(_paths.ConfigurationFile).ConfigureAwait(false);

        shellSettings = shellSettings with { LanguageTag = ShellLanguage.Normalize(shellSettings.LanguageTag) };
        string shellJson = JsonSerializer.Serialize(shellSettings, s_jsonOptions);
        await File.WriteAllTextAsync
        (
            _paths.ShellSettingsFile.FullName,
            shellJson,
            cancellationToken
        ).ConfigureAwait(false);

        ApplyShellSettings(modelConfiguration, shellSettings);
        _windowsSandboxToolSource.UpdateConfiguration
        (
            shellSettings.IsWindowsSandboxEnabled,
            shellSettings.WindowsSandboxToolPath,
            shellSettings.WindowsSandboxServerAddress
        );
    }

    internal void ApplyShellSettings(
        AgentApiManagerConfiguration modelConfiguration,
        PrismCodingAgentShellSettings shellSettings)
    {
        _deepSeekWebToolSource?.UpdateConfiguration(
            DeepSeekApiKeyResolver.Resolve(shellSettings.DeepSeekApiKey, modelConfiguration));
        AppliedShellSettings = shellSettings;
    }

    internal async Task<PrismCodingAgentShellSettings> LoadInitialShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        _paths.ShellSettingsFile.Refresh();
        return _paths.ShellSettingsFile.Exists
            ? await LoadShellSettingsAsync(cancellationToken).ConfigureAwait(false)
            : new PrismCodingAgentShellSettings { IsWindowsSandboxEnabled = false, LanguageTag = ShellLanguage.GetDefault() };
    }

    public async Task<PrismCodingAgentShellSettings> LoadShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        _paths.ShellSettingsFile.Refresh();
        if (!_paths.ShellSettingsFile.Exists)
        {
            return new PrismCodingAgentShellSettings { LanguageTag = ShellLanguage.GetDefault() };
        }

        string json = await File.ReadAllTextAsync
        (
            _paths.ShellSettingsFile.FullName,
            cancellationToken
        ).ConfigureAwait(false);
        var settings = JsonSerializer.Deserialize<PrismCodingAgentShellSettings>(json, s_jsonOptions)
                       ?? new PrismCodingAgentShellSettings();
        return settings with
        {
            LanguageTag = settings.LanguageTag is { } languageTag
                ? ShellLanguage.Normalize(languageTag)
                : ShellLanguage.GetDefault(),
        };
    }
}

internal sealed record CodingChatSettingsSnapshot
(
    AgentApiManagerConfiguration? ModelConfiguration,
    PrismCodingAgentShellSettings ShellSettings,
    string? ModelConfigurationError
);

internal sealed class CodingChatSettingsSavedEventArgs(CodingChatSettingsSnapshot settings) : EventArgs
{
    public CodingChatSettingsSnapshot Settings { get; } = settings
        ?? throw new ArgumentNullException(nameof(settings));
}