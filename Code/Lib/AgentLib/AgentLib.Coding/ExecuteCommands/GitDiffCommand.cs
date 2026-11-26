using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AgentLib.Logging;

namespace AgentLib.Coding.ExecuteCommands;

internal sealed class GitDiffCommand(string? workspacePath, ISessionCopilotChatLogger? logger) : IExecuteCommandHandler
{
    private const string CommandName = "git diff";
    private const int PreviewLimit = 16_000;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    internal static bool CanHandle(ReadOnlyMemory<string> argv, out ReadOnlyMemory<string> arguments)
    {
        if (argv.Span is ["git", "diff", ..])
        {
            arguments = argv[2..];
            return true;
        }

        arguments = default;
        return false;
    }

    async Task<string> IExecuteCommandHandler.HandleAsync(ReadOnlyMemory<string> arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return ExecuteCommandTool.FormatError("参数错误", "git diff 需要当前工作区。", CommandName);
        }

        if (HasRestrictedOption(arguments.Span))
        {
            return ExecuteCommandTool.FormatError("参数错误",
                "不支持写出文件、调用外部 diff 或启用 textconv 的选项。如需传入同名路径，请使用 ./ 前缀。", CommandName);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var process = new Process { StartInfo = CreateStartInfo(workspacePath, arguments.Span) };
        try
        {
            if (!process.Start())
            {
                return ExecuteCommandTool.FormatError("启动失败", "无法启动 Git 进程。", CommandName);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return await ReportFailureAsync(logger, workspacePath, "启动失败", exception).ConfigureAwait(false);
        }

        try
        {
            process.StandardInput.Close();
            Task<string> stdout = ReadPreviewAsync(process.StandardOutput, timeout.Token);
            Task<string> stderr = ReadPreviewAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(process.WaitForExitAsync(timeout.Token), stdout, stderr)
                .WaitAsync(timeout.Token).ConfigureAwait(false);
            string output = await stdout.ConfigureAwait(false);
            string error = await stderr.ConfigureAwait(false);
            return $"<MetaData>\n状态: 已退出\n命令: {CommandName}\n退出码: {process.ExitCode}\n</MetaData>"
                + (output.Length == 0 ? string.Empty : $"\n{output}")
                + (error.Length == 0 ? string.Empty : $"\n[stderr]\n{error}");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await StopAsync(process, logger, workspacePath).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteCommandTool.FormatError("超时", "git diff 未在 30 秒内完成，输出采集不完整。", CommandName);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception)
        {
            await StopAsync(process, logger, workspacePath).ConfigureAwait(false);
            return await ReportFailureAsync(logger, workspacePath, "采集失败", exception).ConfigureAwait(false);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string workspacePath, ReadOnlySpan<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspacePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        foreach (string argument in new[] { "--no-pager", "-c", "diff.autoRefreshIndex=false", "-c", "core.fsmonitor=false",
                     "diff", "--no-ext-diff", "--no-textconv" })
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return startInfo;
    }

    private static bool HasRestrictedOption(ReadOnlySpan<string> arguments)
    {
        // Git can consume a standalone "--" as an option value, so it cannot delimit this check.
        foreach (string argument in arguments)
        {
            if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Length == 2)
            {
                continue;
            }
            ReadOnlySpan<char> option = argument.AsSpan();
            int equals = option.IndexOf('=');
            if (equals >= 0) option = option[..equals];
            if (option.Length <= 2) continue;
            // Git accepts unambiguous long-option abbreviations in some diff modes.
            if ("--output".AsSpan().StartsWith(option, StringComparison.Ordinal)
                || "--ext-diff".AsSpan().StartsWith(option, StringComparison.Ordinal)
                || "--textconv".AsSpan().StartsWith(option, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static async Task<string> ReadPreviewAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var preview = new StringBuilder();
        var buffer = new char[4096];
        bool truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            int take = Math.Min(count, PreviewLimit - preview.Length);
            preview.Append(buffer, 0, take);
            truncated |= take < count;
        }
        if (truncated) preview.Append("\n【输出已截断】");
        return preview.ToString();
    }

    private static async Task StopAsync(Process process, ISessionCopilotChatLogger? logger, string workspacePath)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The process exited between HasExited and Kill.
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or AggregateException)
        {
            await logger.TryLogDiagnosticAsync("[ExecuteCommand][Stop]",
                $"无法结束 Git 进程。CommandName={CommandName}, WorkspacePath={workspacePath}", exception).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReportFailureAsync(ISessionCopilotChatLogger? logger, string workspacePath,
        string status, Exception exception)
    {
        await logger.TryLogDiagnosticAsync("[ExecuteCommand][Run]",
            $"Git 执行失败。CommandName={CommandName}, WorkspacePath={workspacePath}, FailureKind={status}", exception)
            .ConfigureAwait(false);
        return ExecuteCommandTool.FormatError(status, exception.Message, CommandName);
    }
}
