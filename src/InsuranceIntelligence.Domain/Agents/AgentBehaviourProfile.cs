using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Agents;

/// <summary>
/// A tenant's configuration of one agent's voice and scope — never its authority. Every save is a
/// new, append-only version (never overwritten in place), so the behaviour screen can show real
/// version history with a diff and the reason for each change (blueprint §6.3, §14 Phase 3).
///
/// The composed prompt an agent actually runs with is [immutable platform preamble] + [this
/// profile's SystemPromptOverride, or the agent's built-in default] + [refusal-topic handling] —
/// see AgentBehaviourComposer. A tenant can shape what an agent talks about and how; it can never
/// remove the human-decision boundary, because that boundary is not part of this record at all —
/// it lives in code the tenant's data never touches.
/// </summary>
public sealed record AgentBehaviourProfile
{
    public required Guid VersionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string AgentKey { get; init; }
    public required int Version { get; init; }

    /// <summary>Replaces the agent's built-in default system prompt when set. Null means "use the
    /// platform default for this agent" — not "no instructions at all".</summary>
    public string? SystemPromptOverride { get; init; }

    /// <summary>Free-text tone guidance ("formal", "plain-language", or a tenant's own words) —
    /// folded into the composed prompt, never interpreted as a permission grant.</summary>
    public string? Persona { get; init; }

    public string Language { get; init; } = "en";
    public IReadOnlyList<string> AllowedTopics { get; init; } = [];

    /// <summary>Topics this agent must decline and hand off, using EscalationMessage.</summary>
    public IReadOnlyList<string> RefusalTopics { get; init; } = [];
    public string EscalationMessage { get; init; } = "I'm not able to help with that directly — let me connect you with a member of our team.";

    /// <summary>Response length discipline for SMS/WhatsApp-bound output. Null means no additional
    /// limit beyond the model's own.</summary>
    public int? MaxResponseCharacters { get; init; }

    /// <summary>Cannot be set false by a tenant for any agent that answers coverage/policy
    /// questions — enforced in AgentBehaviourEndpoints.SaveVersion, not just documented here.</summary>
    public bool RequireCitation { get; init; } = true;

    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string ChangeReason { get; init; }
}

public interface IAgentBehaviourProfileRepository
{
    /// <summary>The highest-Version row for this (scope, agentKey), or null if never configured —
    /// in which case the agent runs on its built-in default prompt alone.</summary>
    Task<AgentBehaviourProfile?> GetCurrentAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default);

    /// <summary>Every saved version for this agent, newest first.</summary>
    Task<IReadOnlyCollection<AgentBehaviourProfile>> ListVersionsAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default);

    /// <summary>The current version for every agent this tenant has ever configured (for the
    /// admin agent list — which agents have a customized profile at all).</summary>
    Task<IReadOnlyCollection<AgentBehaviourProfile>> ListCurrentForAllAgentsAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Always inserts a new version row — never overwrites a previous one.</summary>
    Task SaveAsync(AgentBehaviourProfile version, CancellationToken cancellationToken = default);
}
