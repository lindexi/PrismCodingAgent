namespace AgentLib.Model;

/// <summary>会话快照标识。</summary>
public readonly record struct SessionSnapshotId(Guid Value)
{
    /// <summary>创建新的会话快照标识。</summary>
    public static SessionSnapshotId NewId() => new(Guid.NewGuid());

    /// <summary>返回默认格式的 Guid 文本。</summary>
    public override string ToString() => Value.ToString();

    /// <summary>返回 N 格式的快照目录名。</summary>
    public string ToDirectoryName() => Value.ToString("N");
}
