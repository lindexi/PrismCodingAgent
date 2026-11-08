using AgentLib.Tools;

namespace AgentLib.Coding;

/// <summary>
/// 按代码工作区创建一组附加编程工具。
/// </summary>
public interface ICodingWorkspaceToolSource
{
    /// <summary>根据本次运行选项创建工具及其展示摘要规则。</summary>
    /// <param name="workspacePath">代码工作区的完整路径。</param>
    /// <param name="runOptions">本次运行选项；工具源应捕获所需值，不保留可变选项。</param>
    /// <returns>绑定该运行的工具注册集合。</returns>
    IReadOnlyList<ToolRegistration> CreateToolRegistrations(string workspacePath, CodingChatRunOptions runOptions);
}
