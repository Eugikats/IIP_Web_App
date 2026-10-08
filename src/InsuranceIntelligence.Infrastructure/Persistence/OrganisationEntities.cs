namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class OrganisationEntity
{
    public Guid Id { get; set; }
    public string OrganisationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DefaultWorkspaceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? MarketplaceSubscriptionId { get; set; }
    public string Product { get; set; } = nameof(InsuranceIntelligence.Domain.Organisations.OrganisationProduct.Insurance);
}

public sealed class OrganisationInvitationEntity
{
    public Guid Id { get; set; }
    public string OrganisationId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InvitedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

public sealed class BrokerLinkEntity
{
    public Guid Id { get; set; }
    public string BrokerOrganisationId { get; set; } = string.Empty;
    public string InsurerOrganisationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InitiatedBy { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string? DecidedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
