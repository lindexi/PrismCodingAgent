using AgentLib.Logging;

namespace AgentLib.Coding.ExecuteCommands;

internal sealed class GitDiffCommand(string? workspacePath, ISessionCopilotChatLogger logger) : ICommandHandler
{
    private const string CommandName = "git diff";
    private static readonly string[] ControlledOptions = ["--output", "--ext-diff", "--textconv"];

    string ICommandHandler.Topic => "git";
    string ICommandHandler.Usage => "git diff [参数]：查看当前工作区的 Git 改动。";

    bool ICommandHandler.CanHandle(ReadOnlySpan<string> argv) => argv is ["git", "diff", ..];

    Task<string> ICommandHandler.RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken)
    {
        if (workspacePath is null)
        {
            return Task.FromResult(CommandResult.Format("参数错误", CommandName, detail: "请先选择工作区。"));
        }

        var startInfo = CommandProcess.CreateStartInfo("git", workspacePath);
        string[] fixedArguments = ["--no-pager", "--no-optional-locks", "-c", "diff.autoRefreshIndex=false",
            "-c", "core.fsmonitor=false", "diff", "--no-ext-diff", "--no-textconv"];
        foreach (string argument in fixedArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (string argument in argv.Span[2..])
        {
            if (argument is null || argument.Contains('\0'))
            {
                return Task.FromResult(CommandResult.Format("参数错误", CommandName, detail: "Git 参数不能为 null 或包含 NUL 字符。"));
            }

            if (IsControlledOption(argument))
            {
                return Task.FromResult(CommandResult.Format("参数错误", CommandName,
                    detail: "此命令仅支持标准输出和 Git 内置差异比较。与选项同名的路径请加 ./ 前缀。"));
            }

            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        startInfo.Environment["GIT_ALLOW_PROTOCOL"] = string.Empty;
        return CommandProcess.RunAsync(startInfo, CommandName, logger, cancellationToken);
    }

    private static bool IsControlledOption(string argument)
    {
        // Git 接受长选项缩写，因此只比较等号前的选项名。
        int separator = argument.IndexOf('=');
        ReadOnlySpan<char> option = separator < 0 ? argument.AsSpan() : argument.AsSpan(0, separator);
        if (option.Length <= 2) return false;
        foreach (string controlledOption in ControlledOptions)
        {
            if (controlledOption.AsSpan().StartsWith(option, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
