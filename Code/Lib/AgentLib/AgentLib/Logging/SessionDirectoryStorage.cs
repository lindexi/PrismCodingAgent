using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AgentLib.Logging;

internal static class SessionDirectoryStorage
{
    internal const string EntryFileName = "Session.xml";
    private const string ArchiveFileName = "Content.zip";

    internal static async Task WriteAsync(string entryPath, XDocument document, DateTimeOffset updatedTime,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(entryPath)!;
        Directory.CreateDirectory(directory);
        XElement root = document.Root!;
        foreach (XElement resource in root.Descendants().Where(element => element.Name == "ImageItem" || element.Name == "AudioItem"))
        {
            resource.SetAttributeValue("Resource", await WriteResourceAsync(directory,
                Convert.FromBase64String(resource.Value), resource.Attribute("MimeType")!.Value, cancellationToken).ConfigureAwait(false));
            resource.Value = string.Empty;
        }

        XElement? agentState = root.Element("AgentSessionState");
        if (agentState is not null)
        {
            JsonNode state = JsonNode.Parse(agentState.Value)!;
            await TransformResourcesAsync(state, directory, false, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(directory, "AgentSession.json"), state.ToJsonString(), cancellationToken).ConfigureAwait(false);
            agentState.Remove();
        }
        else File.Delete(Path.Join(directory, "AgentSession.json"));

        XElement? protocolStates = root.Element("ProtocolStates");
        if (protocolStates is not null)
        {
            var items = new JsonArray();
            foreach (XElement item in protocolStates.Element("ResponsesState")!.Elements("Item"))
                items.Add(JsonNode.Parse(item.Value));
            await TransformResourcesAsync(items, directory, false, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Join(directory, "ResponsesSession.json"), items.ToJsonString(), cancellationToken).ConfigureAwait(false);
            protocolStates.Remove();
        }
        else File.Delete(Path.Join(directory, "ResponsesSession.json"));

        await FileCopilotChatSessionStore.SaveDocumentAsync(Path.Join(directory, "History.xml"), document, cancellationToken).ConfigureAwait(false);
        var entry = new XElement("CopilotChatSessionHistory", root.Attributes().Select(attribute => new XAttribute(attribute)));
        entry.SetAttributeValue("FormatVersion", FileCopilotChatSessionStore.CurrentFormatVersion);
        entry.SetAttributeValue("UpdatedTime", updatedTime.ToString("o"));
        entry.SetAttributeValue("ContentStorage", "Expanded");
        XElement[] entries = root.Element("Messages")!.Elements().ToArray();
        entry.SetAttributeValue("MessageCount", entries.Count(element => element.Name == "Message"));
        entry.SetAttributeValue("EntryCount", entries.Length);
        string? preview = entries.Where(element => element.Name == "Message"
                && ((string?)element.Attribute("Role") is "user" or "assistant"))
            .Select(element => (string?)element.Element("Content")).LastOrDefault(text => !string.IsNullOrWhiteSpace(text));
        if (preview is not null) entry.Add(new XElement("Summary", preview[..Math.Min(preview.Length, 200)]));
        await FileCopilotChatSessionStore.SaveDocumentAsync(entryPath, new XDocument(entry), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<XDocument> ReadAsync(string entryPath, CancellationToken cancellationToken)
    {
        await ExpandAsync(entryPath, cancellationToken).ConfigureAwait(false);
        string directory = Path.GetDirectoryName(entryPath)!;
        XDocument document = await ReadXmlAsync(Path.Join(directory, "History.xml"), cancellationToken).ConfigureAwait(false);
        XElement root = document.Root!;
        foreach (XElement resource in root.Descendants().Where(element => element.Attribute("Resource") is not null))
        {
            byte[] data = await File.ReadAllBytesAsync(Path.Join(directory, resource.Attribute("Resource")!.Value), cancellationToken).ConfigureAwait(false);
            resource.Value = Convert.ToBase64String(data);
            resource.Attribute("Resource")!.Remove();
        }
        string agentPath = Path.Join(directory, "AgentSession.json");
        if (File.Exists(agentPath))
        {
            JsonNode state = JsonNode.Parse(await File.ReadAllTextAsync(agentPath, cancellationToken).ConfigureAwait(false))!;
            await TransformResourcesAsync(state, directory, true, cancellationToken).ConfigureAwait(false);
            root.Add(new XElement("AgentSessionState", new XCData(state.ToJsonString())));
        }
        string responsesPath = Path.Join(directory, "ResponsesSession.json");
        if (File.Exists(responsesPath))
        {
            JsonArray items = JsonNode.Parse(await File.ReadAllTextAsync(responsesPath, cancellationToken).ConfigureAwait(false))!.AsArray();
            await TransformResourcesAsync(items, directory, true, cancellationToken).ConfigureAwait(false);
            root.Add(new XElement("ProtocolStates", new XElement("ResponsesState",
                items.Select(item => new XElement("Item", item!.ToJsonString())))));
        }
        return document;
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
        string extension = mimeType.ToLowerInvariant() switch
        {
            "image/png" => ".png", "image/jpeg" => ".jpg", "image/gif" => ".gif", "image/webp" => ".webp",
            "image/bmp" => ".bmp", "audio/wav" or "audio/x-wav" => ".wav", "audio/mpeg" => ".mp3",
            "audio/ogg" => ".ogg", "application/pdf" => ".pdf", _ => ".bin",
        };
        string resource = "Resources/" + Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant() + extension;
        string path = Path.Join(directory, resource);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) await File.WriteAllBytesAsync(path, data, cancellationToken).ConfigureAwait(false);
        return resource;
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
