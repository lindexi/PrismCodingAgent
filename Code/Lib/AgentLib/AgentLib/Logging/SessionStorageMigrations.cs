using System.Globalization;
using System.Xml.Linq;

namespace AgentLib.Logging;

internal static class SessionStorageMigrations
{
    internal static async Task<string> UpgradeAsync(string entryPath, string sessionDirectory, CancellationToken cancellationToken)
    {
        int version = await ReadVersionAsync(entryPath, cancellationToken).ConfigureAwait(false);
        if (version is < 1 or > FileCopilotChatSessionStore.CurrentFormatVersion)
            throw new InvalidDataException($"不支持的聊天历史格式版本：{version}。文件：{entryPath}");

        // 版本 1—4 的历史差异由原有解析器统一处理，之后只执行相邻版本迁移。
        if (version < 4) version = 4;
        while (version < FileCopilotChatSessionStore.CurrentFormatVersion)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entryPath = version switch
            {
                4 => await Upgrade4To5Async(entryPath, sessionDirectory, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidDataException($"缺少聊天历史版本 {version} 到 {version + 1} 的迁移。文件：{entryPath}"),
            };
            version = await ReadVersionAsync(entryPath, cancellationToken).ConfigureAwait(false);
        }
        return entryPath;
    }

    private static async Task<int> ReadVersionAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { Async = true });
        cancellationToken.ThrowIfCancellationRequested();
        await reader.MoveToContentAsync().ConfigureAwait(false);
        if (reader.Name != "CopilotChatSessionHistory")
            throw new InvalidDataException($"聊天历史文件缺少根节点。文件：{path}");
        return int.TryParse(reader.GetAttribute("FormatVersion"), out int version) ? version : 1;
    }

    private static async Task<string> Upgrade4To5Async(string path, string sessionDirectory, CancellationToken cancellationToken)
    {
        XDocument source = await SessionDirectoryStorage.ReadXmlAsync(path, cancellationToken).ConfigureAwait(false);
        CopilotChatSessionPersistenceData data = CopilotChatHistoryXmlCodec.ReadPersistenceData(source);
        var resources = new SessionResources();
        var document = new XDocument(CopilotChatHistoryXmlCodec.CreateRootElement(data.SessionId, data.StartedTime,
            data.Title, 5, data.Entries, data.AgentSessionState, resources));
        CopilotChatHistoryXmlCodec.AppendResponsesSession(document.Root!, data.ResponsesSession);
        document.Root!.SetAttributeValue("WorkspacePath", data.WorkspacePath);
        document.Root.SetAttributeValue("WorkTaskId", data.WorkTaskId);
        document.Root.SetAttributeValue("WorkTaskName", data.WorkTaskName);
        string entryPath = Path.Join(sessionDirectory, data.StartedTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            data.SessionId.ToString("N"), SessionDirectoryStorage.EntryFileName);
        await SessionDirectoryStorage.WriteAsync(entryPath, document, resources, File.GetLastWriteTimeUtc(path), cancellationToken).ConfigureAwait(false);
        File.Delete(path);
        return entryPath;
    }
}
