using PrismCodingAgent.AvaloniaShell.Infrastructure;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CodingChatStartupOptionsTests
{
    [TestMethod]
    public void NoArgumentsShouldUseProductionDirectory()
    {
        var options = CodingChatStartupOptions.FromArguments(null);
        Assert.AreEqual(CodingChatRoomPaths.CreateForCurrentUser().RootDirectory, options.Paths.RootDirectory);
    }

    [TestMethod]
    [DataRow("--debug-onboarding")]
    [DataRow("--DEBUG-ONBOARDING")]
    public void DebugArgumentShouldEnableOnboarding(string argument)
    {
        var options = CodingChatStartupOptions.FromArguments([argument]);
        Assert.IsTrue(options.IsOnboardingDebug);
    }

    [TestMethod]
    public void SimilarArgumentShouldNotEnableDebugMode()
    {
        var options = CodingChatStartupOptions.FromArguments(["--debug-onboarding=false"]);
        Assert.IsFalse(options.IsOnboardingDebug);
    }

    [TestMethod]
    public void DebugDirectoryShouldBeOutsideProductionDirectory()
    {
        var options = CodingChatStartupOptions.FromArguments(["--debug-onboarding"]);
        string production = CodingChatRoomPaths.CreateForCurrentUser().RootDirectory + Path.DirectorySeparatorChar;
        Assert.IsFalse(options.Paths.RootDirectory.StartsWith(production, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void EachDebugLaunchShouldUseNewDirectory()
    {
        var first = CodingChatStartupOptions.FromArguments(["--debug-onboarding"]);
        var second = CodingChatStartupOptions.FromArguments(["--debug-onboarding"]);
        Assert.AreNotEqual(first.Paths.RootDirectory, second.Paths.RootDirectory);
    }

    [TestMethod]
    public void ExplicitDirectoryShouldUseFirstOccurrence()
    {
        string first = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = CodingChatStartupOptions.FromArguments(["--DATA-DIRECTORY", first, "--data-directory", "ignored"]);
        Assert.AreEqual(CodingChatRoomPaths.Create(first).RootDirectory, options.Paths.RootDirectory);
    }

    [TestMethod]
    [DataRow(new string[] { "--data-directory" })]
    [DataRow(new string[] { "--data-directory", "" })]
    [DataRow(new string[] { "--data-directory", " " })]
    [DataRow(new string[] { "--data-directory", "--data-directory", "ignored" })]
    public void MissingDirectoryShouldRejectArguments(string[] arguments)
    {
        Assert.ThrowsExactly<ArgumentException>(() => CodingChatStartupOptions.FromArguments(arguments));
    }

    [TestMethod]
    [DataRow(new string[] { "--data-directory", "ignored", "--debug-onboarding" })]
    [DataRow(new string[] { "--debug-onboarding", "--data-directory" })]
    [DataRow(new string[] { "--debug-onboarding", "--debug-onboarding" })]
    public void DebugShouldPreserveIsolatedDirectoryPriority(string[] arguments)
    {
        var options = CodingChatStartupOptions.FromArguments(arguments);
        Assert.IsTrue(options.Paths.RootDirectory.StartsWith(
            Path.Combine(Path.GetTempPath(), "CodingChatRoom-OnboardingDebug") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AllApplicationDataShouldUseDebugDirectory()
    {
        var paths = CodingChatStartupOptions.FromArguments(["--debug-onboarding"]).Paths;
        string[] locations =
        [
            paths.ConfigurationFile.FullName, paths.ShellSettingsFile.FullName, paths.WorkTasksFile.FullName,
            paths.LogDirectory, paths.SessionDirectory.FullName, paths.AbilitiesDirectory.FullName,
        ];
        Assert.IsTrue(locations.All(path => path.StartsWith(paths.RootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
    }
}
