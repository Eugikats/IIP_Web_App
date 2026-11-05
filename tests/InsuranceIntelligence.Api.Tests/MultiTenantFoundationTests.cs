using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Functions;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>SaaS mode never falls back to a configured tenant: a request, receipt or job that cannot
/// say whose it is gets refused, and scheduled work visits every workspace on its own.</summary>
public sealed class MultiTenantFoundationTests
{
    private static readonly TenantScope Acme = TenantScope.Create("tenant-acme", "workspace-main");
    private static readonly TenantScope Baraka = TenantScope.Create("tenant-baraka", "workspace-main");

    [Fact]
    public void StartingWithADeploymentWideTenantOrWhatsAppConnection_IsRefused()
    {
        var clean = new InfrastructureOptions
        {
            MultiTenant = true,
            AuthMode = "Entra",
            DataProtectionBlobUri = "https://keys.blob.core.windows.net/dp/keyring.xml",
            DataProtectionKeyUri = "https://vault.vault.azure.net/keys/dp",
        };
        Assert.Empty(MultiTenantStartupGuard.Problems(clean));

        Assert.Single(MultiTenantStartupGuard.Problems(clean with { TenantId = "tenant-demo" }));
        Assert.Single(MultiTenantStartupGuard.Problems(clean with { WorkspaceId = "workspace-operations" }));
        Assert.Single(MultiTenantStartupGuard.Problems(clean with { WhatsApp = new WhatsAppOptions { PhoneNumberId = "123", AccessToken = "t" } }));
        Assert.Single(MultiTenantStartupGuard.Problems(clean with { AuthMode = "Development" }));
        // Stored credentials must not live in a key file on the web server.
        Assert.Single(MultiTenantStartupGuard.Problems(clean with { DataProtectionKeyUri = null }));
        Assert.Empty(MultiTenantStartupGuard.Problems(new InfrastructureOptions
        {
            MultiTenant = true, AuthMode = "Development", RuntimeProfile = "LocalTest",
        }));

        // The Managed Application edition is untouched by any of this.
        Assert.Empty(MultiTenantStartupGuard.Problems(new InfrastructureOptions { TenantId = "tenant-demo", AuthMode = "Development" }));
    }

    [Fact]
    public void RateLimitsAreKeyedPerPersonOrPerRealAddress_NotPerProxy()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Request.Headers["X-Forwarded-For"] = "203.0.113.7:51234, 10.0.0.4";
        var sameAddressDifferentPort = new DefaultHttpContext();
        sameAddressDifferentPort.Request.Headers["X-Forwarded-For"] = "203.0.113.7:60000";
        var other = new DefaultHttpContext();
        other.Request.Headers["X-Forwarded-For"] = "198.51.100.9";
        var noHeader = new DefaultHttpContext();

