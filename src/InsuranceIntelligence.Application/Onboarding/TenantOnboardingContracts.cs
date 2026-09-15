namespace InsuranceIntelligence.Application.Onboarding;

/// <summary>
/// Platform-operator tenant onboarding + Microsoft Entra admin-consent flow (identity spec §4,
/// ADR-0005). An IIP operator creates a pending Tenant record; the insurer admin completes tenant-
/// wide admin consent in their own Entra tenant; IIP stores the returned external tenant id as a
/// lookup key (never authority) and bootstraps the admin's membership in IIP's own store.
/// The actual Microsoft admin-consent redirect requires real Entra config; until then the consent
/// URL is reported as Not configured rather than fabricated.
/// </summary>
public interface ITenantOnboardingService
{
    Task<TenantOnboardingDto> CreatePendingAsync(CreateTenantOnboardingRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantOnboardingDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<TenantOnboardingDto?> GetAsync(string onboardingId, CancellationToken cancellationToken = default);
    Task<AdminConsentUrlDto> GetAdminConsentUrlAsync(string onboardingId, CancellationToken cancellationToken = default);
    Task<TenantOnboardingDto> CompleteConsentAsync(CompleteConsentRequest request, CancellationToken cancellationToken = default);
}

public interface ITenantOnboardingRepository
{
    Task UpsertAsync(TenantOnboardingRecord record, CancellationToken cancellationToken = default);
    Task<TenantOnboardingRecord?> GetAsync(string onboardingId, CancellationToken cancellationToken = default);
    Task<TenantOnboardingRecord?> GetByStateAsync(string state, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantOnboardingRecord>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Writes IIP's own membership/role records (bootstrap on consent). Runtime authority store.</summary>
public interface ITenantMembershipWriter
{
    Task CreateAdminMembershipAsync(string tenantId, string workspaceId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a person an active membership of a workspace with exactly one role, replacing any
    /// other role they held there. This is what actually lets someone use the workspace: the
    /// per-request membership lookup reads these records, not the user directory, so an admin
    /// adding a user without this never granted any access. Safe to call repeatedly.
    /// </summary>
    Task GrantMembershipAsync(string tenantId, string workspaceId, string userId, string roleCode, CancellationToken cancellationToken = default);
}

/// <summary>Non-secret Entra settings needed to build the admin-consent redirect. Supplied by the API layer.</summary>
public interface IOnboardingConfiguration
{
    string? EntraClientId { get; }
    string? AdminConsentRedirectUri { get; }
}

public enum TenantOnboardingStatus
{
    Pending,
    Consented,
    Active,
    Blocked
}

public sealed record TenantOnboardingRecord(
    string OnboardingId,
    string CustomerName,
    string ExpectedEntraTenantOrDomain,
    string AdminContactEmail,
    IReadOnlyList<string> TargetWorkspaces,
    IReadOnlyList<string> AzureSubscriptionIds,
    string InternalTenantId,
    string State,
    TenantOnboardingStatus Status,
    string? ExternalEntraTenantId,
    string? ConsentedByUserId,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConsentedAt);

public sealed record CreateTenantOnboardingRequest(
    string? CustomerName,
    string? ExpectedEntraTenantOrDomain,
    string? AdminContactEmail,
    IReadOnlyList<string>? TargetWorkspaces,
    IReadOnlyList<string>? AzureSubscriptionIds);

public sealed record CompleteConsentRequest(
    string State,
    string? Tenant,
    bool AdminConsent,
    string? AdminUserId);

public sealed record TenantOnboardingDto(
    string OnboardingId,
    string CustomerName,
    string ExpectedEntraTenantOrDomain,
    string AdminContactEmail,
    IReadOnlyList<string> TargetWorkspaces,
    IReadOnlyList<string> AzureSubscriptionIds,
    string InternalTenantId,
    string Status,
    string? ExternalEntraTenantId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConsentedAt);

public sealed record AdminConsentUrlDto(bool Configured, string? Url, string Message);

public sealed class TenantOnboardingValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
