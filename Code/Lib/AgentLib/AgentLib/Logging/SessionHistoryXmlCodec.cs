using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.Xml.Linq;

namespace AgentLib.Logging;

internal static class SessionHistoryXmlCodec
{
    internal static XDocument Write(IEnumerable<CopilotChatEntry> entries, SessionResources resources) =>
        new(new XElement("History", new XElement("Messages", entries.Select(entry => WriteEntry(entry, resources)))));

    internal static IReadOnlyList<CopilotChatEntry> Read(XDocument document, SessionResources resources) =>
        (document.Root?.Element("Messages") ?? throw new InvalidDataException("会话正文缺少 Messages 节点。"))
        .Elements().Select(element => ReadEntry(element, resources)).ToArray();

    private static XElement WriteEntry(CopilotChatEntry entry, SessionResources resources) => entry switch
    {
        CopilotChatMessage message => WriteMessage(message, resources),
        CopilotChatNotice notice => new XElement("Notice", new XAttribute("CreatedTime", notice.CreatedTime.ToString("o")),
            new XAttribute("Level", notice.Level), new XElement("Content", notice.Content)),
        _ => throw new InvalidDataException($"不支持的时间线条目：{entry.GetType().FullName}"),
    };

    private static CopilotChatEntry ReadEntry(XElement element, SessionResources resources)
    {
        if (element.Name == "Message") return ReadMessage(element, resources);
        if (element.Name != "Notice" || !Enum.TryParse((string?)element.Attribute("Level"), out CopilotChatNoticeLevel level)
            || !Enum.IsDefined(typeof(CopilotChatNoticeLevel), level) || element.Element("Content") is not { } content)
            throw new InvalidDataException($"无效的时间线条目：{element.Name}");
        return new CopilotChatNotice(content.Value, level, ReadTime(element));
    }

    private static XElement WriteMessage(CopilotChatMessage message, SessionResources resources) =>
        new("Message", new XAttribute("Role", message.Role), new XAttribute("Author", message.Author),
            new XAttribute("CreatedTime", message.CreatedTime.ToString("o")), new XAttribute("IsPresetInfo", message.IsPresetInfo),
            new XElement("Content", message.Content), new XElement("Reason", message.Reason),
            new XElement("MessageItems", message.MessageItems.Select(item => WriteItem(item, resources))),
            message.TotalUsageDetails is { } usage ? WriteUsage(usage) : null);

    private static CopilotChatMessage ReadMessage(XElement element, SessionResources resources) =>
        CopilotChatMessage.Restore(new ChatRole((string?)element.Attribute("Role") ?? throw new InvalidDataException("消息缺少 Role。")),
            ReadTime(element), (bool?)element.Attribute("IsPresetInfo") ?? false,
            (element.Element("MessageItems") ?? throw new InvalidDataException("消息缺少 MessageItems。"))
                .Elements().Select(item => ReadItem(item, resources)).ToList(), ReadUsage(element.Element("UsageDetails")));

    private static XElement WriteItem(ICopilotChatMessageItem item, SessionResources resources) => item switch
    {
        CopilotChatTextItem text => new XElement("TextItem", new XAttribute("Text", text.Text)),
        CopilotChatReasoningItem reason => new XElement("ReasoningItem", new XAttribute("Text", reason.Text)),
        CopilotChatImageItem image => WriteResource("ImageItem", image.Data, image.MimeType, resources),
        CopilotChatAudioItem audio => WriteResource("AudioItem", audio.Data, audio.MimeType, resources),
        CopilotChatToolItem tool => new XElement("ToolItem", new XAttribute("CallId", tool.CallId), new XAttribute("ToolName", tool.ToolName),
            new XElement("PrimaryText", tool.PrimaryText), new XElement("SecondaryText", tool.SecondaryText),
            new XElement("FullTargetText", tool.FullTargetText), new XElement("Input", tool.InputText), new XElement("Output", tool.OutputText),
            tool.Details is { } details ? new XElement("Details", WriteMessage(details, resources)) : null),
        CopilotChatApprovalToolItem approval => new XElement("ApprovalToolItem", new XAttribute("CallId", approval.CallId),
            new XAttribute("ToolName", approval.ToolName), new XAttribute("DisplayName", approval.DisplayName),
            new XAttribute("ApprovalState", approval.ApprovalState), new XElement("Input", approval.InputText),
            new XElement("Output", approval.OutputText), new XElement("ApprovalDescription", approval.ApprovalDescription),
            new XElement("DecisionReason", approval.DecisionReason)),
        CopilotChatSubAgentItem child => new XElement("SubAgentItem", new XAttribute("CallId", child.CallId),
            new XAttribute("ToolName", child.ToolName), new XElement("Input", child.InputText), new XElement("Output", child.OutputText),
            new XElement("MessageItems", child.MessageItems.Select(fragment => WriteItem(fragment, resources)))),
        _ => throw new InvalidDataException($"不支持的消息片段：{item.GetType().FullName}"),
    };

