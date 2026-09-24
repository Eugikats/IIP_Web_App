using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Onboarding;

public sealed class TenantOnboardingService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ITenantOnboardingRepository repository,
    ITenantMembershipWriter membershipWriter,
    IOnboardingConfiguration onboardingConfiguration) : ITenantOnboardingService
{
    public async Task<TenantOnboardingDto> CreatePendingAsync(CreateTenantOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantOnboarding, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new TenantOnboardingValidationException(nameof(request.CustomerName), "Customer name is required.");
        }
        if (string.IsNullOrWhiteSpace(request.AdminContactEmail))
        {
            throw new TenantOnboardingValidationException(nameof(request.AdminContactEmail), "An initial admin contact email is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var onboardingId = $"ONB-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var record = new TenantOnboardingRecord(
            OnboardingId: onboardingId,
            CustomerName: request.CustomerName.Trim(),
            ExpectedEntraTenantOrDomain: request.ExpectedEntraTenantOrDomain?.Trim() ?? string.Empty,
            AdminContactEmail: request.AdminContactEmail.Trim(),
            TargetWorkspaces: Clean(request.TargetWorkspaces) is { Count: > 0 } ws ? ws : ["workspace-claims"],
            AzureSubscriptionIds: Clean(request.AzureSubscriptionIds),
            InternalTenantId: $"tenant-{Guid.NewGuid().ToString("N")[..10]}",
            State: Guid.NewGuid().ToString("N"),
            Status: TenantOnboardingStatus.Pending,
            ExternalEntraTenantId: null,
            ConsentedByUserId: null,
            CreatedByUserId: tenantContextAccessor.Current.UserId,
            CreatedAt: now,
            ConsentedAt: null);

        await repository.UpsertAsync(record, cancellationToken);
        return ToDto(record);
    }

    public async Task<IReadOnlyList<TenantOnboardingDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantOnboarding, cancellationToken);
        var records = await repository.ListAsync(cancellationToken);
        return records.OrderByDescending(r => r.CreatedAt).Select(ToDto).ToArray();
    }

    public async Task<TenantOnboardingDto?> GetAsync(string onboardingId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantOnboarding, cancellationToken);
        var record = await repository.GetAsync(onboardingId, cancellationToken);
        return record is null ? null : ToDto(record);
    }

    public async Task<AdminConsentUrlDto> GetAdminConsentUrlAsync(string onboardingId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantOnboarding, cancellationToken);
        var record = await repository.GetAsync(onboardingId, cancellationToken)
            ?? throw new TenantOnboardingValidationException(nameof(onboardingId), "Onboarding record not found.");

        var clientId = onboardingConfiguration.EntraClientId;
        var redirect = onboardingConfiguration.AdminConsentRedirectUri;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirect))
        {
            return new AdminConsentUrlDto(false, null,
                "Microsoft Entra admin consent is not configured. Set the app registration client id and the onboarding redirect URI (see the Gate 1 provisioning runbook).");
        }

        // Per identity spec §4.2: never use 'common' for admin consent, use the insurer's tenant.
        var directory = string.IsNullOrWhiteSpace(record.ExpectedEntraTenantOrDomain)
            ? "organizations"
            : record.ExpectedEntraTenantOrDomain;
        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(directory)}/adminconsent"
            + $"?client_id={Uri.EscapeDataString(clientId!)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirect!)}"
            + $"&state={Uri.EscapeDataString(record.State)}";
        return new AdminConsentUrlDto(true, url, "Redirect the insurer administrator to this URL to grant tenant-wide consent.");
    }

    public async Task<TenantOnboardingDto> CompleteConsentAsync(CompleteConsentRequest request, CancellationToken cancellationToken = default)
    {
        // Not permission-gated: this is the OAuth redirect target, authorized by the opaque state value.
        if (string.IsNullOrWhiteSpace(request.State))
        {
            throw new TenantOnboardingValidationException(nameof(request.State), "Missing consent state.");
        }

        var record = await repository.GetByStateAsync(request.State, cancellationToken)
            ?? throw new TenantOnboardingValidationException(nameof(request.State), "Consent state did not match a pending onboarding session.");

        if (!request.AdminConsent)
        {
            var blocked = record with { Status = TenantOnboardingStatus.Blocked };
            await repository.UpsertAsync(blocked, cancellationToken);
            throw new TenantOnboardingValidationException("adminConsent", "Admin consent was not granted.");
        }

        var adminUserId = string.IsNullOrWhiteSpace(request.AdminUserId) ? record.AdminContactEmail : request.AdminUserId!;
        var now = DateTimeOffset.UtcNow;
        var consented = record with
        {
            Status = TenantOnboardingStatus.Consented,
            ExternalEntraTenantId = string.IsNullOrWhiteSpace(request.Tenant) ? record.ExpectedEntraTenantOrDomain : request.Tenant,
            ConsentedByUserId = adminUserId,
            ConsentedAt = now,
        };
        await repository.UpsertAsync(consented, cancellationToken);

        // Bootstrap the consenting admin's membership in IIP's own store (runtime authority, ADR-0005).
        var workspace = consented.TargetWorkspaces.Count > 0 ? consented.TargetWorkspaces[0] : "workspace-claims";
        await membershipWriter.CreateAdminMembershipAsync(consented.InternalTenantId, workspace, adminUserId, cancellationToken);

        return ToDto(consented);
    }

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToArray();

    private static TenantOnboardingDto ToDto(TenantOnboardingRecord r) =>
        new(r.OnboardingId, r.CustomerName, r.ExpectedEntraTenantOrDomain, r.AdminContactEmail,
            r.TargetWorkspaces, r.AzureSubscriptionIds, r.InternalTenantId, r.Status.ToString(),
            r.ExternalEntraTenantId, r.CreatedAt, r.ConsentedAt);
}
