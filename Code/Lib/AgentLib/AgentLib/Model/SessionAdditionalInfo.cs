using System.Collections;
using System.Text.Json;

namespace AgentLib.Model;

/// <summary>管理会话附加信息及其资源存储。</summary>
public sealed class SessionAdditionalInfo : IEnumerable<KeyValuePair<string, ISessionAdditionalInfo>>
{
    private readonly Dictionary<string, ISessionAdditionalInfo> _items = new(StringComparer.Ordinal);

    /// <summary>使用所属会话的资源存储创建附加信息集合。</summary>
    public SessionAdditionalInfo(ISessionStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        Storage = storage;
    }

    /// <summary>附加信息使用的资源存储。</summary>
    public ISessionStorage Storage { get; }

    /// <summary>取得指定业务的附加信息。</summary>
    public ISessionAdditionalInfo this[string key] => _items[key];

    /// <summary>取得或判断指定业务是否已有附加信息。</summary>
    public bool TryGetValue(string key, out ISessionAdditionalInfo? value) => _items.TryGetValue(key, out value);

    /// <summary>保存指定业务的附加信息。</summary>
    public void Set(string key, ISessionAdditionalInfo value) => _items[key] = value;

    /// <summary>清除会话附加信息。</summary>
    public void Clear() => _items.Clear();

    /// <summary>导出各业务的持久化索引。</summary>
    public async Task<IReadOnlyDictionary<string, JsonElement>> SerializeAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in _items)
            result.Add(key, await value.SerializeAsync(Storage, cancellationToken).ConfigureAwait(false));
        return result;
    }

    /// <summary>枚举会话附加信息。</summary>
    public IEnumerator<KeyValuePair<string, ISessionAdditionalInfo>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
