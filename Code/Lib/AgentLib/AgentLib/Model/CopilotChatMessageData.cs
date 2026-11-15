using Microsoft.Extensions.AI;

namespace AgentLib.Model;

internal sealed record CopilotChatMessageData
{
    public required ChatRole Role { get; init; }
    public required DateTimeOffset CreatedTime { get; init; }
    public bool IsPresetInfo { get; init; }
    public required IEnumerable<ICopilotChatMessageItem> MessageItems { get; init; }
    public UsageDetails? TotalUsageDetails { get; init; }
    public Guid? SessionSnapshotId { get; init; }
}
