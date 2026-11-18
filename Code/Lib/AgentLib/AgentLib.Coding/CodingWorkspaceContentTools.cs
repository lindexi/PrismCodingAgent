using System.ComponentModel;

using AgentLib.Model;
using AgentLib.Tools;

using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

internal sealed class CodingWorkspaceContentTools
{
    private readonly string _workspacePath;

    internal CodingWorkspaceContentTools(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("代码工作区路径不能为空。", nameof(workspacePath));
        }

        _workspacePath = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(_workspacePath))
        {
            throw new DirectoryNotFoundException("指定的代码工作区不存在。");
        }
    }

    internal IReadOnlyList<AITool> AsAITools() => AsToolRegistrations().Select(registration => registration.Tool).ToArray();

    internal IReadOnlyList<ToolRegistration> AsToolRegistrations() =>
    [
        new(AIFunctionFactory.Create(LoadImageAsync, "load_image"), arguments => ToolCallPresentationFactory.ForPath(arguments, "filePath"))
    ];

    [Description("从图片文件加载多模态图片内容。")]
    internal async Task<DataContent> LoadImageAsync(
        [Description("图片文件路径。可以传绝对路径；相对路径则相对于代码工作区。")]
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("图片文件路径不能为空。", nameof(filePath));
        }

        DataContent content = await LoadImageContentAsync(filePath, _workspacePath, cancellationToken)
            .ConfigureAwait(false);
        if (content.MediaType is null || !content.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("指定文件不是受支持的图片文件。");
        }

        return content;
    }

    internal static ValueTask<DataContent> LoadImageContentAsync(
        string filePath, string? workspacePath, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(Path.IsPathRooted(filePath) || workspacePath is null
            ? filePath
            : Path.Join(workspacePath, filePath));
        return DataContent.LoadFromAsync(fullPath, cancellationToken: cancellationToken);
    }
}
