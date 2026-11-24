using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class FavoritePromptsScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FavoritePromptsRenderInChineseAndApplyFullTextAsync()
    {
        Lang.SetCurrent("zh-Hans");
        const string configuration = """{"PrimaryModel":"Preview model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Preview model","ModelId":"preview","Provider":"openai"}]}]}""";
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext, configuration);
        var prompts = new[]
        {
            "请审查以下代码，优先指出会影响正确性的问题。\n先说明问题发生的条件，再说明用户会看到什么。\n建议尽可能小，保持现有公开接口不变。\n如有边界条件，请给出具体示例。\n最后列出需要补充的测试。\n不要直接修改代码，先给出审查结论。",
            "根据当前改动生成一份简短的提交说明，先概括目的，再列出主要变化。",
            "把这段技术说明改写成面向用户的文档。保留必要的前置条件，并给出一个可操作的示例。",
            "解释这个测试为什么偶尔失败。区分已验证的事实和仍需验证的假设。",
        };
        string path = Path.Combine(session.DirectoryPath, "AppData", "Prompts.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(prompts));
        var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
        var panel = chat.FindControl<FavoritePromptsView>("FavoritePromptsPanel")!;
        var list = panel.FindControl<ListBox>("PromptList")!;
        var apply = panel.FindControl<Button>("ApplyPromptButton")!;

        session.Capture("favorite-prompts-entry");
        const string draft = "Existing draft: ";
        session.Model.ChatViewModel.InputText = draft;
        Dispatcher.UIThread.RunJobs();
        chat.FindControl<TextBox>("MessageInputTextBox")!.CaretIndex = 3;
        session.Click(chat.FindControl<Button>("OpenFavoritePromptsButton")!);
        await WaitUntilAsync(() => list.ItemCount == prompts.Length);
        Assert.IsFalse(apply.IsEnabled);
        list.SelectedIndex = 0;
        session.Capture("favorite-prompts-selected");
        Assert.IsTrue(apply.IsEnabled);
        var row = list.ContainerFromIndex(0)!;
        var editor = row.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.IsTrue(editor.IsReadOnly);
        Assert.IsFalse(editor.Text!.Contains("先给出审查结论", StringComparison.Ordinal));

        var edit = row.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button)?.ToString() == "编辑");
        session.Click(edit);
        session.Capture("favorite-prompts-editing");
        editor = list.ContainerFromIndex(0)!.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.IsFalse(editor.IsReadOnly);
        Assert.AreEqual(prompts[0], editor.Text);
        var cancel = list.ContainerFromIndex(0)!.GetVisualDescendants().OfType<Button>()
            .Single(button => ToolTip.GetTip(button)?.ToString() == "取消");
        session.Click(cancel);
        session.Click(apply);
        Assert.AreEqual(draft + prompts[0], session.Model.ChatViewModel.InputText);
        Assert.IsFalse(panel.IsVisible);

        session.Click(chat.FindControl<Button>("OpenFavoritePromptsButton")!);
        await WaitUntilAsync(() => list.ItemCount == prompts.Length);
        session.Click(panel.FindControl<Button>("NewPromptButton")!);
        session.Capture("favorite-prompts-new");
        Assert.AreEqual(prompts.Length + 1, list.ItemCount);
        var newRow = list.ContainerFromIndex(prompts.Length)!;
        newRow.GetVisualDescendants().OfType<TextBox>().Single().Text = "新建的提示词";
        Dispatcher.UIThread.RunJobs();
        session.Click(newRow.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button)?.ToString() == "完成"));
        await WaitUntilAsync(() => apply.IsEnabled);
        var persisted = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(path));
        Assert.AreEqual("新建的提示词", persisted![^1]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
}
