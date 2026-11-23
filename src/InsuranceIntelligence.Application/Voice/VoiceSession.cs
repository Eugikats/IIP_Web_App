using System.Threading.Channels;
using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Application.Voice;

/// <summary>Something the phone should show during the call: a chart, a table, a draft, or a button to open a screen.</summary>
public sealed record VoiceScreenEvent(string Kind, object Data);

/// <summary>A place the call may offer to open, taken only from an answer the agent really produced.</summary>
public sealed record VoiceLink(string Label, string Href);

/// <summary>
/// One live call. It belongs to the person who opened it and carries their tenant context, because the
/// socket that serves it runs outside any web request: every tool call is made as this person, in this workspace.
/// </summary>
public sealed class VoiceSession
{
    private readonly List<VoiceLink> links = [];
    private readonly object linkGate = new();

    public VoiceSession(string id, TenantContext context, Guid? conversationId, int? timeZoneOffsetMinutes, VoiceSessionLease lease, TimeProvider clock)
    {
        Id = id;
        Context = context;
        ConversationId = conversationId;
        TimeZoneOffsetMinutes = timeZoneOffsetMinutes;
        Lease = lease;
        StartedAt = clock.GetUtcNow();
        LastActivityAt = StartedAt;
    }

    public string Id { get; }

    public TenantContext Context { get; }

    /// <summary>The written conversation this call adds to, so a spoken exchange is in the person's chats like a typed one.</summary>
    public Guid? ConversationId { get; set; }

    public int? TimeZoneOffsetMinutes { get; }

    public VoiceSessionLease Lease { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>True while the phone is locked. No figures are given while it is.</summary>
    public bool Locked { get; set; }

    public bool EndRequested { get; set; }

    public int ToolCalls { get; set; }

    public Channel<VoiceScreenEvent> Events { get; } =
        Channel.CreateBounded<VoiceScreenEvent>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public CancellationTokenSource Cancellation { get; } = new();

    public bool IsOwnedBy(TenantContext other) =>
        string.Equals(Context.TenantId, other.TenantId, StringComparison.Ordinal)
        && string.Equals(Context.WorkspaceId, other.WorkspaceId, StringComparison.Ordinal)
        && string.Equals(Context.UserId, other.UserId, StringComparison.Ordinal);

    public void RememberLinks(IEnumerable<VoiceLink> found)
    {
        lock (linkGate)
        {
            foreach (var link in found)
            {
                if (!links.Any(existing => existing.Href == link.Href))
                {
                    links.Add(link);
                }
            }

            if (links.Count > 40)
            {
                links.RemoveRange(0, links.Count - 40);
            }
        }
    }

    public VoiceLink? FindLink(string label)
    {
        lock (linkGate)
        {
            return links.LastOrDefault(link => link.Label.Contains(label, StringComparison.OrdinalIgnoreCase));
        }
    }
}
