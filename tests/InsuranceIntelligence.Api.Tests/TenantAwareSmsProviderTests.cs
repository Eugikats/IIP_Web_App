using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Communications;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Behavioral coverage of the three-step resolution order from
/// docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md §3.2: an active
/// tenant IntegrationConnection first, then the deployment's environment-configured fallback,
/// then Not configured. Also covers the bug this whole model exists to fix:
/// ICommunicationSendingProvider.SendSmsAsync previously ignored SmsProvider/Connection state
/// entirely and always returned ConfigurationRequired, see CommunicationSendingProviderSmsTests.
/// </summary>
public sealed class TenantAwareSmsProviderTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task SendAsync_NoConnectionAndNoFallback_ReturnsConfigurationRequired()
    {
        var provider = new TenantAwareSmsProvider(
            new StubConnectionRepository(connection: null),
            new StubProtector(),
            new StubFactory(fallback: new StubResultProvider("ConfigurationRequired")),
            NullLogger<TenantAwareSmsProvider>.Instance);

        var result = await provider.SendAsync(Request());

        Assert.Equal("ConfigurationRequired", result.DeliveryStatus);
    }

    [Fact]
    public async Task SendAsync_NoConnection_FallsBackToEnvironmentConfiguredProvider()
    {
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareSmsProvider(
            new StubConnectionRepository(connection: null),
            new StubProtector(),
            new StubFactory(fallback),
            NullLogger<TenantAwareSmsProvider>.Instance);

        var result = await provider.SendAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(fallback.WasCalled);
    }

    [Fact]
    public async Task SendAsync_ActiveConnection_UsesConnectionResolvedProviderNotFallback()
    {
        var connection = new IntegrationConnection
        {
            Scope = Scope,
            ConnectionType = ConnectionType.Sms,
            ProviderKey = "EgoSms",
            EncryptedSecretSettingsJson = "encrypted",
            IsActive = true,
            ConfiguredByUserId = "admin-1",
            ConfiguredAt = DateTimeOffset.UtcNow,
        };
        var connectionResolved = new StubResultProvider("Sent");
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareSmsProvider(
            new StubConnectionRepository(connection),
            new StubProtector(),
            new StubFactory(fallback, providerForKey: ("EgoSms", connectionResolved)),
            NullLogger<TenantAwareSmsProvider>.Instance);

        var result = await provider.SendAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(connectionResolved.WasCalled);
        Assert.False(fallback.WasCalled);
    }

    [Fact]
    public async Task SendAsync_ActiveConnectionWithUnrecognizedProviderKey_ReturnsConfigurationRequiredWithoutFallingBack()
    {
        var connection = new IntegrationConnection
        {
            Scope = Scope,
            ConnectionType = ConnectionType.Sms,
            ProviderKey = "SomeRemovedProvider",
            EncryptedSecretSettingsJson = "encrypted",
            IsActive = true,
            ConfiguredByUserId = "admin-1",
            ConfiguredAt = DateTimeOffset.UtcNow,
        };
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareSmsProvider(
            new StubConnectionRepository(connection),
            new StubProtector(),
            new StubFactory(fallback), // factory.Create returns null for unrecognized keys
            NullLogger<TenantAwareSmsProvider>.Instance);

        var result = await provider.SendAsync(Request());

        Assert.Equal("ConfigurationRequired", result.DeliveryStatus);
        Assert.False(fallback.WasCalled); // must not silently send through the wrong (fallback) account
    }

    [Fact]
    public async Task SendAsync_InactiveConnection_FallsBackToEnvironmentConfiguredProvider()
    {
        var inactiveConnection = new IntegrationConnection
        {
            Scope = Scope,
            ConnectionType = ConnectionType.Sms,
            ProviderKey = "EgoSms",
            EncryptedSecretSettingsJson = "encrypted",
            IsActive = false,
            ConfiguredByUserId = "admin-1",
            ConfiguredAt = DateTimeOffset.UtcNow,
        };
        var connectionResolved = new StubResultProvider("Sent");
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareSmsProvider(
            new StubConnectionRepository(inactiveConnection),
            new StubProtector(),
            new StubFactory(fallback, providerForKey: ("EgoSms", connectionResolved)),
            NullLogger<TenantAwareSmsProvider>.Instance);

        var result = await provider.SendAsync(Request());

        Assert.True(fallback.WasCalled);
        Assert.False(connectionResolved.WasCalled);
    }

    private static CommunicationSendRequest Request() =>
        new(Scope, Guid.NewGuid(), CommunicationChannel.SMS, "+256772118001", "Test message", "officer-1", Approved: true, LowRisk: false, "corr-1");

    private sealed class StubConnectionRepository(IntegrationConnection? connection) : IIntegrationConnectionRepository
    {
        public Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) =>
            Task.FromResult(connection);
        public Task SaveAsync(IntegrationConnection connectionToSave, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubProtector : IConnectionSecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => "{}";
    }

    private sealed class StubFactory(ISmsProvider fallback, (string Key, ISmsProvider Provider)? providerForKey = null) : ISmsProviderFactory
    {
        public IReadOnlyCollection<string> SupportedProviderKeys { get; } = ["EgoSms", "AfricasTalking"];

        public ISmsProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings) =>
            providerForKey is { } match && string.Equals(match.Key, providerKey, StringComparison.OrdinalIgnoreCase)
                ? match.Provider
                : null;

        public ISmsProvider CreateFallback() => fallback;
    }

    private sealed class StubResultProvider(string deliveryStatus) : ISmsProvider
    {
        public bool WasCalled { get; private set; }

        public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new CommunicationSendResult(deliveryStatus, "provider-msg-1", null));
        }
    }
}
