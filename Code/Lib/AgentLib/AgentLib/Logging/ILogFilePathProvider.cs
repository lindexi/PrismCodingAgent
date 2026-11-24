namespace AgentLib.Logging;

/// <summary>提供日志记录器绑定的目标文件路径。</summary>
public interface ILogFilePathProvider
{
    /// <summary>获取目标文件路径；尚未确定目标时返回 <see langword="null"/>。</summary>
    Task<string?> GetLogFilePathAsync(CancellationToken cancellationToken = default);
}
