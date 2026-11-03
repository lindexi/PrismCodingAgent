using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace AgentLib.Logging;

internal static class LegacySessionXmlReader
{
    internal static CopilotChatSessionPersistenceData Read(XDocument document)
    {
        XElement root = document.Root ?? throw new InvalidDataException("聊天历史文件缺少根节点。");
        int version = (int?)root.Attribute("FormatVersion") ?? 1;
        XElement messages = root.Element("Messages") ?? throw new InvalidDataException("聊天历史文件缺少 Messages 节点。");
        return new CopilotChatSessionPersistenceData
        {
            FormatVersion = version,
            SessionId = Guid.TryParse((string?)root.Attribute("SessionId"), out Guid id)
                ? id : throw new InvalidDataException("聊天历史文件缺少有效的 SessionId。"),
            StartedTime = ParseTime((string?)root.Attribute("StartedTime") ?? (string?)root.Attribute("CreatedTime")),
            Title = (string?)root.Attribute("Title") ?? string.Empty,
            WorkspacePath = (string?)root.Attribute("WorkspacePath"),
            WorkTaskId = Guid.TryParse((string?)root.Attribute("WorkTaskId"), out Guid taskId) ? taskId : null,
            WorkTaskName = (string?)root.Attribute("WorkTaskName"),
            Entries = messages.Elements().Select(element => ReadEntry(element, version)).ToArray(),
            AgentSessionState = ReadAgentState(root.Element("AgentSessionState")?.Value),
            ResponsesSession = ReadResponses(root.Element("ProtocolStates")?.Element("ResponsesState")),
        };
    }

    private static CopilotChatEntry ReadEntry(XElement element, int version)
    {
        if (element.Name == "Notice")
        {
            if (!Enum.TryParse((string?)element.Attribute("Level"), out CopilotChatNoticeLevel level)
                || !Enum.IsDefined(typeof(CopilotChatNoticeLevel), level) || element.Element("Content") is not { } content)
                throw new InvalidDataException("Invalid notice data.");
            return new CopilotChatNotice(content.Value, level, ParseTime((string?)element.Attribute("CreatedTime")));
        }
        if (element.Name != "Message") throw new InvalidDataException("Unknown timeline entry.");
        CopilotChatMessage message = ReadMessage(element);
        if (version < 4 && message.Role == ChatRole.System && message.IsPresetInfo
            && message.MessageItems.All(item => item is CopilotChatTextItem))
            return new CopilotChatNotice(message.Content, createdTime: message.CreatedTime);
        return message;
    }

    private static CopilotChatMessage ReadMessage(XElement element)
    {
        var items = new List<ICopilotChatMessageItem>();
        if (element.Element("MessageItems") is { } fragments) items.AddRange(fragments.Elements().Select(ReadItem));
        else
        {
            if ((string?)element.Element("Reason") is { Length: > 0 } reason) items.Add(new CopilotChatReasoningItem(reason));
            if ((string?)element.Element("Content") is { Length: > 0 } content) items.Add(new CopilotChatTextItem(content));
        }
        return CopilotChatMessage.Restore(
            new ChatRole((string?)element.Attribute("Role") ?? throw new InvalidDataException("消息缺少 Role 属性。")),
            ParseTime((string?)element.Attribute("CreatedTime")), (bool?)element.Attribute("IsPresetInfo") ?? false,
            items, ReadUsage(element.Element("UsageDetails")));
    }

    private static ICopilotChatMessageItem ReadItem(XElement element) => element.Name.LocalName switch
    {
        "TextItem" => new CopilotChatTextItem((string?)element.Attribute("Text") ?? string.Empty),
        "ReasoningItem" => new CopilotChatReasoningItem((string?)element.Attribute("Text") ?? string.Empty),
        "ImageItem" => new CopilotChatImageItem(BinaryData.FromBytes(Convert.FromBase64String(element.Value)),
            (string?)element.Attribute("MimeType") ?? "application/octet-stream"),
        "AudioItem" => new CopilotChatAudioItem(BinaryData.FromBytes(Convert.FromBase64String(element.Value)),
            (string?)element.Attribute("MimeType") ?? "application/octet-stream"),
        "ToolItem" => new CopilotChatToolItem((string?)element.Attribute("CallId") ?? string.Empty,
            (string?)element.Attribute("ToolName") ?? string.Empty, (string?)element.Element("Input"), (string?)element.Element("Output"),
            new ToolCallPresentation((string?)element.Element("PrimaryText"), (string?)element.Element("SecondaryText"), (string?)element.Element("FullTargetText")))
            { Details = element.Element("Details")?.Element("Message") is { } details ? ReadMessage(details) : null },
        "ApprovalToolItem" => ReadApproval(element),
        "SubAgentItem" => ReadChild(element),
        _ => throw new InvalidDataException($"不支持的聊天消息片段类型：{element.Name.LocalName}"),
    };

    private static CopilotChatSubAgentItem ReadChild(XElement element)
    {
        var item = new CopilotChatSubAgentItem((string?)element.Attribute("CallId") ?? string.Empty,
            (string?)element.Attribute("ToolName") ?? string.Empty, (string?)element.Element("Input"), (string?)element.Element("Output"));
        foreach (XElement fragment in element.Element("MessageItems")?.Elements() ?? []) item.MessageItems.Add(ReadItem(fragment));
        return item;
    }

    private static CopilotChatApprovalToolItem ReadApproval(XElement element)
    {
        var item = new CopilotChatApprovalToolItem((string?)element.Attribute("CallId") ?? string.Empty,
            (string?)element.Attribute("ToolName") ?? string.Empty, (string?)element.Element("Input"),
            (string?)element.Element("ApprovalDescription"), (string?)element.Attribute("DisplayName"))
        { OutputText = (string?)element.Element("Output") ?? string.Empty };
        CopilotToolApprovalState state = Enum.TryParse((string?)element.Attribute("ApprovalState"), out CopilotToolApprovalState parsed)
            ? parsed : CopilotToolApprovalState.Pending;
        switch (state)
        {
            case CopilotToolApprovalState.Approved: item.Approve(); break;
            case CopilotToolApprovalState.Rejected: item.Reject((string?)element.Element("DecisionReason")); break;
            case CopilotToolApprovalState.Canceled: item.Cancel(); break;
        }
        return item;
    }

    private static UsageDetails? ReadUsage(XElement? element) => element is null ? null : new UsageDetails
    {
        TotalTokenCount = ParseLong(element, "TotalTokenCount"), InputTokenCount = ParseLong(element, "InputTokenCount"),
        OutputTokenCount = ParseLong(element, "OutputTokenCount"), ReasoningTokenCount = ParseLong(element, "ReasoningTokenCount"),
        CachedInputTokenCount = ParseLong(element, "CachedInputTokenCount"),
    };

    private static long? ParseLong(XElement element, string name) =>
        long.TryParse((string?)element.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : null;

    private static DateTimeOffset ParseTime(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset time)
            ? time : throw new InvalidDataException("聊天历史缺少有效的时间属性。");

    private static JsonElement? ReadAgentState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CopilotResponsesSession? ReadResponses(XElement? element)
    {
        if (element is null) return null;
        var session = new CopilotResponsesSession();
        foreach (XElement item in element.Elements("Item")) session.AppendItem(ResponsesSessionJsonCodec.ReadItem(item.Value));
        return session;
    }
}
