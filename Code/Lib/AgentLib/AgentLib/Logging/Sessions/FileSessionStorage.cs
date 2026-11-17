using AgentLib.Model;

namespace AgentLib.Logging.Sessions;

/// <summary>访问会话目录下 Resources 中的附加文件。</summary>
public sealed class FileSessionStorage : ISessionStorage
{
    private readonly string _resourcesDirectory;

    /// <summary>使用所属会话目录创建资源存储。</summary>
    public FileSessionStorage(string sessionDirectory)
    {
        _resourcesDirectory = Path.Join(sessionDirectory, "Resources");
    }

    /// <inheritdoc />
    public Stream OpenRead(string relativePath) => File.OpenRead(Path.Join(_resourcesDirectory, relativePath));

    /// <inheritdoc />
    public Stream OpenWrite(string relativePath)
    {
        string path = Path.Join(_resourcesDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.Create(path);
    }
}
