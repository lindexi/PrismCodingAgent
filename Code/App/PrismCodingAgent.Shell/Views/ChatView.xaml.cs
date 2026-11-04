using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.CodingShell.Views;

public partial class ChatView : UserControl
{
    /// <summary>创建绑定共享聊天运行时的WPF输入视图。</summary>
    public ChatView()
    {
        InitializeComponent();
        MessageInputTextBox.PreviewKeyDown += MessageInputTextBox_OnKeyDown;

    }

    private void AbilitySelector_OnDropDownOpened(object sender, EventArgs e)
    {
        if (DataContext is ChatViewModel model)
        {
            model.RefreshAbilities();
        }
    }

    private async void MessageInputTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && DataContext is ChatViewModel pasteModel)
        {
            var session = pasteModel.NoticeSession;
            try
            {
                if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap)
                {
                    e.Handled = true;
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = new MemoryStream();
                    encoder.Save(stream);
                    pasteModel.TryAddImageAttachment($"clipboard-{DateTime.Now:yyyyMMdd-HHmmss}.png", stream.ToArray());
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.ExternalException)
            {
                e.Handled = true;
                await pasteModel.AddSystemNoticeAsync(session, Lang.Format(Lang.Current.ImagePasteFailed, exception.Message));
            }
            return;
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && DataContext is ChatViewModel model && model.SendCommand.CanExecute(null))
        {
            e.Handled = true;
            model.SendCommand.Execute(null);
        }
    }

    private void PreviewImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: ImageAttachmentViewModel attachment })
        {
            return;
        }

        ImageViewer.Open(this, attachment.Data, attachment.FileName);
    }

    private void RemoveImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ImageAttachmentViewModel attachment } && DataContext is ChatViewModel model)
        {
            model.RemoveImageAttachment(attachment);
        }
    }

    private async void AddImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel model)
        {
            return;
        }

        var session = model.NoticeSession;
        var picker = new OpenFileDialog
        {
            Title = Lang.Current.ImagePickerTitle,
            Multiselect = true,
            Filter = $"{Lang.Current.ImageType}|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp"
        };
        if (picker.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        foreach (string file in picker.FileNames)
        {
            string name = Path.GetFileName(file);
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(file);
                using var stream = new MemoryStream(bytes, writable: false);
                _ = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (!model.TryAddImageAttachment(name, bytes))
                {
                    await model.AddSystemNoticeAsync(session, Lang.Format(Lang.Current.ImageUnsupported, name), AgentLib.Model.CopilotChatNoticeLevel.Warning);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                await model.AddSystemNoticeAsync(session, Lang.Format(Lang.Current.ImageAttachFailed, name, exception.Message));
            }
        }
    }
}
