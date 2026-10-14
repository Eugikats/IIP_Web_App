using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Compliance;

/// <summary>
/// A workspace-wide legal hold: while it is on, retention deletes nothing. For a claim in dispute, an investigation or
/// litigation, records must not quietly expire. Placing and releasing a hold needs a stated reason and is audited.
/// Workspace-wide is deliberately blunt: it is easy to understand and cannot miss a record. A hold on one claim or one
/// conversation is a later refinement.
/// </summary>
public sealed record LegalHold
{
    public required TenantScope Scope { get; init; }
    public bool Active { get; init; }
    public string? Reason { get; init; }
    public string? PlacedBy { get; init; }
    public DateTimeOffset? PlacedAt { get; init; }
    public string? ReleasedBy { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }

    public static Guid StorageKey(TenantScope scope) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.WorkspaceId}:legal-hold")));
}

public interface ILegalHoldRepository
{
    Task<LegalHold?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(LegalHold hold, CancellationToken cancellationToken = default);
}
