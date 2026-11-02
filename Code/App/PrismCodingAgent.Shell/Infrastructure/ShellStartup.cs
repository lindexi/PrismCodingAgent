using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgentLib;
using AgentLib.Coding.Sandboxes;
using PrismCodingAgent.AvaloniaShell.Abilities;
using PrismCodingAgent.AvaloniaShell.Services.WorkTasks;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Infrastructure;

internal static class ShellStartup
{
    internal static async Task<MainViewModel> InitializeAsync(CodingChatRoomPaths paths, IMainThreadDispatcher dispatcher)
    {
        var runtimes = new List<CodingWorkTaskRuntime>();
        var tasks = new List<WorkTaskItemViewModel>();
        MainViewModel? mainViewModel = null;
        try
        {
            var windowsSandboxToolSource = new WindowsSandboxToolSource(false, string.Empty, string.Empty);
            var languageSettingsService = new Services.Settings.CodingChatSettingsService(paths, windowsSandboxToolSource);
            var shellSettings = await languageSettingsService.LoadInitialShellSettingsAsync().ConfigureAwait(true);
            Localizations.Lang.SetCurrent(shellSettings.LanguageTag == "en" ? "en" : "zh-Hans");
            await DefaultAbilityInstaller.InstallOnceAsync(paths.AbilitiesDirectory).ConfigureAwait(true);
            var abilityCatalog = new AbilityCatalog(paths.AbilitiesDirectory);
            await abilityCatalog.RefreshAsync().ConfigureAwait(true);
            var workTaskStore = new WorkTaskStore(paths);
            IReadOnlyList<WorkTaskRecord> records = await workTaskStore.LoadAsync().ConfigureAwait(true);
            WorkTaskRecord[] activeRecords = records.Where(record => !record.IsArchived).ToArray();
            WorkTaskRecord[] archivedRecords = records.Where(record => record.IsArchived).ToArray();

            if (activeRecords.Length == 0)
            {
                CodingWorkTaskRuntime runtime = await CodingWorkTaskRuntimeFactory
                    .InitializeAsync(paths, dispatcher, windowsSandboxToolSource).ConfigureAwait(true);
                runtimes.Add(runtime);
                tasks.Add(MainViewModel.CreateRuntimeTask(runtime,
                    new WorkTaskRecord(Guid.NewGuid(), $"{Localizations.Lang.Current.WorkTaskText} 1", null,
                        runtime.ModelDisplayName, null), abilityCatalog));
            }
            else
            {
                foreach (WorkTaskRecord record in activeRecords)
                {
                    CodingWorkTaskRuntime runtime = await CodingWorkTaskRuntimeFactory
                        .InitializeAsync(paths, dispatcher, windowsSandboxToolSource).ConfigureAwait(true);
                    runtimes.Add(runtime);
                    WorkTaskItemViewModel task = MainViewModel.CreateRuntimeTask(runtime, record, abilityCatalog);
                    tasks.Add(task);
                    await RestoreTaskConfigurationAsync(task, runtime, record).ConfigureAwait(true);
                }
            }

            mainViewModel = MainViewModel.Create(tasks, archivedRecords, runtimes[0].SettingsService, workTaskStore,
                () => CodingWorkTaskRuntimeFactory.InitializeAsync(paths, dispatcher, windowsSandboxToolSource),
                abilityCatalog, FormatWorkTaskRecoveryMessage(workTaskStore.LastRecoveryInfo));
            if (activeRecords.Length == 0)
            {
                WorkTaskItemViewModel task = tasks[0];
                await workTaskStore.SaveAsync([new WorkTaskRecord(task.Id, task.DisplayName,
                    task.Chat.NextRunWorkspacePath, task.Chat.SelectedModel?.DisplayName,
                    task.Chat.SelectedReasoningEffort?.Value, false), .. archivedRecords]).ConfigureAwait(true);
            }
            await mainViewModel.InitializeSettingsAsync().ConfigureAwait(true);
            return mainViewModel;
        }
        catch
        {
            await DisposeStartupStateAsync(mainViewModel, tasks, runtimes).ConfigureAwait(true);
            throw;
        }
    }

    internal static async Task DisposeStartupStateAsync(MainViewModel? mainViewModel,
        IReadOnlyList<WorkTaskItemViewModel> tasks, IReadOnlyList<CodingWorkTaskRuntime> runtimes)
    {
        if (mainViewModel is not null)
        {
            await mainViewModel.DisposeAsync().ConfigureAwait(true);
            return;
        }
        // MainViewModel 尚未接管时，启动代码仍拥有任务及尚未关联任务的运行时。
        foreach (WorkTaskItemViewModel task in tasks)
        {
            task.Detach();
            task.Chat.Dispose();
            task.Sessions.Dispose();
        }
        await Task.WhenAll(runtimes.Select(runtime => runtime.DisposeAsync().AsTask())).ConfigureAwait(true);
    }

    private static async Task RestoreTaskConfigurationAsync(WorkTaskItemViewModel task,
        CodingWorkTaskRuntime runtime, WorkTaskRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.WorkspacePath))
        {
            await runtime.WorkspaceController.ChangeWorkspaceAsync(record.WorkspacePath).ConfigureAwait(true);
        }
        LanguageModelOptionViewModel? model = task.Chat.AvailableModels.FirstOrDefault
            (option => string.Equals(option.DisplayName, record.ModelReference, StringComparison.Ordinal));
        if (model is not null) task.Chat.SelectedModel = model;
        ReasoningEffortOptionViewModel? effort = task.Chat.AvailableReasoningEfforts.FirstOrDefault
            (option => option.Value == record.ReasoningEffort);
        if (effort is not null) task.Chat.SelectedReasoningEffort = effort;
    }

    private static string? FormatWorkTaskRecoveryMessage(WorkTaskRecoveryInfo? recoveryInfo)
        => recoveryInfo is null ? null
            : Localizations.Lang.Format(Localizations.Lang.Current.WorkTaskRecoveryMessage,
                recoveryInfo.AssignedMissingIdCount, recoveryInfo.ReassignedDuplicateIdCount, recoveryInfo.BackupFilePath);
}
