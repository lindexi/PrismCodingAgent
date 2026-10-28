using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class LocalizationTests
{
    [TestMethod]
    public void WhenReadingDefaultThenChineseTextIsAvailable()
    {
        Assert.AreEqual("设置", Lang.Default.SettingsNavigationText);
    }

    [TestMethod]
    public void WhenSwitchingToEnglishThenTextChanges()
    {
        Lang.SetCurrent("en");

        Assert.AreEqual("Settings", Lang.Current.SettingsNavigationText);
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenCurrentObjectRemainsStable()
    {
        var current = Lang.Current;
        Lang.SetCurrent(current.IetfLanguageTag == "en" ? "zh-Hans" : "en");

        Assert.AreSame(current, Lang.Current);
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenCurrentNotifiesBindings()
    {
        string target = Lang.Current.IetfLanguageTag == "en" ? "zh-Hans" : "en";
        var current = (INotifyPropertyChanged)Lang.Current;
        int notifications = 0;
        PropertyChangedEventHandler handler = (_, _) => notifications++;
        current.PropertyChanged += handler;
        try
        {
            Lang.SetCurrent(target);

            Assert.IsGreaterThan(0, notifications);
        }
        finally
        {
            current.PropertyChanged -= handler;
        }
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenExistingSettingsViewUpdates()
    {
        string target = Lang.Current.IetfLanguageTag == "en" ? "zh-Hans" : "en";
        var window = new Window { Content = new SettingsView { DataContext = new SettingsViewModel() } };
        window.Show();
        try
        {
            var title = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Lang.Current.SettingsNavigationText);

            Lang.SetCurrent(target);
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(Lang.Current.SettingsNavigationText, title.Text);
        }
        finally
        {
            window.Close();
        }
    }
}
