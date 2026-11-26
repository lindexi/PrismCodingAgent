using AgentLib.Tools;

namespace AgentLib.Coding;

/// <summary>
/// 为一次编程运行创建一组附加工具。
/// </summary>
public interface ICodingWorkspaceToolSource
{
    /// <summary>根据本次运行上下文创建工具及其展示摘要规则。</summary>
    /// <param name="context">本次运行的上下文；工具源应捕获所需的选项值，不保留可变选项。</param>
    /// <returns>绑定该运行的工具注册集合。</returns>
    IReadOnlyList<ToolRegistration> CreateToolRegistrations(CodingRunContext context);
}
