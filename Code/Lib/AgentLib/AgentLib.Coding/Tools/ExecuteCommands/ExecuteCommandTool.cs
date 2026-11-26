using System.ComponentModel;
using AgentLib.Tools;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tools.ExecuteCommands;

internal sealed class ExecuteCommandTool
{
    private readonly ICommandHandler[] _handlers;

    private ExecuteCommandTool(CodingRunContext context)
    {
        _handlers =
        [
            new GitDiffCommand(context.WorkspacePath, context.Session.Logger),
            new WinRemoteShellCommand(context),
            new DotNetCommand(context.WorkspacePath, context.Session.Logger),
        ];
    }

    internal static ToolRegistration CreateRegistration(CodingRunContext context)
    {
        var tool = new ExecuteCommandTool(context);
        return new ToolRegistration(AIFunctionFactory.Create(tool.RunAsync, new AIFunctionFactoryOptions
        {
            Name = "ExecuteCommand",
            Description = "执行命令。输入 `help` 查询用法。",
            MarshalResult = static (result, _, _) => ValueTask.FromResult(result),
        }));
    }

    private Task<string> RunAsync(
        [Description("命令、子命令和参数逐项传入，含空格的参数不拆分。")]
        string[]? argv = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (argv is null || argv.Length == 0)
        {
            return Task.FromResult(GetHelp([]));
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
            var summaries = _handlers.Select(handler => $"{handler.Topic}：{handler.Summary}");
            return CommandResult.Format("成功", "help", detail: string.Join(Environment.NewLine,
                summaries.Prepend("在 `help` 后加上主题名查看详细用法。")));
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
