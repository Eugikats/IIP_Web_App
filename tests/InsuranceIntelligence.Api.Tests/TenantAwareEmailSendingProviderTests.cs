using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Communications;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Mirrors TenantAwareSmsProviderTests: the resolution-order coverage already exists
/// implicitly through TenantAwareSmsProvider's own tests (identical structure), so this file
/// focuses on what outcome M actually adds here, consent enforcement before a non-low-risk email.</summary>
public sealed class TenantAwareEmailSendingProviderTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task SendEmailAsync_NonLowRiskWithoutConsent_ReturnsConsentRequiredWithoutReachingAnyProvider()
    {
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareEmailSendingProvider(
            new StubConnectionRepository(connection: null),
            new StubProtector(),
            new StubFactory(fallback),
            NullLogger<TenantAwareEmailSendingProvider>.Instance,
            new FakeConsentRepository(granted: false));

        var result = await provider.SendEmailAsync(Request());

        Assert.Equal("ConsentRequired", result.DeliveryStatus);
        Assert.False(fallback.WasCalled);
    }

    [Fact]
    public async Task SendEmailAsync_NonLowRiskWithRecordedConsent_Proceeds()
    {
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareEmailSendingProvider(
            new StubConnectionRepository(connection: null),
            new StubProtector(),
            new StubFactory(fallback),
            NullLogger<TenantAwareEmailSendingProvider>.Instance,
            new FakeConsentRepository(granted: true));

        var result = await provider.SendEmailAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(fallback.WasCalled);
    }

    [Fact]
    public async Task SendEmailAsync_LowRiskWithoutConsent_ProceedsAnyway()
    {
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareEmailSendingProvider(
            new StubConnectionRepository(connection: null),
            new StubProtector(),
            new StubFactory(fallback),
            NullLogger<TenantAwareEmailSendingProvider>.Instance,
            new FakeConsentRepository(granted: false));

        var result = await provider.SendEmailAsync(Request(lowRisk: true));

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(fallback.WasCalled);
    }

    [Fact]
    public async Task SendEmailAsync_NoConsentRepositoryInjected_SkipsEnforcementAndProceeds()
    {
        var fallback = new StubResultProvider("Sent");
        var provider = new TenantAwareEmailSendingProvider(
            new StubConnectionRepository(connection: null),
            new StubProtector(),
            new StubFactory(fallback),
            NullLogger<TenantAwareEmailSendingProvider>.Instance);

        var result = await provider.SendEmailAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
    }

    private static CommunicationSendRequest Request(bool lowRisk = false) =>
        new(Scope, Guid.NewGuid(), CommunicationChannel.Email, "customer@example.test", "Test message", "officer-1", Approved: true, LowRisk: lowRisk, "corr-1");

    private sealed class FakeConsentRepository(bool granted) : IConsentRepository
    {
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(granted
                ? new ConsentRecord
                {
                    RecordId = Guid.NewGuid(), Scope = scope, CustomerIdentifier = customerIdentifier,
                    Basis = ConsentBasis.ExplicitOptIn, Purpose = purpose, Channel = "Email",
                    RecordedByUserId = "officer-1", RecordedAt = DateTimeOffset.UtcNow, CorrelationId = "corr-consent-seed",
                }
                : null);

        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ConsentRecord>>([]);
    }

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

    private sealed class StubFactory(IEmailSendingProvider fallback) : IEmailSendingProviderFactory
    {
        public IReadOnlyCollection<string> SupportedProviderKeys { get; } = ["Smtp"];

        public IEmailSendingProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings) => null;

        public IEmailSendingProvider CreateFallback() => fallback;
    }

    private sealed class StubResultProvider(string deliveryStatus) : IEmailSendingProvider
    {
        public bool WasCalled { get; private set; }

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new CommunicationSendResult(deliveryStatus, "provider-msg-1", null));
        }
    }
}
