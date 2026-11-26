using System.ComponentModel;
using AgentLib.Tools;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.ExecuteCommands;

internal sealed class ExecuteCommandTool
{
    private readonly ICommandHandler[] _handlers;

    private ExecuteCommandTool(CodingRunContext context)
    {
        _handlers = [new GitDiffCommand(context.WorkspacePath, context.Session.Logger), new WinRemoteShellCommand(context)];
    }

    internal static ToolRegistration CreateRegistration(CodingRunContext context)
    {
        var tool = new ExecuteCommandTool(context);
        return new ToolRegistration(AIFunctionFactory.Create(tool.RunAsync, new AIFunctionFactoryOptions
        {
            Name = "ExecuteCommand",
            Description = "调用内建命令。传入 [\"help\"] 查看目录；在 help 后追加主题查看具体用法。",
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

        if (argv[0] == "help")
        {
            return Task.FromResult(GetHelp(argv.AsSpan(1)));
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

    private string GetHelp(ReadOnlySpan<string> topics)
    {
        if (topics.IsEmpty)
        {
            var names = new string[_handlers.Length];
            for (int i = 0; i < _handlers.Length; i++) names[i] = _handlers[i].Topic;
            return CommandResult.Format("成功", "help", detail: string.Join(Environment.NewLine, names));
        }

        if (topics is [string topic])
        {
            foreach (ICommandHandler handler in _handlers)
            {
                if (handler.Topic == topic) return CommandResult.Format("成功", "help", detail: handler.Usage);
            }
        }

        return CommandResult.Format("参数错误", "help", detail: "请从 [\"help\"] 返回的目录中选择一个主题。");
    }
}
