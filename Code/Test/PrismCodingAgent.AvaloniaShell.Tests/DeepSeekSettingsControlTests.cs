using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.ViewModels;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;
using SettingsView = PrismCodingAgent.CodingShell.Views.SettingsView;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class DeepSeekSettingsControlTests
{
    [TestMethod]
    public void WhenEditingKeyThenSettingsBindingUpdates()
    {
        var viewModel = new SettingsViewModel();
        var view = new SettingsView { DataContext = viewModel };
        var input = view.FindControl<TextBox>("DeepSeekApiKeyInput")!;

        input.Text = "edited-key";
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual("edited-key", viewModel.DeepSeekApiKey);
    }

    [TestMethod]
    public void WhenDisplayingKeyThenInputIsMasked()
    {
        var view = new SettingsView { DataContext = new SettingsViewModel() };

        Assert.AreNotEqual('\0', view.FindControl<TextBox>("DeepSeekApiKeyInput")!.PasswordChar);
    }

    [TestMethod]
    [DataRow("en", "DeepSeek API key")]
    [DataRow("zh-Hans", "DeepSeek API 密钥")]
    public void WhenSwitchingLanguageThenDeepSeekLabelIsLocalized(string language, string expected)
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        var window = new Window { Content = new SettingsView { DataContext = new SettingsViewModel() } };
        window.Show();
        try
        {
            Lang.SetCurrent(language);
            Dispatcher.UIThread.RunJobs();

            Assert.IsTrue(window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == expected));
        }
        finally
        {
            Lang.SetCurrent(originalLanguage);
            window.Close();
        }
    }
}
