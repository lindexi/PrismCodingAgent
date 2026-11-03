using System.Security.Cryptography;

namespace AgentLib.Logging;

internal sealed class SessionResources
{
    private readonly Dictionary<string, BinaryData> _items = [];

    internal string Add(BinaryData data, string mediaType)
    {
        string extension = mediaType.ToLowerInvariant() switch
        {
            "image/png" => ".png", "image/jpeg" => ".jpg", "image/gif" => ".gif", "image/webp" => ".webp",
            "image/bmp" => ".bmp", "audio/wav" or "audio/x-wav" => ".wav", "audio/mpeg" => ".mp3",
            "audio/ogg" => ".ogg", "application/pdf" => ".pdf", _ => ".bin",
        };
        string reference = "Resources/" + Convert.ToHexString(SHA1.HashData(data.ToMemory().Span)).ToLowerInvariant() + extension;
        _items.TryAdd(reference, data);
        return reference;
    }

    internal BinaryData Get(string reference) => _items[reference];

    internal async Task WriteAsync(string directory, CancellationToken cancellationToken)
    {
        foreach (var (reference, data) in _items)
        {
            string path = Path.Join(directory, reference);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path))
            {
                await using var stream = File.Create(path);
                await stream.WriteAsync(data.ToMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task ReadAsync(string directory, IEnumerable<string> references, CancellationToken cancellationToken)
    {
        foreach (string reference in references.Distinct())
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Join(directory, reference), cancellationToken).ConfigureAwait(false);
            _items.Add(reference, BinaryData.FromBytes(bytes));
        }
    }
}
