using System.Threading.Channels;

namespace InsuranceIntelligence.Application.CommandCenter;

/// <summary>
/// One thing the agent is doing right now, in words a person can read ("Counting claims this week").
/// Steps describe real work the agent performed; none is invented to fill time.
/// </summary>
public sealed record AgentStep(string Kind, string Label);

/// <summary>Where an agent reports what it is doing, so a person watching can follow along.</summary>
public interface IAgentProgress
{
    /// <summary>True when somebody is watching. Callers may skip building a label when it is not.</summary>
    bool IsWatched { get; }

    void Report(string kind, string label);
}

/// <summary>
/// A per-request progress feed. It stays silent (and holds nothing) until a streaming endpoint opens it,
/// so the ordinary request and response path is unchanged.
/// </summary>
public sealed class AgentProgress : IAgentProgress
{
    private Channel<AgentStep>? channel;

    public bool IsWatched => channel is not null;

    public ChannelReader<AgentStep> Open()
    {
        channel ??= Channel.CreateUnbounded<AgentStep>(new UnboundedChannelOptions { SingleReader = true });
        return channel.Reader;
    }

    public void Report(string kind, string label) => channel?.Writer.TryWrite(new AgentStep(kind, label));

    public void Complete() => channel?.Writer.TryComplete();
}
