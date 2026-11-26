using AgentLib.Coding.ExecuteCommands;
using AgentLib.Coding.Settings;
using AgentLib.Logging.Sessions;
using AgentLib.Model;
using AgentLib.Tools;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class ExecuteCommandToolTests
{
    [TestMethod]
    public async Task GitDiffWithoutWorkspaceReturnsWorkspaceError()
    {
        AIFunction tool = CreateTool();

        object? result = await tool.InvokeAsync(new AIFunctionArguments { ["argv"] = new[] { "git", "diff" } });

        StringAssert.Contains(Assert.IsInstanceOfType<string>(result), "请先选择工作区");
    }

    [TestMethod]
    [DataRow("git diff", "--stat")]
    [DataRow("git", "diff-tree")]
    public async Task InexactCommandReturnsAnUnknownCommandError(string command, string subcommand)
    {
        AIFunction tool = CreateTool();

        object? result = await tool.InvokeAsync(new AIFunctionArguments { ["argv"] = new[] { command, subcommand } });

        StringAssert.Contains(Assert.IsInstanceOfType<string>(result), "未找到匹配的内建命令");
    }

    [TestMethod]
    public async Task ResponsesReturnsUnquotedToolText()
    {
        AIFunction tool = CreateTool();
        var call = ResponseItem.CreateFunctionCallItem("call", tool.Name,
            BinaryData.FromString("""{"argv":["git","diff"]}"""));

        var result = await ResponsesToolHelper.InvokeAsync(tool, call);

        StringAssert.StartsWith(result.FunctionOutput, "<MetaData>");
    }

    [TestMethod]
    public void ToolContextDoesNotAppearInModelParameters()
    {
        AIFunction tool = CreateTool();

        string[] names = tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();

        CollectionAssert.AreEqual(new[] { "argv" }, names);
    }

    [TestMethod]
    public async Task RemoteQueryIsUnavailableWhenDisabledInGlobalSettings()
    {
        var tool = CreateTool(new CodingChatShellSettings { IsWindowsSandboxEnabled = false });

        var result = await InvokeAsync(tool, "winremoteshell", "ls");

        StringAssert.StartsWith(Assert.IsInstanceOfType<string>(result), $"<MetaData>{Environment.NewLine}状态: 不可用");
    }

    [TestMethod]
    public async Task RemoteQueryUsesTheClientSpecifiedByGlobalSettings()
    {
        string missingClient = Path.Combine(Path.GetTempPath(), $"missing-winremoteshell-{Guid.NewGuid():N}.exe");
        var tool = CreateTool(new CodingChatShellSettings { WindowsSandboxToolPath = missingClient });

        var result = await InvokeAsync(tool, "winremoteshell", "ps", "--json");

        StringAssert.StartsWith(Assert.IsInstanceOfType<string>(result), $"<MetaData>{Environment.NewLine}状态: 启动失败");
    }

    [TestMethod]
    [DataRow("server")]
    [DataRow("apply-update")]
    [DataRow("SERVER")]
    public async Task RemoteCommandRejectsServerSubcommands(string subcommand)
    {
        var tool = CreateTool(new CodingChatShellSettings());

        var result = await InvokeAsync(tool, "winremoteshell", subcommand);

        StringAssert.StartsWith(Assert.IsInstanceOfType<string>(result), $"<MetaData>{Environment.NewLine}状态: 参数错误");
    }

    [TestMethod]
    [DataRow("exec")]
    [DataRow("update")]
    [DataRow("future-command")]
    [DataRow("--help")]
    public async Task RemoteCommandForwardsOtherSubcommands(string subcommand)
    {
        string missingClient = Path.Combine(Path.GetTempPath(), $"missing-winremoteshell-{Guid.NewGuid():N}.exe");
        var tool = CreateTool(new CodingChatShellSettings { WindowsSandboxToolPath = missingClient });

        var result = await InvokeAsync(tool, "winremoteshell", subcommand);

        StringAssert.StartsWith(Assert.IsInstanceOfType<string>(result), $"<MetaData>{Environment.NewLine}状态: 启动失败");
    }

    [TestMethod]
    [DataRow(new[] { "ps", "--json" }, new[] { "ps", "--server", "default.example:12399", "--json" })]
    [DataRow(new[] { "ls", "path with spaces", "--server", "other.example:12399" },
        new[] { "ls", "path with spaces", "--server", "other.example:12399" })]
    [DataRow(new[] { "ps", "--server=other.example:12399" }, new[] { "ps", "--server=other.example:12399" })]
    [DataRow(new[] { "ls", "--", "--server" }, new[] { "ls", "--server", "default.example:12399", "--", "--server" })]
    [DataRow(new[] { "exec", "--", "dotnet.exe", "--server", "server" },
        new[] { "exec", "--server", "default.example:12399", "--", "dotnet.exe", "--server", "server" })]
    [DataRow(new[] { "--help" }, new[] { "--help" })]
    public void RemoteCommandUsesConfiguredServerOnlyWhenNotSpecified(string[] arguments, string[] expected)
    {
        var settings = new CodingChatShellSettings { WindowsSandboxServerAddress = "default.example:12399" };

        var startInfo = WinRemoteShellCommand.CreateStartInfo(settings, arguments);

        CollectionAssert.AreEqual(expected, startInfo.ArgumentList.ToArray());
    }

    [TestMethod]
    public async Task RemoteHelpIsAvailableOnDemand()
    {
        AIFunction tool = CreateTool();

        var result = await InvokeAsync(tool, "help", "winremoteshell");

        StringAssert.Contains(Assert.IsInstanceOfType<string>(result), "winremoteshell ps [--details] [--json]");
    }

    private static ValueTask<object?> InvokeAsync(AIFunction tool, params string[] argv) =>
        tool.InvokeAsync(new AIFunctionArguments { ["argv"] = argv });

    private static AIFunction CreateTool(CodingChatShellSettings? settings = null)
    {
        string storagePath = Path.Combine(Path.GetTempPath(), $"command-tests-{Guid.NewGuid():N}");
        var session = new CopilotChatSession(new FileSessionStorage(storagePath));
        var context = new CodingToolContext { Session = session, Settings = settings };
        return (AIFunction)ExecuteCommandTool.CreateRegistration(context).Tool;
    }
}
