using AgentLib.Coding.ExecuteCommands;

namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class DotNetCommandTests
{
    [TestMethod]
    [DataRow(new[] { "dotnet", "package", "search", "Example" }, new[] { "package", "search", "Example" })]
    [DataRow(new[] { "dotnet", "package", "search", "two words; quoted \"text\"", "--take", "05", "--prerelease" },
        new[] { "package", "search", "two words; quoted \"text\"", "--take", "05", "--prerelease" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--prerelease", "--take", "1" },
        new[] { "package", "search", "Example", "--prerelease", "--take", "1" })]
    [DataRow(new[] { "package", "versions", "Example.Package" }, new[] { "package", "search", "Example.Package", "--exact-match" })]
    [DataRow(new[] { "dotnet", "sdk", "list" }, new[] { "--list-sdks" })]
    public void CommandMapsToIndividualCliArguments(string[] argv, string[] expected)
    {
        Assert.IsTrue(DotNetCommand.TryCreateStartInfo(argv, Path.GetTempPath(), out var startInfo, out _, out _));

        CollectionAssert.AreEqual(expected, startInfo.ArgumentList.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "dotnet", "package", "search", "Example" })]
    [DataRow(new[] { "package", "versions", "Example" })]
    [DataRow(new[] { "dotnet", "sdk", "list" })]
    public void CommandUsesWorkspaceDirectoryWhenPresent(string[] argv)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "workspace with spaces");

        Assert.IsTrue(DotNetCommand.TryCreateStartInfo(argv, workspace, out var startInfo, out _, out _));

        Assert.AreEqual(workspace, startInfo.WorkingDirectory);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(" \t")]
    public void SdkListDoesNotRequireWorkspace(string? workspace)
    {
        Assert.IsTrue(DotNetCommand.TryCreateStartInfo(["dotnet", "sdk", "list"], workspace, out var startInfo, out _, out _));

        Assert.AreEqual(string.Empty, startInfo.WorkingDirectory);
    }

    [TestMethod]
    public void CommandLaunchesDotNetViaPathWithoutShell()
    {
        Assert.IsTrue(DotNetCommand.TryCreateStartInfo(["dotnet", "sdk", "list"], null, out var startInfo, out _, out _));

        Assert.AreEqual("dotnet", startInfo.FileName);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.AreEqual(string.Empty, startInfo.Arguments);
    }

    [TestMethod]
    [DataRow("DOTNET_NOLOGO")]
    [DataRow("DOTNET_CLI_TELEMETRY_OPTOUT")]
    public void CommandSetsDotNetEnvironmentVariable(string variable)
    {
        Assert.IsTrue(DotNetCommand.TryCreateStartInfo(["dotnet", "sdk", "list"], null, out var startInfo, out _, out _));

        Assert.AreEqual("1", startInfo.Environment[variable]);
    }

    [TestMethod]
    [DataRow(new[] { "dotnet", "package", "search", "" })]
    [DataRow(new[] { "dotnet", "package", "search", " \t" })]
    [DataRow(new[] { "dotnet", "package", "search", "ex\0ample" })]
    [DataRow(new[] { "dotnet", "package", "search", "--source" })]
    [DataRow(new[] { "dotnet", "package", "search", "  -q" })]
    [DataRow(new[] { "dotnet", "package", "search", "@query.rsp" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--source", "elsewhere" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--configfile", "elsewhere" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take=5" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", "0" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", "-1" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", "1.5" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", " 5" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", "5\0" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", "2147483648" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--take", "1", "--take", "2" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--prerelease", "--prerelease" })]
    [DataRow(new[] { "dotnet", "package", "search", "Example", "--prerelease\0" })]
    [DataRow(new[] { "package", "versions", " " })]
    [DataRow(new[] { "package", "versions", "Example\0" })]
    [DataRow(new[] { "package", "versions", "two words" })]
    [DataRow(new[] { "package", "versions", "-Example" })]
    [DataRow(new[] { "package", "versions", "Example", "--prerelease" })]
    [DataRow(new[] { "dotnet", "sdk", "list", "" })]
    public void InvalidArgumentsDoNotCreateProcessStartInfo(string[] argv)
    {
        bool created = DotNetCommand.TryCreateStartInfo(argv, Path.GetTempPath(), out var startInfo, out _, out _);

        Assert.IsFalse(created);
        Assert.IsNull(startInfo);
    }
}
