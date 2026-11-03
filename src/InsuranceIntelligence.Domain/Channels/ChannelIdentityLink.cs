using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Channels;

public enum ChannelIdentityVerificationMethod
{
    StaffVerifiedCustomerChallenge,
}

/// <summary>
/// A staff-verified link between one external channel recipient and one IIP customer. A name,
/// phone number, email address, or social handle is never enough by itself to create this link.
/// </summary>
public sealed record ChannelIdentityLink
{
    public required Guid LinkId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string CustomerId { get; init; }
    public required string Channel { get; init; }
    public required string AccountId { get; init; }
    public required string ExternalRecipientId { get; init; }
    public required ChannelIdentityVerificationMethod VerificationMethod { get; init; }
    public required string EvidenceReference { get; init; }
    public required string ConsentPurpose { get; init; }
    public required string LinkedByUserId { get; init; }
    public required DateTimeOffset LinkedAt { get; init; }
    public bool IsActive { get; init; } = true;
    public string? RevokedByUserId { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }

    public ChannelIdentityLink Revoke(string actorUserId, DateTimeOffset at)
    {
        if (!IsActive) throw new InvalidOperationException("This channel identity link is already revoked.");
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("An accountable actor is required.", nameof(actorUserId));
        return this with { IsActive = false, RevokedByUserId = actorUserId.Trim(), RevokedAt = at };
    }
}

public interface IChannelIdentityLinkRepository
{
    Task<ChannelIdentityLink?> FindByExternalIdentityAsync(
        TenantScope scope, string channel, string accountId, string externalRecipientId,
        CancellationToken cancellationToken = default);
    Task<ChannelIdentityLink?> GetAsync(TenantScope scope, Guid linkId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelIdentityLink>> ListForCustomerAsync(
        TenantScope scope, string customerId, CancellationToken cancellationToken = default);
    Task SaveAsync(ChannelIdentityLink link, CancellationToken cancellationToken = default);
}

public static class ChannelIdentityConsentPurposes
{
    public const string CrossChannelContext = "cross_channel_context";

    public static string ForChannel(string channel) => $"{CrossChannelContext}:{channel.Trim().ToLowerInvariant()}";
}
