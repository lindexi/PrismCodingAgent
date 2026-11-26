using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using AgentLib.Coding.ExecuteCommands;
using AgentLib.Tools;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class ExecuteCommandTests
{
    [TestMethod]
    [DataRow("git", "different")]
    [DataRow("git diff", "--stat")]
    [DataRow("Git", "diff")]
    public async Task InvokeAsync_ShouldMatchWholeTokens(string first, string second)
    {
        var result = await InvokeAsync(CreateTool(), first, second);

        StringAssert.Contains(result, "不支持该命令");
    }

    [TestMethod]
    [DataRow("--output")]
    [DataRow("--output=result.patch")]
    [DataRow("--out=result.patch")]
    [DataRow("--ext-diff")]
    [DataRow("--textconv")]
    public async Task InvokeAsync_ShouldRejectOptionsThatEnableSideEffects(string option)
    {
        var result = await InvokeAsync(CreateTool("unused"), "git", "diff", option);

        StringAssert.StartsWith(result, "<MetaData>\n状态: 参数错误\n命令: git diff\n");
    }

    [TestMethod]
    public async Task InvokeAsync_ShouldReturnPlainTextToResponses()
    {
        AIFunction function = CreateTool();
        var call = ResponseItem.CreateFunctionCallItem("call-1", function.Name,
            BinaryData.FromString("{\"argv\":[\"git\",\"diff\"]}"));

        var result = await ResponsesToolHelper.InvokeAsync(function, call);

        Assert.AreEqual("<MetaData>\n状态: 参数错误\n命令: git diff\n</MetaData>\ngit diff 需要当前工作区。",
            result.FunctionOutput);
    }

    [TestMethod]
    public void CreateRegistration_ShouldExposeArrayWithoutCancellationParameter()
    {
        JsonElement properties = CreateTool().JsonSchema.GetProperty("properties");

        Assert.AreEqual("array", properties.EnumerateObject().Single(property => property.Name == "argv")
            .Value.GetProperty("type").GetString());
        Assert.HasCount(1, properties.EnumerateObject().ToArray());
    }

    [TestMethod]
    public async Task InvokeAsync_ShouldPreserveGitExitCodeAndPathsWithSpaces()
    {
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            });
            var version = await git!.StandardOutput.ReadToEndAsync();
            await git.WaitForExitAsync();
            Console.WriteLine(version);
        }
        catch (Win32Exception exception)
        {
            Assert.Inconclusive($"Git is not available: {exception.Message}");
        }

        string directory = Directory.CreateTempSubdirectory("execute-command-").FullName;
        Console.WriteLine($"Git diff fixture: {directory}");
        await File.WriteAllTextAsync(Path.Combine(directory, "before file.txt"), "before\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "after file.txt"), "after\n");

        var result = await InvokeAsync(CreateTool(directory), "git", "diff", "--no-index", "--", "before file.txt", "after file.txt");

        StringAssert.Contains(result, "退出码: 1\n</MetaData>\ndiff --git a/before file.txt b/after file.txt");
    }

    private static AIFunction CreateTool(string? workspacePath = null) =>
        (AIFunction)ExecuteCommandTool.CreateRegistration(workspacePath, logger: null).Tool;

    private static async Task<string> InvokeAsync(AIFunction function, params string[] argv) =>
        (string)(await function.InvokeAsync(new AIFunctionArguments { ["argv"] = argv }))!;
}
