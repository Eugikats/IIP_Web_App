using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Fixed-value test double for IWhatsAppRuntimeSettingsProvider, no DB, no Data
/// Protection, just whatever WhatsAppEffectiveSettings the test constructs.</summary>
public sealed class StaticWhatsAppRuntimeSettingsProvider(WhatsAppEffectiveSettings? settings) : IWhatsAppRuntimeSettingsProvider
{
    public IReadOnlyList<WhatsAppEffectiveSettings> All => settings is null ? [] : [settings];
    public WhatsAppEffectiveSettings? Current => settings;

    public bool HasAny => settings is not null;

    public WhatsAppEffectiveSettings? ForScope(InsuranceIntelligence.Domain.Common.TenantScope scope) =>
        settings is not null && settings.TenantId == scope.TenantId && settings.WorkspaceId == scope.WorkspaceId ? settings : null;

    public WhatsAppEffectiveSettings? ForPhoneNumber(string phoneNumberId) =>
        settings is not null && settings.PhoneNumberId == phoneNumberId ? settings : null;

    public WhatsAppEffectiveSettings? ForVerifyToken(string verifyToken) =>
        settings is not null && settings.VerifyToken == verifyToken ? settings : null;

    public Task<WhatsAppEffectiveSettings?> FindByPhoneNumberAsync(string phoneNumberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ForPhoneNumber(phoneNumberId));

    public Task RefreshIfStaleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
