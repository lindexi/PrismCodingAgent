using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AgentLib.Logging;

namespace AgentLib.Coding.Tools.ExecuteCommands;

internal sealed class DotNetCommand(string? workspacePath, ISessionCopilotChatLogger logger) : ICommandHandler
{
    private const string Topic = "dotnet";
    private const string Search = "dotnet package search";
    private const string Versions = "package versions";
    private const string ListSdks = "dotnet sdk list";
    private const string Usage = """
        dotnet package search <查询词> [--take <正整数>] [--prerelease]：使用当前工作区生效的 NuGet 源搜索包。含空格的查询词须作为一个 argv 元素。
        package versions <包 ID>：使用当前工作区生效的 NuGet 源按包 ID 精确搜索；结果由源和 .NET CLI 决定，不保证包含全部版本。
        dotnet sdk list：列出已安装的 .NET SDK，无需工作区。
        """;

    string ICommandHandler.Topic => Topic;
    string ICommandHandler.Summary => "搜索 NuGet 包和版本，查看已安装的 .NET SDK。";
    string ICommandHandler.Usage => Usage;

    bool ICommandHandler.CanHandle(ReadOnlySpan<string> argv) => GetCommandName(argv) is not null;

    Task<string> ICommandHandler.RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryCreateStartInfo(argv.Span, workspacePath, out var startInfo, out string commandName, out string? error)
            ? CommandProcess.RunAsync(startInfo, commandName, logger, cancellationToken)
            : Task.FromResult(CommandResult.Format("参数错误", commandName, detail: error));
    }

    internal static bool TryCreateStartInfo(ReadOnlySpan<string> argv, string? workspacePath,
        [NotNullWhen(true)] out ProcessStartInfo? startInfo, out string commandName, out string? error)
    {
        startInfo = null;
        commandName = GetCommandName(argv) ?? Topic;
        error = null;
        var arguments = new List<string>();

        switch (commandName)
        {
            case ListSdks:
                if (argv.Length != 3) return Fail("dotnet sdk list 不接受额外参数。", out error);
                arguments.Add("--list-sdks");
                break;
            case Versions:
                if (argv is not [_, _, string packageId] || !IsSearchValue(packageId) || packageId.Any(char.IsWhiteSpace))
                {
                    return Fail("package versions 只接受一个有效的包 ID。", out error);
                }
                arguments.AddRange(["package", "search", packageId, "--exact-match"]);
                break;
            case Search:
                if (argv is not [_, _, _, string query, ..] || !IsSearchValue(query))
                {
                    return Fail("请提供一个有效查询词；不能为纯空白、包含 NUL 字符，或以选项或响应文件形式传入。", out error);
                }
                arguments.AddRange(["package", "search", query]);
                bool seenTake = false;
                bool seenPrerelease = false;
                for (int i = 4; i < argv.Length; i++)
                {
                    switch (argv[i])
                    {
                        case "--take" when !seenTake:
                            if (++i >= argv.Length || argv[i] is not string count || count.Contains('\0')
                                || !int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out int take) || take <= 0)
                            {
                                return Fail("--take 必须紧跟一个有效的正整数。", out error);
                            }
                            seenTake = true;
                            arguments.Add("--take");
                            arguments.Add(count);
                            break;
                        case "--prerelease" when !seenPrerelease:
                            seenPrerelease = true;
                            arguments.Add("--prerelease");
                            break;
                        default:
                            return Fail("只支持 --take <正整数> 和 --prerelease，且每个选项最多指定一次。", out error);
                    }
                }
                break;
            default:
                return Fail("未找到匹配的 .NET 内建命令。", out error);
        }

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            if (commandName != ListSdks) return Fail("请先选择工作区。", out error);
            workspacePath = null;
        }

        startInfo = CommandProcess.CreateStartInfo(DotNetCliProcess.FileName, workspacePath);
        DotNetCliProcess.ConfigureEnvironment(startInfo);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return true;
    }

    private static string? GetCommandName(ReadOnlySpan<string> argv) => argv switch
    {
        [Topic, "package", "search", ..] => Search,
        ["package", "versions", ..] => Versions,
        [Topic, "sdk", "list", ..] => ListSdks,
        _ => null,
    };

    private static bool IsSearchValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0')) return false;
        // CLI 可能把 @ 文件展开为额外参数；斜杠形式也可表示帮助选项。
        ReadOnlySpan<char> trimmed = value.AsSpan().TrimStart();
        return trimmed[0] is not ('-' or '@')
            && !trimmed.Equals("/?", StringComparison.Ordinal)
            && !trimmed.Equals("/h", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
