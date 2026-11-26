namespace AgentLib.Coding.Commands;

internal interface ICommandHandler
{
    string Topic { get; }
    string Usage { get; }
    bool CanHandle(ReadOnlySpan<string> argv);

    Task<string> RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken);
}
