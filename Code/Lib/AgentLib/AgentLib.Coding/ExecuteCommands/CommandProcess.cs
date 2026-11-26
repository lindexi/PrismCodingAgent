using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AgentLib.Logging;

namespace AgentLib.Coding.ExecuteCommands;

internal static class CommandProcess
{
    private const int TimeoutSeconds = 30;
    private const int MaxOutputChars = 16_000;

    internal static ProcessStartInfo CreateStartInfo(string fileName, string? workspacePath = null) => new(fileName)
    {
        WorkingDirectory = workspacePath ?? string.Empty,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    internal static async Task<string> RunAsync(ProcessStartInfo startInfo, string commandName,
        ISessionCopilotChatLogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        using var process = new Process { StartInfo = startInfo };
        bool started = false;
        try
        {
            started = process.Start();
            if (!started)
            {
                return CommandResult.Format("启动失败", commandName, detail: $"未能启动客户端：{startInfo.FileName}");
            }

            process.StandardInput.Close();
            using StreamReader stdout = process.StandardOutput;
            using StreamReader stderr = process.StandardError;
            Task<CapturedOutput> outputTask = CaptureAsync(stdout, timeout.Token);
            Task<CapturedOutput> errorTask = CaptureAsync(stderr, timeout.Token);
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeout.Token))
                .WaitAsync(timeout.Token).ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();
            CapturedOutput output = await outputTask.ConfigureAwait(false);
            CapturedOutput error = await errorTask.ConfigureAwait(false);
            return CommandResult.Format("已退出", commandName, process.ExitCode, standardOutput: output.Text,
                standardError: error.Text, truncated: output.Truncated || error.Truncated);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            if (started) await StopAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await LogFailureAsync("Timeout", exception).ConfigureAwait(false);
            return CommandResult.Format("超时", commandName, detail: $"超过 {TimeoutSeconds} 秒，未完成输出采集。");
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            if (started) await StopAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await LogFailureAsync(started ? "CaptureFailed" : "StartFailed", exception).ConfigureAwait(false);
            return CommandResult.Format(started ? "执行失败" : "启动失败", commandName, detail: exception.Message);
        }

        async Task StopAsync()
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

        Task LogFailureAsync(string failureKind, Exception exception) =>
            logger.TryLogDiagnosticAsync("[ExecuteCommand][Run]",
                $"CommandName={commandName}, FailureKind={failureKind}, Executable={startInfo.FileName}, WorkingDirectory={startInfo.WorkingDirectory}", exception);
    }

    private static async Task<CapturedOutput> CaptureAsync(StreamReader stream, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        bool truncated = false;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = Math.Min(read, MaxOutputChars - text.Length);
            text.Append(buffer, 0, length);
            truncated |= length < read;
        }

        if (truncated && char.IsHighSurrogate(text[^1])) text.Length--;
        return new CapturedOutput(text.ToString(), truncated);
    }

    private readonly record struct CapturedOutput(string Text, bool Truncated);
}
