using PrismCodingAgent.CodingShell.Services.Settings;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class FavoritePromptStoreTests
{
    [TestMethod]
    public async Task LoadReturnsEmptyWhenFileDoesNotExistAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Prompts.json");

        Assert.IsEmpty(await FavoritePromptStore.LoadAsync(path));
    }

    [TestMethod]
    public async Task SaveReplacesExistingFileWithoutLeavingTemporaryFilesAsync()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "Prompts.json");
        string[] prompts = ["First line\nSecond line", "Another prompt"];
        await FavoritePromptStore.SaveAsync(path, ["Old prompt"]);

        await FavoritePromptStore.SaveAsync(path, prompts);

        CollectionAssert.AreEqual(prompts, await FavoritePromptStore.LoadAsync(path));
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task SaveRemovesTemporaryFileWhenReplacementFailsAsync()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Directory.CreateDirectory(Path.Combine(directory, "Prompts.json")).FullName;

        try
        {
            await FavoritePromptStore.SaveAsync(path, ["Prompt"]);
            Assert.Fail("Replacing a directory with the prompts file should fail.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.IsEmpty(Directory.GetFiles(directory));
        }
    }

    [TestMethod]
    [DataRow("[")]
    [DataRow("null")]
    [DataRow("[null]")]
    public async Task LoadReturnsEmptyWhenJsonIsInvalidAsync(string json)
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Prompts.json");
        await File.WriteAllTextAsync(path, json);

        Assert.IsEmpty(await FavoritePromptStore.LoadAsync(path));
    }

    [TestMethod]
    public async Task LoadReturnsEmptyWhenPathCannotBeReadAsFileAsync()
    {
        string path = Directory.CreateTempSubdirectory().FullName;

        Assert.IsEmpty(await FavoritePromptStore.LoadAsync(path));
    }
}
