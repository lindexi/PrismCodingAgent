using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI.Responses;

#pragma warning disable OPENAI001, SCME0001

namespace AgentLib.Logging;

internal static class ResponsesSessionJsonCodec
{
    internal static ResponseItem ReadItem(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        // SDK 的 FunctionOutput 字符串属性通过 JSON Patch 保留多模态结果。
        if (root.GetProperty("type").GetString() == "function_call_output"
            && root.TryGetProperty("output", out JsonElement output) && output.ValueKind == JsonValueKind.Array)
        {
            var properties = root.EnumerateObject().ToDictionary(property => property.Name,
                property => property.Name == "output" ? (object)string.Empty : property.Value.Clone());
            ResponseItem item = ModelReaderWriter.Read<ResponseItem>(BinaryData.FromObjectAsJson(properties))
                ?? throw new InvalidDataException("Responses 历史 Item 不能为空。");
            item.Patch.Set("$.output"u8, BinaryData.FromString(output.GetRawText()));
            return item;
        }
        return ModelReaderWriter.Read<ResponseItem>(BinaryData.FromString(json))
            ?? throw new InvalidDataException("Responses 历史 Item 不能为空。");
    }
}
