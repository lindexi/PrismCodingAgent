namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class CodingModelPurposeTests
{
    [DataTestMethod]
    [DataRow(CodingModelPurposes.CodeDesign, "CodeDesign")]
    [DataRow(CodingModelPurposes.CodeReview, "CodeReview")]
    [DataRow(CodingModelPurposes.CodeExploration, "CodeExploration")]
    public void CodingModelPurposes_UseStablePersistedIdentifiers(string actual, string expected)
    {
        Assert.AreEqual(expected, actual);
    }
}
