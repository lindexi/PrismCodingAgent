using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Abilities;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ChatOptionLocalizationTests
{
    private static void SwitchLanguage() =>
        Lang.SetCurrent(Lang.Current.IetfLanguageTag == "en" ? "zh-Hans" : "en");
    [TestMethod]
    public void WhenSwitchingLanguageThenReasoningOptionsKeepTheirIdentity()
    {
        using var chat = new ChatViewModel();
        var options = chat.AvailableReasoningEfforts.ToArray();
        int collectionChanges = 0;
        chat.AvailableReasoningEfforts.CollectionChanged += (_, _) => collectionChanges++;

        SwitchLanguage();

        CollectionAssert.AreEqual(options, chat.AvailableReasoningEfforts.ToArray());
        Assert.AreEqual(0, collectionChanges);
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenAbilityOptionsKeepTheirIdentity()
    {
        using var chat = new ChatViewModel();
        var options = chat.AvailableAbilities.ToArray();
        int collectionChanges = 0;
        chat.AvailableAbilities.CollectionChanged += (_, _) => collectionChanges++;

        SwitchLanguage();

        CollectionAssert.AreEqual(options, chat.AvailableAbilities.ToArray());
        Assert.AreEqual(0, collectionChanges);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void WhenSwitchingLanguageThenSelectedReasoningReferenceIsPreserved(int index)
    {
        using var chat = new ChatViewModel();
        var selected = chat.AvailableReasoningEfforts[index];
        chat.SelectedReasoningEffort = selected;

        SwitchLanguage();

        Assert.AreSame(selected, chat.SelectedReasoningEffort);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void WhenSwitchingLanguageThenSelectedAbilityReferenceIsPreserved(int index)
    {
        using var chat = new ChatViewModel();
        var selected = chat.AvailableAbilities[index];
        chat.SelectedAbility = selected;

        SwitchLanguage();

        Assert.AreSame(selected, chat.SelectedAbility);
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenBusinessSelectionsAreNotNotified()
    {
        using var chat = new ChatViewModel();
        chat.SelectedReasoningEffort = chat.AvailableReasoningEfforts[3];
        chat.SelectedAbility = chat.AvailableAbilities[1];
        var changes = new List<string?>();
        chat.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        SwitchLanguage();

        CollectionAssert.DoesNotContain(changes, nameof(ChatViewModel.SelectedReasoningEffort));
        CollectionAssert.DoesNotContain(changes, nameof(ChatViewModel.SelectedAbility));
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenExistingOptionNamesUpdate()
    {
        using var chat = new ChatViewModel();
        var options = chat.AvailableReasoningEfforts.ToArray();
        var abilities = chat.AvailableAbilities.ToArray();

        SwitchLanguage();

        CollectionAssert.AreEqual(
            Lang.Current.IetfLanguageTag == "en"
                ? new[] { "Default", "Low", "Medium", "High", "Coding", "Compress with instructions" }
                : new[] { "默认", "低", "中", "高", "编程", "按照指令进行压缩" },
            options.Select(option => option.DisplayName).Concat(abilities.Select(option => option.DisplayName)).ToArray());
    }

    [TestMethod]
    [DataRow("custom")]
    [DataRow("compress")]
    public void WhenSwitchingLanguageThenUserProvidedAbilityNameIsPreserved(string id)
    {
        var option = new AbilityOptionViewModel(new AbilityDefinition(id, "User supplied name", null, null));
        using var chat = new ChatViewModel();
        chat.AvailableAbilities.Add(option);
        chat.SelectedAbility = option;

        Lang.SetCurrent("en");

        Assert.AreEqual("User supplied name", option.DisplayName);
        Assert.AreSame(option, chat.SelectedAbility);
    }

    [TestMethod]
    public void WhenSwitchingLanguageThenExistingChatOptionTemplatesUpdate()
    {
        using var chat = new ChatViewModel();
        var view = new ChatView { DataContext = chat };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var reasoning = view.FindControl<ComboBox>("ReasoningEffortSelector")!;
            var ability = view.FindControl<ComboBox>("AbilitySelector")!;
            var selectedReasoning = chat.SelectedReasoningEffort;
            var selectedAbility = chat.SelectedAbility;
            Assert.IsNotNull(selectedReasoning);
            Assert.IsNotNull(selectedAbility);
            var reasoningText = reasoning.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == selectedReasoning.DisplayName);
            var abilityText = ability.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == selectedAbility.DisplayName);

            SwitchLanguage();
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(selectedReasoning.DisplayName, reasoningText.Text);
            Assert.AreEqual(selectedAbility.DisplayName, abilityText.Text);
        }
        finally
        {
            window.Close();
        }
    }
}
