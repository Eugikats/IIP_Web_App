using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Compliance;

/// <summary>
/// A data class this platform actually stores and can delete on a schedule (blueprint §14 Phase
/// 6, WO 6.3). Deliberately small: only a category this codebase has real, safe delete capability
/// for belongs here, adding a data class to this enum without also giving
/// RetentionEnforcementService a real deletion path for it is a contract this type cannot enforce
/// by itself, so keep the two in lockstep.
/// </summary>
public enum RetentionDataClass
{
    /// <summary>Raw inbound/outbound WhatsApp message text captured by CustomerAssistantService,
    /// genuinely personal customer conversation content, not the immutable audit trail. Deleting a
    /// turn here never removes anything from AuditEvents; the fact that a turn existed and was
    /// later deleted is exactly what RetentionEnforcementService's own audit write preserves.</summary>
    CustomerConversationTurns,
}

/// <summary>
/// One tenant's retention window for one data class, current state, not append-only history
/// (matches WhatsAppChannelConfiguration's one-row-per-tenant pattern, not AgentBehaviourProfile's
/// versioned one): a retention *setting* changing over time is not itself the compliance-relevant
/// fact here, the actual deletions are, and those already get their own audit trail via
/// RetentionEnforcementService.
/// </summary>
public sealed record RetentionPolicy
{
    public required TenantScope Scope { get; init; }
    public required RetentionDataClass DataClass { get; init; }
    public required int RetentionDays { get; init; }
    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Conservative built-in default so enforcement has a real answer even for a tenant
    /// that has never configured this data class explicitly, 365 days for customer conversation
    /// content, a common operational/regulatory retention window for this kind of record.</summary>
    public static int DefaultRetentionDays(RetentionDataClass dataClass) => dataClass switch
    {
        RetentionDataClass.CustomerConversationTurns => 365,
        _ => 365,
    };

    public static Guid StorageKey(TenantScope scope, RetentionDataClass dataClass)
    {
        var key = $"{scope.TenantId}:{scope.WorkspaceId}:retention-policy:{dataClass}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}

public interface IRetentionPolicyRepository
{
    /// <summary>Null means never explicitly configured, RetentionEnforcementService falls back
    /// to RetentionPolicy.DefaultRetentionDays, it never treats "unconfigured" as "retain
    /// forever."</summary>
    Task<RetentionPolicy?> GetAsync(TenantScope scope, RetentionDataClass dataClass, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RetentionPolicy>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(RetentionPolicy policy, CancellationToken cancellationToken = default);
}

/// <summary>What one enforcement run actually did for one data class, always written to the audit
/// trail (blueprint's own exit test: "the retention job deletes an expired record and the deletion
/// is in the audit trail"), whether it deleted anything or not, so a quiet run is provably quiet
/// rather than provably absent.</summary>
public sealed record RetentionEnforcementResult(
    RetentionDataClass DataClass,
    int RetentionDaysApplied,
    int RecordsDeleted,
    DateTimeOffset? OldestDeletedOccurredAt,
    DateTimeOffset? NewestDeletedOccurredAt,
    DateTimeOffset RunAt);

/// <summary>What a data class's own repository actually removed for one enforcement pass, the
/// repository is the only thing that knows how many rows matched and their real timestamps, so it
/// hands this back rather than the enforcement service re-deriving it.</summary>
public sealed record RetentionDeletionSummary(int Count, DateTimeOffset? OldestOccurredAt, DateTimeOffset? NewestOccurredAt)
{
    public static readonly RetentionDeletionSummary None = new(0, null, null);
}
