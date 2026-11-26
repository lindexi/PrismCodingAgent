using System.Text;

namespace AgentLib.Coding.ExecuteCommands;

internal static class CommandResult
{
    internal static string Format(string status, string? command = null, int? exitCode = null, string? detail = null,
        string? standardOutput = null, string? standardError = null, bool truncated = false)
    {
        var result = new StringBuilder().AppendLine("<MetaData>").Append("状态: ").AppendLine(status);
        if (command is not null) result.Append("命令: ").AppendLine(command);
        if (exitCode is { } code) result.Append("退出码: ").Append(code).AppendLine();
        if (truncated) result.AppendLine("输出: 已截断，请缩小查询范围。");
        result.Append("</MetaData>");
        if (!string.IsNullOrEmpty(detail)) result.AppendLine().Append(detail);
        if (!string.IsNullOrEmpty(standardOutput)) result.AppendLine().Append(standardOutput);
        if (!string.IsNullOrEmpty(standardError)) result.AppendLine().AppendLine("标准错误:").Append(standardError);
        return result.ToString();
    }
}
