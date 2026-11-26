namespace AgentLib.Coding.Commands;

internal interface ICommandHandler
{
    bool CanHandle(ReadOnlySpan<string> argv);

    Task<string> RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken);
}
