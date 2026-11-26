using System.ComponentModel;
using AgentLib.Logging;
using AgentLib.Tools;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.ExecuteCommands;

internal sealed class ExecuteCommandTool
{
    internal const string Name = "ExecuteCommand";

    private readonly string? _workspacePath;
    private readonly ISessionCopilotChatLogger? _logger;

    private ExecuteCommandTool(string? workspacePath, ISessionCopilotChatLogger? logger)
    {
        _workspacePath = workspacePath;
        _logger = logger;
    }

    internal static ToolRegistration CreateRegistration(string? workspacePath, ISessionCopilotChatLogger? logger) =>
        new(AIFunctionFactory.Create(new ExecuteCommandTool(workspacePath, logger).ExecuteAsync,
            new AIFunctionFactoryOptions
            {
                Name = Name,
                MarshalResult = static (result, _, _) => new ValueTask<object?>(result),
            }));

    [Description("运行内建命令并返回文本。查看工作区 Git 改动可传入 [\"git\", \"diff\"]，可追加 Git diff 参数。")]
    private Task<string> ExecuteAsync(
        [Description("命令和参数，每个元素是一个独立参数，不使用 shell 语法。")] string[] argv,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (argv is null || argv.Length == 0 || Array.Exists(argv, static argument => argument is null || argument.Contains('\0')))
        {
            return Task.FromResult(FormatError("参数错误", "argv 必须包含命令，且元素必须是不含空字符的字符串。"));
        }

        if (GitDiffCommand.CanHandle(argv, out ReadOnlyMemory<string> arguments))
        {
            IExecuteCommandHandler handler = new GitDiffCommand(_workspacePath, _logger);
            return handler.HandleAsync(arguments, cancellationToken);
        }

        return Task.FromResult(FormatError("参数错误", "不支持该命令。"));
    }

    internal static string FormatError(string status, string reason, string? command = null) =>
        $"<MetaData>\n状态: {status}\n{(command is null ? string.Empty : $"命令: {command}\n")}</MetaData>\n{reason}";

}
