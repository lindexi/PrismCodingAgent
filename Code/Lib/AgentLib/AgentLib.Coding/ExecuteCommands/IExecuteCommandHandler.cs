namespace AgentLib.Coding.ExecuteCommands;

internal interface IExecuteCommandHandler
{
    /// <summary>执行与当前处理器匹配的命令，并将调用方取消传递给底层操作。</summary>
    Task<string> HandleAsync(ReadOnlyMemory<string> arguments, CancellationToken cancellationToken);
}
