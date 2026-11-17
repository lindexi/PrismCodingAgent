namespace AgentLib.Model;

/// <summary>会话快照标识。</summary>
public readonly record struct SessionSnapshotId(Guid Value)
{
    /// <summary>创建新的会话快照标识。</summary>
    public static SessionSnapshotId NewId() => new(Guid.NewGuid());

    /// <summary>返回默认格式的 Guid 文本。</summary>
    public override string ToString() => Value.ToString();

    /// <summary>返回适合文件命名的 N 格式标识。</summary>
    public string ToFileName() => Value.ToString("N");
}