        Assert.Equal("ip:203.0.113.7", RateLimitKeys.For(anonymous));
        Assert.Equal(RateLimitKeys.For(anonymous), RateLimitKeys.For(sameAddressDifferentPort));
        Assert.Equal("ip:198.51.100.9", RateLimitKeys.For(other));
        Assert.Equal("ip:unknown", RateLimitKeys.For(noHeader));
    }

    [Fact]
    public void ARequestWithNoOrganisation_IsNotSentToAConfiguredDefault()
    {
        var multi = new InfrastructureOptions { MultiTenant = true, TenantId = "tenant-leftover", WorkspaceId = "workspace-leftover" };

        Assert.False(TenantScopeReader.TryGetFromConfiguredScope(multi, out _, out var error));
        Assert.Contains("organisation", error);

        var single = new InfrastructureOptions { TenantId = "tenant-demo", WorkspaceId = "workspace-operations" };
        Assert.True(TenantScopeReader.TryGetFromConfiguredScope(single, out var scope, out _));
        Assert.Equal("tenant-demo", scope.TenantId);
    }

    [Fact]
    public async Task ScheduledJobs_VisitEveryWorkspace_AndOneFailureDoesNotStopTheOthers()
    {
        var directory = new InMemoryWorkspaceDirectory(Acme, Baraka);
        var workspaces = await ScheduledWorkspaces.ResolveAsync(
            new InfrastructureOptions { MultiTenant = true }, directory, NullLogger.Instance, "test", CancellationToken.None);
        var visited = new List<string>();

        await ScheduledWorkspaces.ForEachAsync(workspaces, NullLogger.Instance, "test", scope =>
        {
            visited.Add(scope.TenantId);
            return scope == Acme ? throw new InvalidOperationException("Acme's job broke") : Task.CompletedTask;
        });

        Assert.Equal(["tenant-acme", "tenant-baraka"], visited);
    }

    [Fact]
    public async Task ScheduledJobs_InAManagedApplication_RunForTheOneConfiguredWorkspace()
    {
        var directory = new InMemoryWorkspaceDirectory(Acme, Baraka);

        var configured = await ScheduledWorkspaces.ResolveAsync(
            new InfrastructureOptions { TenantId = "tenant-demo", WorkspaceId = "workspace-operations" }, directory, NullLogger.Instance, "test", CancellationToken.None);
        var unconfigured = await ScheduledWorkspaces.ResolveAsync(
            new InfrastructureOptions(), directory, NullLogger.Instance, "test", CancellationToken.None);

        Assert.Equal(TenantScope.Create("tenant-demo", "workspace-operations"), Assert.Single(configured));
        Assert.Empty(unconfigured);
    }

    [Fact]
    public async Task ASmsDeliveryReceipt_FindsTheInsurerThatSentIt_AndAnAmbiguousOneUpdatesNothing()
    {
        var repository = new InMemoryCommunicationRepository();
        await repository.SaveAsync(Sms(Acme, "gateway-1"), CancellationToken.None);
        await repository.SaveAsync(Sms(Baraka, "gateway-2"), CancellationToken.None);
        await repository.SaveAsync(Sms(Acme, "gateway-shared"), CancellationToken.None);
        await repository.SaveAsync(Sms(Baraka, "gateway-shared"), CancellationToken.None);
        var options = new InfrastructureOptions { MultiTenant = true, SmsWebhookSharedSecret = "path-secret" };
        var barakaPortal = new PortalDirectoryStub(Baraka, "path-secret");

        var unique = await SmsIntegrationEndpoints.ReceiveDeliveryStatus(
            "path-secret", new SmsDeliveryStatusPayload("gateway-2", "256700000001", "Delivered"), repository, options,
            NullLogger<SmsDeliveryStatusLogMarker>.Instance, CancellationToken.None, barakaPortal);
        var ambiguous = await SmsIntegrationEndpoints.ReceiveDeliveryStatus(
            "path-secret", new SmsDeliveryStatusPayload("gateway-shared", "256700000001", "Delivered"), repository, options,
            NullLogger<SmsDeliveryStatusLogMarker>.Instance, CancellationToken.None, new PortalDirectoryStub(null, null));
        var wrongSecret = await SmsIntegrationEndpoints.ReceiveDeliveryStatus(
            "guess", new SmsDeliveryStatusPayload("gateway-1", "256700000001", "Delivered"), repository, options,
            NullLogger<SmsDeliveryStatusLogMarker>.Instance, CancellationToken.None);

        Assert.Equal("Delivered", (await repository.FindByProviderMessageIdAsync(Baraka, "gateway-2"))!.DeliveryStatus);
        Assert.Equal("Sent", (await repository.FindByProviderMessageIdAsync(Acme, "gateway-1"))!.DeliveryStatus);
        Assert.Equal("Sent", (await repository.FindByProviderMessageIdAsync(Acme, "gateway-shared"))!.DeliveryStatus);
        Assert.Equal("Sent", (await repository.FindByProviderMessageIdAsync(Baraka, "gateway-shared"))!.DeliveryStatus);
        Assert.IsAssignableFrom<IResult>(unique);
        Assert.IsAssignableFrom<IResult>(ambiguous);
        Assert.IsAssignableFrom<IResult>(wrongSecret);
    }

    private sealed class PortalDirectoryStub(TenantScope? scope, string? secret) : InsuranceIntelligence.Infrastructure.ClientPortal.IPortalDirectory
    {
        private readonly InsuranceIntelligence.Domain.ClientPortal.PortalIdentity? identity = scope is null || secret is null
            ? null
            : new InsuranceIntelligence.Domain.ClientPortal.PortalIdentity
            {
                Scope = scope,
                Slug = "baraka",
                DisplayName = "Baraka Insurance",
                SmsWebhookSecretHash = InsuranceIntelligence.Domain.ClientPortal.PortalIdentity.HashSecret(secret),
                UpdatedByUserId = "test",
                UpdatedAt = DateTimeOffset.UnixEpoch,
            };

        public InsuranceIntelligence.Domain.ClientPortal.PortalIdentity? BySlug(string slug) => null;
        public InsuranceIntelligence.Domain.ClientPortal.PortalIdentity? ByHostname(string hostname) => null;
        public InsuranceIntelligence.Domain.ClientPortal.PortalIdentity? ForScope(TenantScope requested) => identity?.Scope.Matches(requested) == true ? identity : null;
        public InsuranceIntelligence.Domain.ClientPortal.PortalIdentity? BySmsSecretHash(string secretHash) => identity?.SmsWebhookSecretHash == secretHash ? identity : null;
        public bool IsPortalOrigin(string origin) => false;
        public Task RefreshIfStaleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity?>(null);
        public Task<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity?> FindByHostnameAsync(string hostname, CancellationToken cancellationToken = default) => Task.FromResult<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity?>(null);
    }

    private static Communication Sms(TenantScope scope, string providerMessageId) => new()
    {
        CommunicationId = Guid.NewGuid(),
        Scope = scope,
        CaseId = Guid.NewGuid(),
        Channel = CommunicationChannel.SMS,
        Direction = "Outbound",
        MessageBody = "Hello",
        SentBy = "system",
        DeliveryStatus = "Sent",
        CreatedAt = DateTimeOffset.UtcNow,
        CorrelationId = "corr",
        ProviderMessageId = providerMessageId,
    };
}
