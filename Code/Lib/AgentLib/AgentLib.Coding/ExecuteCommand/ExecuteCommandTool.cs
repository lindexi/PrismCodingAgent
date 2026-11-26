using System.ComponentModel;
using AgentLib.Logging;
using AgentLib.Tools;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Commands;

internal sealed class ExecuteCommandTool
{
    private readonly ICommandHandler[] _handlers;

    private ExecuteCommandTool(string? workspacePath, ISessionCopilotChatLogger logger)
    {
        _handlers = [new GitDiffCommand(workspacePath, logger)];
    }

    internal static ToolRegistration CreateRegistration(string? workspacePath, ISessionCopilotChatLogger logger)
    {
        var tool = new ExecuteCommandTool(workspacePath, logger);
        return new ToolRegistration(AIFunctionFactory.Create(tool.RunAsync, new AIFunctionFactoryOptions
        {
            Name = "ExecuteCommand",
            Description = "调用内建命令。",
            MarshalResult = static (result, _, _) => ValueTask.FromResult(result),
        }));
    }

    private Task<string> RunAsync(
        [Description("命令和参数按顺序逐项传入，含空格的参数仍是一个元素。")]
        string[] argv,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (argv is null || argv.Length == 0)
        {
            return Task.FromResult(CommandResult.Format("参数错误", detail: "argv 必须包含命令。"));
        }

        foreach (ICommandHandler handler in _handlers)
        {
            if (handler.CanHandle(argv))
            {
                return handler.RunAsync(argv, cancellationToken);
            }
        }

        return Task.FromResult(CommandResult.Format("参数错误", detail: "未找到匹配的内建命令。"));
    }
}
