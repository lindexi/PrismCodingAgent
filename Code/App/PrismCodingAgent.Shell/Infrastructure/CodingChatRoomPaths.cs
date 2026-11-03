using System;
using System.IO;

namespace PrismCodingAgent.AvaloniaShell.Infrastructure;

/// <summary>
/// 定义应用的本地数据路径。
/// </summary>
public sealed class CodingChatRoomPaths
{
    private const string ApplicationFolderName = "PrismCodingAgent";
    private const string LegacyApplicationFolderName = "CodingChatRoom";
    private const string ConfigurationFileName = "AgentConfiguration.json";
    private const string ShellSettingsFileName = "ShellSettings.json";
    private const string WorkTasksFileName = "WorkTasks.json";

    private readonly string? _legacyDirectory;

    private CodingChatRoomPaths(string rootDirectory, string? legacyDirectory = null)
    {
        _legacyDirectory = legacyDirectory;
        RootDirectory = Path.GetFullPath(rootDirectory);
        ConfigurationFile = new FileInfo(Path.Join(RootDirectory, ConfigurationFileName));
        ShellSettingsFile = new FileInfo(Path.Join(RootDirectory, ShellSettingsFileName));
        WorkTasksFile = new FileInfo(Path.Join(RootDirectory, WorkTasksFileName));
        LogDirectory = Path.Join(RootDirectory, "Logs");
        SessionDirectory = new DirectoryInfo(Path.Join(RootDirectory, "Sessions"));
        AbilitiesDirectory = new DirectoryInfo(Path.Join(RootDirectory, "Abilities"));
    }

    /// <summary>
    /// 获取应用本地数据根目录。
    /// </summary>
    public string RootDirectory { get; }

    /// <summary>
    /// 获取固定模型配置文件。
    /// </summary>
    public FileInfo ConfigurationFile { get; }

    /// <summary>
    /// 获取 Shell 设置文件。
    /// </summary>
    public FileInfo ShellSettingsFile { get; }

    /// <summary>
    /// 获取工作任务元数据文件。
    /// </summary>
    public FileInfo WorkTasksFile { get; }

    /// <summary>
    /// 获取文本日志目录。
    /// </summary>
    public string LogDirectory { get; }

    /// <summary>
    /// 获取可恢复会话目录。
    /// </summary>
    public DirectoryInfo SessionDirectory { get; }

    /// <summary>
    /// 获取发送前能力目录。
    /// </summary>
    public DirectoryInfo AbilitiesDirectory { get; }

    /// <summary>
    /// 为当前用户创建生产路径对象。
    /// </summary>
    public static CodingChatRoomPaths CreateForCurrentUser()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return CreateForLocalApplicationData(localApplicationData);
    }

    internal static CodingChatRoomPaths CreateForLocalApplicationData(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        return new CodingChatRoomPaths(
            Path.Join(localApplicationData, ApplicationFolderName),
            Path.Join(localApplicationData, LegacyApplicationFolderName));
    }

    /// <summary>
    /// 为测试或显式宿主根目录创建路径对象。
    /// </summary>
    internal static CodingChatRoomPaths Create(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return new CodingChatRoomPaths(rootDirectory);
    }

    /// <summary>
    /// 默认路径下仅有旧数据目录时先复制数据，再创建应用所需目录，不创建配置文件。
    /// </summary>
    public void EnsureDirectories()
    {
        if (_legacyDirectory is not null && !Directory.Exists(RootDirectory) && Directory.Exists(_legacyDirectory))
        {
            CopyDirectory(_legacyDirectory, RootDirectory);
        }

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogDirectory);
        SessionDirectory.Create();
        AbilitiesDirectory.Create();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Join(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Join(destination, Path.GetFileName(directory)));
        }
    }
}
