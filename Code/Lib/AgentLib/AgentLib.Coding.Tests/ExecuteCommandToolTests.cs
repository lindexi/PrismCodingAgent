using AgentLib.Coding.Commands;
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

    private static AIFunction CreateTool()
    {
        string storagePath = Path.Combine(Path.GetTempPath(), $"command-tests-{Guid.NewGuid():N}");
        var session = new CopilotChatSession(new FileSessionStorage(storagePath));
        return (AIFunction)ExecuteCommandTool.CreateRegistration(null, session.Logger).Tool;
    }
}
