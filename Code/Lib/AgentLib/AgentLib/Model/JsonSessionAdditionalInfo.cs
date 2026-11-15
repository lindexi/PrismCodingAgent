using System.Text.Json;

namespace AgentLib.Model;

/// <summary>保留尚未被业务消费的会话附加 JSON 信息。</summary>
public sealed class JsonSessionAdditionalInfo : ISessionAdditionalInfo
{
    /// <summary>复制 JSON 数据，使其不依赖源文档的生命周期。</summary>
    public JsonSessionAdditionalInfo(JsonElement value) => Value = value.Clone();

    /// <summary>独立持有的原始 JSON 数据。</summary>
    public JsonElement Value { get; }

    /// <inheritdoc />
    public ValueTask<JsonElement> SerializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Value);
    }
}
