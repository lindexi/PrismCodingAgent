using AgentLib.Model;

namespace AgentLib.Tests.Model;

[TestClass]
public class SessionSnapshotIdTests
{
    [TestMethod]
    public void ConstructorShouldPreserveValue()
    {
        Guid value = Guid.NewGuid();

        var id = new SessionSnapshotId(value);

        Assert.AreEqual(value, id.Value);
    }

    [TestMethod]
    public void NewIdShouldProduceNonEmptyId()
    {
        SessionSnapshotId id = SessionSnapshotId.NewId();

        Assert.AreNotEqual(new SessionSnapshotId(Guid.Empty), id);
    }

    [TestMethod]
    public void NewIdShouldProduceDistinctIds()
    {
        SessionSnapshotId first = SessionSnapshotId.NewId();
        SessionSnapshotId second = SessionSnapshotId.NewId();

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void ToStringShouldReturnDefaultGuidText()
    {
        var id = new SessionSnapshotId(new Guid("00112233-4455-6677-8899-aabbccddeeff"));

        Assert.AreEqual("00112233-4455-6677-8899-aabbccddeeff", id.ToString());
    }

    [TestMethod]
    public void ToFileNameShouldReturnNFormatId()
    {
        var id = new SessionSnapshotId(new Guid("00112233-4455-6677-8899-aabbccddeeff"));

        Assert.AreEqual("00112233445566778899aabbccddeeff", id.ToFileName());
    }

    [TestMethod]
    public void SameValueShouldProduceEqualIds()
    {
        Guid value = Guid.NewGuid();

        Assert.AreEqual(new SessionSnapshotId(value), new SessionSnapshotId(value));
    }
}
