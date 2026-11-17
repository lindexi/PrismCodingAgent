namespace AgentLib.Model;

/// <summary>按相对于会话 Resources 的路径访问附加资源。</summary>
public interface ISessionStorage
{
    /// <summary>打开现有资源的读取流，由调用方释放。</summary>
    Stream OpenRead(string relativePath);

    /// <summary>创建或覆盖资源，返回由调用方释放的写入流。</summary>
    Stream OpenWrite(string relativePath);
}