    private static ICopilotChatMessageItem ReadItem(XElement element, SessionResources resources) => element.Name.LocalName switch
    {
        "TextItem" => new CopilotChatTextItem((string?)element.Attribute("Text") ?? string.Empty),
        "ReasoningItem" => new CopilotChatReasoningItem((string?)element.Attribute("Text") ?? string.Empty),
        "ImageItem" => new CopilotChatImageItem(resources.Get((string?)element.Attribute("Resource") ?? throw new InvalidDataException("图片缺少 Resource。")),
            (string?)element.Attribute("MimeType") ?? "application/octet-stream"),
        "AudioItem" => new CopilotChatAudioItem(resources.Get((string?)element.Attribute("Resource") ?? throw new InvalidDataException("音频缺少 Resource。")),
            (string?)element.Attribute("MimeType") ?? "application/octet-stream"),
        "ToolItem" => ReadTool(element, resources),
        "ApprovalToolItem" => ReadApproval(element),
        "SubAgentItem" => ReadChild(element, resources),
        _ => throw new InvalidDataException($"不支持的消息片段：{element.Name}"),
    };

    private static CopilotChatToolItem ReadTool(XElement element, SessionResources resources) =>
        new((string?)element.Attribute("CallId") ?? string.Empty, (string?)element.Attribute("ToolName") ?? string.Empty,
            (string?)element.Element("Input"), (string?)element.Element("Output"),
            new ToolCallPresentation((string?)element.Element("PrimaryText"), (string?)element.Element("SecondaryText"), (string?)element.Element("FullTargetText")))
        {
            Details = element.Element("Details")?.Element("Message") is { } details ? ReadMessage(details, resources) : null,
        };

    private static CopilotChatSubAgentItem ReadChild(XElement element, SessionResources resources)
    {
        var child = new CopilotChatSubAgentItem((string?)element.Attribute("CallId") ?? string.Empty,
            (string?)element.Attribute("ToolName") ?? string.Empty, (string?)element.Element("Input"), (string?)element.Element("Output"));
        foreach (XElement fragment in element.Element("MessageItems")?.Elements() ?? []) child.MessageItems.Add(ReadItem(fragment, resources));
        return child;
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

    private static XElement WriteResource(string name, BinaryData data, string mediaType, SessionResources resources) =>
        new(name, new XAttribute("MimeType", mediaType), new XAttribute("Resource", resources.Add(data, mediaType)));

    private static XElement WriteUsage(UsageDetails usage)
    {
        var element = new XElement("UsageDetails");
        AddUsage(element, "TotalTokenCount", usage.TotalTokenCount);
        AddUsage(element, "InputTokenCount", usage.InputTokenCount);
        AddUsage(element, "OutputTokenCount", usage.OutputTokenCount);
        AddUsage(element, "ReasoningTokenCount", usage.ReasoningTokenCount);
        AddUsage(element, "CachedInputTokenCount", usage.CachedInputTokenCount);
        return element;
    }

    private static void AddUsage(XElement element, string name, long? value)
    {
        if (value is not null) element.SetAttributeValue(name, value.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static UsageDetails? ReadUsage(XElement? element) => element is null ? null : new UsageDetails
    {
        TotalTokenCount = (long?)element.Attribute("TotalTokenCount"), InputTokenCount = (long?)element.Attribute("InputTokenCount"),
        OutputTokenCount = (long?)element.Attribute("OutputTokenCount"), ReasoningTokenCount = (long?)element.Attribute("ReasoningTokenCount"),
        CachedInputTokenCount = (long?)element.Attribute("CachedInputTokenCount"),
    };

    private static DateTimeOffset ReadTime(XElement element) => DateTimeOffset.Parse(
        (string?)element.Attribute("CreatedTime") ?? throw new InvalidDataException("时间线条目缺少 CreatedTime。"),
        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
