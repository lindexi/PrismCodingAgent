using System.Diagnostics;
using AgentLib.Coding.Settings;

namespace AgentLib.Coding.Tools.ExecuteCommands;

internal sealed class WinRemoteShellCommand(CodingRunContext context) : ICommandHandler
{
    private const string Topic = "winremoteshell";
    private const string Usage = """
        winremoteshell <客户端命令> [参数...]：将命令和参数传递给 Windows 沙盒客户端。
        winremoteshell ls [路径]：列出 Windows 沙盒目录。
        winremoteshell ps [--details] [--json]：列出 Windows 沙盒进程。
        支持 --server <地址>；省略时使用设置中的地址。
        """;

    string ICommandHandler.Topic => Topic;
    string ICommandHandler.Summary => "访问已启用的 Windows 沙盒。";
    string ICommandHandler.Usage => Usage;

    bool ICommandHandler.CanHandle(ReadOnlySpan<string> argv) => argv is [Topic, ..];

    Task<string> ICommandHandler.RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken)
    {
        var arguments = argv.Span[1..];
        string? subcommand = arguments.IsEmpty ? null : arguments[0];
        if (string.Equals(subcommand, "server", StringComparison.OrdinalIgnoreCase)
            || string.Equals(subcommand, "apply-update", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CommandResult.Format("参数错误", Topic, detail: "不支持服务端命令 server 和 apply-update。"));
        }

        if (context.Settings is not { IsWindowsSandboxEnabled: true } settings)
        {
            return Task.FromResult(CommandResult.Format("不可用", Topic, detail: "当前运行未启用 Windows 沙盒。"));
        }

        return CommandProcess.RunAsync(CreateStartInfo(settings, arguments), subcommand is null ? Topic : $"{Topic} {subcommand}",
            context.Session.Logger, cancellationToken);
    }

    internal static ProcessStartInfo CreateStartInfo(CodingChatShellSettings settings, ReadOnlySpan<string> arguments)
    {
        var startInfo = CommandProcess.CreateStartInfo(settings.WindowsSandboxToolPath);
        if (arguments is [string subcommand, ..] && !subcommand.StartsWith("-", StringComparison.Ordinal))
        {
            // 仅是因为存在 `--` 分割前后，避免加入到 `--` 之后，才进行此处理
            // 既不能加入到最前（在 verb command 之前），也不能加入到最后，只好取 subcommand 出来
            startInfo.ArgumentList.Add(subcommand);
            arguments = arguments[1..];
            if (!HasServerOption(arguments))
            {
                startInfo.ArgumentList.Add("--server");
                startInfo.ArgumentList.Add(settings.WindowsSandboxServerAddress);
            }
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static bool HasServerOption(ReadOnlySpan<string> arguments)
    {
        foreach (string argument in arguments)
        {
            if (argument == "--") break;
            if (argument == "--server" || argument?.StartsWith("--server=", StringComparison.Ordinal) == true) return true;
        }

        return false;
    }
}
