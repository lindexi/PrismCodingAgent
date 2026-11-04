using AgentLib.Logging.Sessions;
using AgentLib.Logging.Sessions.Codecs;
using AgentLib.Model;
using System.Text.Json;
using System.Xml.Linq;

namespace AgentLib.Logging;

internal static class LegacySessionXmlFixture
{
    internal static XElement CreateRootElement(Guid sessionId, DateTimeOffset startedTime, string title,
        int formatVersion, IEnumerable<CopilotChatEntry> entries, JsonElement? agentSessionState)
    {
        var resources = new SessionResources();
        XDocument history = SessionHistoryXmlCodec.Write(entries, resources);
        XElement messages = history.Root!.Element("Messages")!;
        foreach (XAttribute reference in messages.Descendants().Attributes("Resource").ToArray())
        {
            reference.Parent!.Value = Convert.ToBase64String(resources.Get(reference.Value).ToArray());
            reference.Remove();
        }
        return new XElement("CopilotChatSessionHistory", new XAttribute("SessionId", sessionId),
            new XAttribute("StartedTime", startedTime.ToString("o")), new XAttribute("Title", title),
            new XAttribute("FormatVersion", formatVersion),
            agentSessionState is JsonElement state ? new XElement("AgentSessionState", new XCData(state.GetRawText())) : null,
            messages);
    }
}
