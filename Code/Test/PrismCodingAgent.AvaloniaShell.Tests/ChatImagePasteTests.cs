using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Moq;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ChatImagePasteTests
{
    [TestMethod]
    public async Task PastingImageFilesAddsAttachmentsWithoutReplacingTheDraftAsync()
    {
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aU5sAAAAASUVORK5CYII=");
        var clipboardData = new DataTransfer();
        clipboardData.Add(DataTransferItem.CreateFile(CreateFile("notes.txt", [])));
        clipboardData.Add(DataTransferItem.CreateFile(CreateFile("first.PNG", png)));
        clipboardData.Add(DataTransferItem.CreateFile(CreateFile("second.png", png)));
        clipboardData.Add(DataTransferItem.CreateText("clipboard text"));

        using var model = new ChatViewModel { InputText = "draft" };
        var view = new ChatView { DataContext = model };
        var window = new Window { Content = view };
        var attached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        model.PendingImages.CollectionChanged += (_, _) =>
        {
            if (model.PendingImages.Count == 2)
            {
                attached.TrySetResult();
            }
        };
        try
        {
            window.Show();
            var input = view.FindControl<TextBox>("MessageInputTextBox")!;
            input.Focus();
            input.SelectAll();
            await window.Clipboard!.SetDataAsync(clipboardData);

            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
            await attached.Task.WaitAsync(TimeSpan.FromSeconds(5));

            CollectionAssert.AreEqual(new[] { "first.PNG", "second.png" }, model.PendingImages.Select(image => image.FileName).ToArray());
            Assert.AreEqual("draft", input.Text);
        }
        finally
        {
            window.Close();
        }
    }

    private static IStorageFile CreateFile(string name, byte[] data)
    {
        var file = new Mock<IStorageFile>(MockBehavior.Strict);
        file.SetupGet(item => item.Name).Returns(name);
        file.Setup(item => item.OpenReadAsync()).ReturnsAsync(() => new MemoryStream(data, writable: false));
        return file.Object;
    }
}
