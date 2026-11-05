using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class CodingChatRunOptionsTests
{
    [TestMethod]
    public void DefaultShouldPreserveLibraryDefaults()
    {
        var options = CodingChatRunOptions.Default;

        Assert.IsTrue(options.EnableAutomaticCompression && !options.EnableDotNetRun
            && options.ReasoningEffort is null
            && options.LanguageTag == System.Globalization.CultureInfo.CurrentUICulture.Name);
    }

    [TestMethod]
    public void DefaultShouldReturnIndependentSettings()
    {
        var options = CodingChatRunOptions.Default;
        options.EnableAutomaticCompression = false;

        Assert.IsTrue(CodingChatRunOptions.Default.EnableAutomaticCompression);
    }

    [TestMethod]
    [DataRow(nameof(CodingChatRunOptions.EnableAutomaticCompression), false)]
    [DataRow(nameof(CodingChatRunOptions.EnableDotNetRun), true)]
    [DataRow(nameof(CodingChatRunOptions.LanguageTag), "test-language")]
    public void ChangedSettingShouldNotifyOnlyOnce(string propertyName, object value)
    {
        var options = CodingChatRunOptions.Default;
        var changes = new List<string?>();
        options.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var property = typeof(CodingChatRunOptions).GetProperty(propertyName)!;

        property.SetValue(options, value);
        property.SetValue(options, value);

        CollectionAssert.AreEqual(new[] { propertyName }, changes);
    }

    [TestMethod]
    public void ReasoningEffortShouldNotifyOnlyWhenChanged()
    {
        var options = CodingChatRunOptions.Default;
        var changes = new List<string?>();
        options.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        options.ReasoningEffort = ReasoningEffort.High;
        options.ReasoningEffort = ReasoningEffort.High;
        options.ReasoningEffort = null;

        CollectionAssert.AreEqual(new[] { nameof(CodingChatRunOptions.ReasoningEffort), nameof(CodingChatRunOptions.ReasoningEffort) }, changes);
    }
}
