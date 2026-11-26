using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AgentLib.Logging;

namespace AgentLib.Coding.Commands;

internal sealed class GitDiffCommand(string? workspacePath, ISessionCopilotChatLogger logger) : ICommandHandler
{
    private const string CommandName = "git diff";
    private const int TimeoutSeconds = 30;
    private const int MaxOutputChars = 16_000;
    private static readonly string[] ControlledOptions = ["--output", "--ext-diff", "--textconv"];

    bool ICommandHandler.CanHandle(ReadOnlySpan<string> argv) => argv is ["git", "diff", ..];

    async Task<string> ICommandHandler.RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken)
    {
        if (workspacePath is null)
        {
            return CommandResult.Format("参数错误", CommandName, detail: "请先选择工作区。");
        }

        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workspacePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        string[] fixedArguments = ["--no-pager", "--no-optional-locks", "-c", "diff.autoRefreshIndex=false",
            "-c", "core.fsmonitor=false", "diff", "--no-ext-diff", "--no-textconv"];
        foreach (string argument in fixedArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        for (int i = 2; i < argv.Length; i++)
        {
            string argument = argv.Span[i];
            if (argument is null || argument.Contains('\0'))
            {
                return CommandResult.Format("参数错误", CommandName, detail: "Git 参数不能为 null 或包含 NUL 字符。");
            }

            // Git 接受长选项缩写，因此按选项名的前缀检查，不解析其取值。
            string option = argument.Split('=', 2)[0];
            if (option.Length > 2 && ControlledOptions.Any(name => name.StartsWith(option, StringComparison.Ordinal)))
            {
                return CommandResult.Format("参数错误", CommandName,
                    detail: "此命令仅支持标准输出和 Git 内置差异比较。与选项同名的路径请加 ./ 前缀。");
            }

            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        startInfo.Environment["GIT_ALLOW_PROTOCOL"] = string.Empty;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        using var process = new Process { StartInfo = startInfo };
        bool started = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started)
            {
                return CommandResult.Format("启动失败", CommandName, detail: "未能启动 Git CLI。");
            }
            process.StandardInput.Close();
            Task<CapturedOutput> stdout = CaptureAsync(process.StandardOutput, timeout.Token);
            Task<CapturedOutput> stderr = CaptureAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token)).WaitAsync(timeout.Token)
                .ConfigureAwait(false);
            CapturedOutput output = await stdout.ConfigureAwait(false);
            CapturedOutput error = await stderr.ConfigureAwait(false);
            return CommandResult.Format("已退出", CommandName, process.ExitCode, standardOutput: output.Text,
                standardError: error.Text, truncated: output.Truncated || error.Truncated);
        }
        catch (OperationCanceledException exception)
        {
            if (started) await StopAsync(process).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await LogFailureAsync("Timeout", exception).ConfigureAwait(false);
            return CommandResult.Format("超时", CommandName, detail: $"超过 {TimeoutSeconds} 秒，未完成输出采集。");
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            if (started) await StopAsync(process).ConfigureAwait(false);
            await LogFailureAsync(started ? "CaptureFailed" : "StartFailed", exception).ConfigureAwait(false);
            return CommandResult.Format(started ? "执行失败" : "启动失败", CommandName, detail: exception.Message);
        }
    }

    private static async Task<CapturedOutput> CaptureAsync(StreamReader stream, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        bool truncated = false;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            int length = Math.Min(read, MaxOutputChars - text.Length);
            text.Append(buffer, 0, length);
            truncated |= length < read;
        }
        if (truncated && char.IsHighSurrogate(text[^1])) text.Length--;
        return new CapturedOutput(text.ToString(), truncated);
    }

    private async Task StopAsync(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or AggregateException)
        {
            await LogFailureAsync("StopFailed", exception).ConfigureAwait(false);
        }
    }

    private Task LogFailureAsync(string kind, Exception exception) =>
        logger.TryLogDiagnosticAsync("[ExecuteCommand][Run]",
            $"CommandName={CommandName}, FailureKind={kind}, WorkspacePath={workspacePath}", exception);

    private readonly record struct CapturedOutput(string Text, bool Truncated);
}
