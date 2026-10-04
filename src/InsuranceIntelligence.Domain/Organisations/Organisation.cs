using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Organisations;

public enum OrganisationType
{
    Insurer,
    Broker,
}

/// <summary>
/// An insurer or a broker that signed up. Its id is the tenant id everything it owns is stored
/// under, so "which organisation is this?" and "which tenant is this?" are the same question.
/// </summary>
public sealed record Organisation(
    string OrganisationId,
    string Name,
    OrganisationType Type,
    string DefaultWorkspaceId,
    string Status,
    string PlanId,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    string? MarketplaceSubscriptionId = null)
{
    public const string ActiveStatus = "Active";
    public const string SuspendedStatus = "Suspended";
    public const string DefaultWorkspace = "main";
    public const string TrialPlan = "trial";

    public TenantScope Scope => TenantScope.Create(OrganisationId, DefaultWorkspaceId);

    public bool IsActive => string.Equals(Status, ActiveStatus, StringComparison.Ordinal);
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
}

/// <summary>An invitation for one person, by email, to join one organisation in one role. Only a
/// hash of the secret is stored; the secret itself is shown once, to the person who invited.</summary>
public sealed record OrganisationInvitation(
    Guid InvitationId,
    string OrganisationId,
    string Email,
    string RoleCode,
    string TokenHash,
    InvitationStatus Status,
    string InvitedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? AcceptedByUserId = null,
    DateTimeOffset? AcceptedAt = null);

public enum BrokerLinkStatus
{
    Requested,
    Active,
    Declined,
    Revoked,
}

public enum LinkInitiator
{
    Broker,
    Insurer,
}

/// <summary>
/// A broker organisation's permission to work with an insurer organisation. It takes both sides:
/// one asks, the other approves. While it is Active, the broker's people can act in the insurer's
/// workspace as brokers (submit claims for their clients, nothing more); the moment it is revoked
/// that access ends, because access is worked out from this record on every request.
/// </summary>
public sealed record BrokerLink(
    Guid LinkId,
    string BrokerOrganisationId,
    string InsurerOrganisationId,
    BrokerLinkStatus Status,
    LinkInitiator InitiatedBy,
    string RequestedByUserId,
    string? DecidedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A person's direct membership of an organisation, with the role they hold there.</summary>
public sealed record OrganisationMembership(string OrganisationId, string WorkspaceId, IReadOnlyList<string> RoleCodes);

public interface IOrganisationRepository
{
    Task AddAsync(Organisation organisation, CancellationToken cancellationToken = default);
    Task<Organisation?> GetAsync(string organisationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organisation>> GetManyAsync(IEnumerable<string> organisationIds, CancellationToken cancellationToken = default);
    Task<int> CountCreatedByAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganisationMembership>> ListMembershipsForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListMemberUserIdsAsync(string organisationId, CancellationToken cancellationToken = default);

    Task AddInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken = default);
    Task UpdateInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken = default);
    Task<OrganisationInvitation?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<OrganisationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganisationInvitation>> ListInvitationsAsync(string organisationId, CancellationToken cancellationToken = default);

    Task AddLinkAsync(BrokerLink link, CancellationToken cancellationToken = default);
    Task UpdateLinkAsync(BrokerLink link, CancellationToken cancellationToken = default);
    Task<BrokerLink?> GetLinkAsync(Guid linkId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrokerLink>> ListLinksForOrganisationAsync(string organisationId, CancellationToken cancellationToken = default);
}
