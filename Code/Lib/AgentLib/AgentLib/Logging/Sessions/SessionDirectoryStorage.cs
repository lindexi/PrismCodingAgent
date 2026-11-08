using AgentLib.Logging.Sessions.Codecs;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AgentLib.Logging.Sessions;

internal static class SessionDirectoryStorage
{
    internal const string EntryFileName = "Session.xml";
    private const string ArchiveFileName = "Content.zip";

    internal static async Task WriteAsync(string entryPath, CopilotChatSessionPersistenceData data, DateTimeOffset updatedTime,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(entryPath)!;
        Directory.CreateDirectory(directory);
        var resources = new SessionResources();
        XDocument document = SessionHistoryXmlCodec.Write(data.Entries, resources);
        await resources.WriteAsync(directory, cancellationToken).ConfigureAwait(false);

        if (data.AgentSessionState is { } agentState)
        {
            JsonNode state = JsonNode.Parse(agentState.GetRawText())!;
            await TransformResourcesAsync(state, directory, false, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(directory, "AgentSession.json"), state.ToJsonString(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            File.Delete(Path.Join(directory, "AgentSession.json"));
        }

        if (data.ResponsesSession is { } responsesSession)
        {
            var items = new JsonArray();
            foreach (var item in responsesSession.Items)
            {
                items.Add(JsonNode.Parse(System.ClientModel.Primitives.ModelReaderWriter.Write(item).ToString()));
            }

            await TransformResourcesAsync(items, directory, false, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(directory, "ResponsesSession.json"), items.ToJsonString(), cancellationToken).ConfigureAwait(false);
        }
        else File.Delete(Path.Join(directory, "ResponsesSession.json"));

        await FileCopilotChatSessionStore.SaveDocumentAsync(Path.Join(directory, "History.xml"), document, cancellationToken).ConfigureAwait(false);
        var entry = new XElement("CopilotChatSessionHistory",
            new XAttribute("SessionId", data.SessionId.ToString("N")), new XAttribute("StartedTime", data.StartedTime.ToString("o")),
            new XAttribute("Title", data.Title));
        entry.SetAttributeValue("WorkspacePath", data.WorkspacePath);
        entry.SetAttributeValue("WorkTaskId", data.WorkTaskId);
        entry.SetAttributeValue("WorkTaskName", data.WorkTaskName);
        entry.SetAttributeValue("FormatVersion", FileCopilotChatSessionStore.CurrentFormatVersion);
        entry.SetAttributeValue("UpdatedTime", updatedTime.ToString("o"));
        entry.SetAttributeValue("ContentStorage", "Expanded");
        entry.SetAttributeValue("MessageCount", data.Entries.OfType<CopilotChatMessage>().Count());
        entry.SetAttributeValue("EntryCount", data.Entries.Count);
        string? preview = data.Entries.OfType<CopilotChatMessage>()
            .Where(message => message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
            .Select(message => message.Content).LastOrDefault(text => !string.IsNullOrWhiteSpace(text));
        if (preview is not null) entry.Add(new XElement("Summary", preview[..Math.Min(preview.Length, 200)]));
        await FileCopilotChatSessionStore.SaveDocumentAsync(entryPath, new XDocument(entry), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<CopilotChatSessionPersistenceData> ReadAsync(string entryPath, CancellationToken cancellationToken)
    {
        await ExpandAsync(entryPath, cancellationToken).ConfigureAwait(false);
        string directory = Path.GetDirectoryName(entryPath)!;
        XDocument document = await ReadXmlAsync(Path.Join(directory, "History.xml"), cancellationToken).ConfigureAwait(false);
        XElement root = document.Root!;
        var resources = new SessionResources();
        await resources.ReadAsync(directory, root.Descendants().Attributes("Resource").Select(attribute => attribute.Value), cancellationToken).ConfigureAwait(false);
        XElement entry = (await ReadXmlAsync(entryPath, cancellationToken).ConfigureAwait(false)).Root!;
        JsonElement? agentState = null;
        CopilotResponsesSession? responses = null;
        string agentPath = Path.Join(directory, "AgentSession.json");
        if (File.Exists(agentPath))
        {
            JsonNode state = JsonNode.Parse(await File.ReadAllTextAsync(agentPath, cancellationToken).ConfigureAwait(false))!;
            await TransformResourcesAsync(state, directory, true, cancellationToken).ConfigureAwait(false);
            using JsonDocument stateDocument = JsonDocument.Parse(state.ToJsonString());
            agentState = stateDocument.RootElement.Clone();
        }
        string responsesPath = Path.Join(directory, "ResponsesSession.json");
        if (File.Exists(responsesPath))
        {
            JsonArray items = JsonNode.Parse(await File.ReadAllTextAsync(responsesPath, cancellationToken).ConfigureAwait(false))!.AsArray();
            await TransformResourcesAsync(items, directory, true, cancellationToken).ConfigureAwait(false);
            responses = new CopilotResponsesSession();
            foreach (JsonNode? item in items) responses.AppendItem(ResponsesSessionJsonCodec.ReadItem(item!.ToJsonString()));
        }
        return new CopilotChatSessionPersistenceData
        {
            FormatVersion = (int)entry.Attribute("FormatVersion")!,
            SessionId = Guid.Parse(entry.Attribute("SessionId")!.Value),
            StartedTime = DateTimeOffset.Parse(entry.Attribute("StartedTime")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Title = (string?)entry.Attribute("Title") ?? string.Empty,
            WorkspacePath = (string?)entry.Attribute("WorkspacePath"),
            WorkTaskId = Guid.TryParse((string?)entry.Attribute("WorkTaskId"), out Guid taskId) ? taskId : null,
            WorkTaskName = (string?)entry.Attribute("WorkTaskName"),
            Entries = SessionHistoryXmlCodec.Read(document, resources),
            AgentSessionState = agentState,
            ResponsesSession = responses,
        };
    }

    internal static async Task<bool> CompressAsync(string entryPath, CancellationToken cancellationToken)
    {
        XDocument entry = await ReadXmlAsync(entryPath, cancellationToken).ConfigureAwait(false);
        if ((string?)entry.Root!.Attribute("ContentStorage") == "Zip") return false;
        string directory = Path.GetDirectoryName(entryPath)!;
        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(file => file != entryPath && Path.GetFileName(file) != ArchiveFileName).ToArray();
        string archivePath = Path.Join(directory, ArchiveFileName);
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ZipArchiveEntry zipEntry = archive.CreateEntry(Path.GetRelativePath(directory, file).Replace('\\', '/'), CompressionLevel.SmallestSize);
                await using Stream output = zipEntry.Open();
                await using var input = File.OpenRead(file);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
        }
        entry.Root.SetAttributeValue("ContentStorage", "Zip");
        await FileCopilotChatSessionStore.SaveDocumentAsync(entryPath, entry, cancellationToken).ConfigureAwait(false);
        foreach (string file in files) File.Delete(file);
        foreach (string subdirectory in Directory.GetDirectories(directory)) Directory.Delete(subdirectory, recursive: true);
        return true;
    }

    internal static async Task ExpandAsync(string entryPath, CancellationToken cancellationToken)
    {
        XDocument entry = await ReadXmlAsync(entryPath, cancellationToken).ConfigureAwait(false);
        if ((string?)entry.Root!.Attribute("ContentStorage") != "Zip") return;
        string directory = Path.GetDirectoryName(entryPath)!;
        string archivePath = Path.Join(directory, ArchiveFileName);
        await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, directory, overwriteFiles: true), cancellationToken).ConfigureAwait(false);
        entry.Root.SetAttributeValue("ContentStorage", "Expanded");
        await FileCopilotChatSessionStore.SaveDocumentAsync(entryPath, entry, cancellationToken).ConfigureAwait(false);
        File.Delete(archivePath);
    }

    internal static async Task<XDocument> ReadXmlAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> WriteResourceAsync(string directory, byte[] data, string mimeType, CancellationToken cancellationToken)
    {
        var resources = new SessionResources();
        string reference = resources.Add(BinaryData.FromBytes(data), mimeType);
        await resources.WriteAsync(directory, cancellationToken).ConfigureAwait(false);
        return reference;
    }

    private static async Task TransformResourcesAsync(JsonNode node, string directory, bool restore, CancellationToken cancellationToken)
    {
        // Only typed binary content and data URLs are storage resources; ordinary text is left intact.
        if (node is JsonObject binary && binary["$type"]?.GetValue<string>() is "data" && binary["data"] is JsonValue dataValue)
        {
            if (restore && binary["resource"] is JsonValue reference)
            {
                byte[] bytes = await File.ReadAllBytesAsync(Path.Join(directory, reference.GetValue<string>()), cancellationToken).ConfigureAwait(false);
                binary["data"] = Convert.ToBase64String(bytes);
                binary.Remove("resource");
            }
            else if (!restore)
            {
                string mime = binary["mediaType"]?.GetValue<string>() ?? "application/octet-stream";
                binary["resource"] = await WriteResourceAsync(directory, Convert.FromBase64String(dataValue.GetValue<string>()), mime, cancellationToken).ConfigureAwait(false);
                binary["data"] = "";
            }
        }
        IEnumerable<(string? Key, int Index, JsonNode? Value)> children = node switch
        {
            JsonObject obj => obj.Select(pair => ((string?)pair.Key, -1, pair.Value)).ToArray(),
            JsonArray array => array.Select((value, index) => ((string?)null, index, value)).ToArray(),
            _ => [],
        };
        foreach (var (key, index, value) in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is JsonObject resource && restore && resource["$resource"] is JsonValue path)
            {
                byte[] bytes = await File.ReadAllBytesAsync(Path.Join(directory, path.GetValue<string>()), cancellationToken).ConfigureAwait(false);
                JsonNode replacement = JsonValue.Create("data:" + resource["MediaType"]!.GetValue<string>() + ";base64," + Convert.ToBase64String(bytes))!;
                if (key is not null) node[key] = replacement; else node[index] = replacement;
            }
            else if (!restore && (key is "uri" or "url" or "image_url" or "file_data")
                && value is JsonValue scalar && scalar.TryGetValue<string>(out string? text)
                && text.StartsWith("data:", StringComparison.Ordinal) && text.IndexOf(";base64,", StringComparison.Ordinal) is int separator && separator >= 5)
            {
                string mime = text[5..separator];
                string reference = await WriteResourceAsync(directory, Convert.FromBase64String(text[(separator + 8)..]), mime, cancellationToken).ConfigureAwait(false);
                var replacement = new JsonObject { ["$resource"] = reference, ["MediaType"] = mime };
                if (key is not null) node[key] = replacement; else node[index] = replacement;
            }
            else if (value is JsonObject or JsonArray)
                await TransformResourcesAsync(value, directory, restore, cancellationToken).ConfigureAwait(false);
        }
    }
}
