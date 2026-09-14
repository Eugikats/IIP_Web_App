using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Fixed-value test double for IWhatsAppRuntimeSettingsProvider, no DB, no Data
/// Protection, just whatever WhatsAppEffectiveSettings the test constructs.</summary>
public sealed class StaticWhatsAppRuntimeSettingsProvider(WhatsAppEffectiveSettings? settings) : IWhatsAppRuntimeSettingsProvider
{
    public WhatsAppEffectiveSettings? Current { get; } = settings;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
