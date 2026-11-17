using System.Text.Json;

namespace AgentLib.Model;

/// <summary>提供会话附加信息的 JSON 表示。</summary>
public interface ISessionAdditionalInfo
{
    /// <summary>异步导出附加信息，返回值不依赖已释放的 JSON 文档。</summary>
    ValueTask<JsonElement> SerializeAsync(ISessionStorage storage, CancellationToken cancellationToken = default);
}
