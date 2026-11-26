namespace AgentLib.Coding.Tools.ExecuteCommands;

internal interface ICommandHandler
{
    string Topic { get; }
    string Summary { get; }
    string Usage { get; }
    bool CanHandle(ReadOnlySpan<string> argv);

    Task<string> RunAsync(ReadOnlyMemory<string> argv, CancellationToken cancellationToken);
}
