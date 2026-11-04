using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.ClientPortal;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Several insurers on one deployment (build-loop item 29): which workspace a customer's link or web address belongs to, that nothing a
/// browser sends can pick a workspace outside Development, and that a name two workspaces hold belongs to neither.
/// </summary>
public sealed class PortalDirectoryTests
{
    private static readonly TenantScope ScopeA = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly TenantScope ScopeB = TenantScope.Create("tenant-b", "workspace-b");

    private static PortalIdentity Identity(TenantScope scope, string slug, string? hostname = null, bool verified = false, string? smsHash = null) =>
        new()
        {
            Scope = scope,
            Slug = slug,
            DisplayName = slug.ToUpperInvariant(),
            Hostname = hostname,
            HostnameVerifiedAt = verified ? DateTimeOffset.UtcNow : null,
            HostnameChallenge = hostname is not null && !verified ? "iip-challenge" : null,
            SmsWebhookSecretHash = smsHash,
            UpdatedByUserId = "admin",
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static async Task<(PortalDirectory Directory, InMemoryPortalIdentityRepository Repository)> BuildAsync(TimeSpan? missInterval = null, params PortalIdentity[] identities)
    {
        var repository = new InMemoryPortalIdentityRepository();
        foreach (var identity in identities)
        {
            await repository.SaveAsync(identity);
        }

        var services = new ServiceCollection().AddSingleton<IPortalIdentityRepository>(repository).BuildServiceProvider();
        return (new PortalDirectory(services.GetRequiredService<IServiceScopeFactory>(), missInterval), repository);
    }

    private static DefaultHttpContext Request(
        PortalDirectory? directory, string authMode = "Development", string? slug = null, string? origin = null, string? tenant = null, string? workspace = null,
        string? pinnedTenant = null, string? pinnedWorkspace = null)
    {
        var services = new ServiceCollection()
            .AddSingleton(new InfrastructureOptions { AuthMode = authMode, TenantId = pinnedTenant, WorkspaceId = pinnedWorkspace });
        if (directory is not null)
        {
            services.AddSingleton<IPortalDirectory>(directory);
        }

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (slug is not null) context.Request.Headers[ClientPortalScopeReader.SlugHeader] = slug;
        if (origin is not null) context.Request.Headers["Origin"] = origin;
        if (tenant is not null) context.Request.Headers["X-Tenant-Id"] = tenant;
        if (workspace is not null) context.Request.Headers["X-Workspace-Id"] = workspace;
        return context;
    }

    // ---- the directory ----

    [Fact]
    public async Task AShortNameFindsItsOwnWorkspaceOnly()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"), Identity(ScopeB, "zuri"));

        Assert.Equal(ScopeA, directory.BySlug("acme")!.Scope);
        Assert.Equal(ScopeB, directory.BySlug("zuri")!.Scope);
        Assert.Null(directory.BySlug("unknown"));
        Assert.Null(directory.BySlug(""));
        Assert.Equal(ScopeB, directory.ForScope(ScopeB)!.Scope);
        Assert.Null(directory.ForScope(TenantScope.Create("tenant-c", "workspace-c")));
    }

    [Fact]
    public async Task AWebAddressRoutesOnlyOnceItIsVerified_AndOnlyAsAnHttpsOriginOnTheDefaultPort()
    {
        var (directory, _) = await BuildAsync(null,
            Identity(ScopeA, "acme", "claims.acme.com", verified: true),
            Identity(ScopeB, "zuri", "claims.zuri.com", verified: false));

        Assert.Equal(ScopeA, directory.ByHostname("claims.acme.com")!.Scope);
        Assert.Null(directory.ByHostname("claims.zuri.com"));
        Assert.True(directory.IsPortalOrigin("https://claims.acme.com"));
        Assert.False(directory.IsPortalOrigin("http://claims.acme.com"));
        Assert.False(directory.IsPortalOrigin("https://claims.acme.com:8443"));
        Assert.False(directory.IsPortalOrigin("https://claims.zuri.com"));
        Assert.False(directory.IsPortalOrigin("https://evil.example.com"));
        Assert.False(directory.IsPortalOrigin("not an origin"));
    }

    [Fact]
    public async Task ANameTwoWorkspacesHoldBelongsToNeither()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"), Identity(ScopeB, "acme"));

        Assert.Null(directory.BySlug("acme"));
    }

    [Fact]
    public async Task ASmsSecretFindsItsOwnWorkspace()
    {
        var (directory, _) = await BuildAsync(null,
            Identity(ScopeA, "acme", smsHash: PortalIdentity.HashSecret("secret-a")),
            Identity(ScopeB, "zuri", smsHash: PortalIdentity.HashSecret("secret-b")));

        Assert.Equal(ScopeA, directory.BySmsSecretHash(PortalIdentity.HashSecret("secret-a"))!.Scope);
        Assert.Equal(ScopeB, directory.BySmsSecretHash(PortalIdentity.HashSecret("secret-b"))!.Scope);
        Assert.Null(directory.BySmsSecretHash(PortalIdentity.HashSecret("secret-c")));
        Assert.Null(directory.BySmsSecretHash(string.Empty));
    }

    [Fact]
    public async Task ANewShortNameIsFoundOnAMiss_ButAStrangerCannotMakeTheStoreBeReadOverAndOver()
    {
        var (directory, repository) = await BuildAsync(TimeSpan.Zero, Identity(ScopeA, "acme"));
        _ = directory.BySlug("acme");
        await repository.SaveAsync(Identity(ScopeB, "newcomer"));

        Assert.Equal(ScopeB, (await directory.FindBySlugAsync("newcomer"))!.Scope);

        var (slow, counting) = await BuildAsync(TimeSpan.FromHours(1), Identity(ScopeA, "acme"));
        _ = slow.BySlug("acme");
        await counting.SaveAsync(Identity(ScopeB, "hidden"));
        for (var i = 0; i < 20; i++)
        {
            Assert.Null(await slow.FindBySlugAsync($"random-{i}"));
        }

        Assert.Null(slow.BySlug("hidden")); // not re-read yet: strangers do not trigger reads
    }

    // ---- which workspace a request is for ----

    [Fact]
    public async Task ALinksShortNameNamesTheWorkspace_EvenWithNoHeadersOutsideDevelopment()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"), Identity(ScopeB, "zuri"));

        Assert.True(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", slug: "zuri"), out var scope, out _));
        Assert.Equal(ScopeB, scope);
    }

    [Fact]
    public async Task AShortNameNobodyHasIsRefused_NotFallenBackToAGuess()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"));

        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", slug: "unknown"), out _, out var error));
        Assert.Contains("not valid", error);
        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Development", slug: "unknown", tenant: "tenant-a", workspace: "workspace-a"), out _, out _));
    }

    [Fact]
    public async Task AVerifiedWebAddressNamesTheWorkspace_AnUnverifiedOneDoesNot()
    {
        var (directory, _) = await BuildAsync(null,
            Identity(ScopeA, "acme", "claims.acme.com", verified: true),
            Identity(ScopeB, "zuri", "claims.zuri.com", verified: false));

        Assert.True(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", origin: "https://claims.acme.com"), out var scope, out _));
        Assert.Equal(ScopeA, scope);
        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", origin: "https://claims.zuri.com"), out _, out _));
    }

    [Fact]
    public async Task ABrowserCannotChooseAWorkspaceByHeaderOnASharedDeploymentOutsideDevelopment()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"));

        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", tenant: "tenant-a", workspace: "workspace-a"), out _, out var error));
        Assert.Contains("does not say which insurer", error);
    }

    [Fact]
    public async Task AHeaderThatDisagreesWithTheLinkIsRefusedOutsideDevelopment()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"), Identity(ScopeB, "zuri"));

        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", slug: "acme", tenant: "tenant-b", workspace: "workspace-b"), out _, out var error));
        Assert.Contains("different insurer", error);
        Assert.True(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Entra", slug: "acme", tenant: "tenant-a", workspace: "workspace-a"), out var scope, out _));
        Assert.Equal(ScopeA, scope);
    }

    [Fact]
    public async Task ADeploymentDedicatedToOneInsurerStillRefusesAnotherInsurersLink()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"), Identity(ScopeB, "zuri"));

        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(
            Request(directory, "Entra", slug: "zuri", pinnedTenant: "tenant-a", pinnedWorkspace: "workspace-a"), out _, out var error));
        Assert.Contains("different insurer", error);
        Assert.True(ClientPortalScopeReader.TryGetFromHeaders(
            Request(directory, "Entra", pinnedTenant: "tenant-a", pinnedWorkspace: "workspace-a"), out var scope, out _));
        Assert.Equal(ScopeA, scope);
    }

    [Fact]
    public async Task DevelopmentStillTakesTheHeadersAsSent()
    {
        var (directory, _) = await BuildAsync(null);

        Assert.True(ClientPortalScopeReader.TryGetFromHeaders(Request(directory, "Development", tenant: "any-tenant", workspace: "any-workspace"), out var scope, out _));
        Assert.Equal(TenantScope.Create("any-tenant", "any-workspace"), scope);
    }

    // ---- the addresses customers are sent to ----

    [Fact]
    public async Task ALinkCarriesTheShortNameOnTheSharedPortal_AndNothingOnTheWorkspacesOwnAddress()
    {
        var (directory, _) = await BuildAsync(null,
            Identity(ScopeA, "acme"),
            Identity(ScopeB, "zuri", "claims.zuri.com", verified: true));
        var address = new PortalAddress(new InfrastructureOptions { AuthMode = "Entra", ClientPortalBaseUrl = "https://claims.platform.example/" }, directory);

        Assert.Equal("https://claims.platform.example", address.BaseUrlFor(ScopeA));
        Assert.Equal("&t=acme", address.LinkQueryFor(ScopeA));
        Assert.Equal("https://claims.zuri.com", address.BaseUrlFor(ScopeB));
        Assert.Equal(string.Empty, address.LinkQueryFor(ScopeB));
        Assert.Equal(string.Empty, address.LinkQueryFor(TenantScope.Create("tenant-c", "workspace-c")));
    }

    [Fact]
    public async Task WithNoAddressConfiguredOutsideDevelopmentNoLinkIsMade()
    {
        var (directory, _) = await BuildAsync(null, Identity(ScopeA, "acme"));

        Assert.Null(new PortalAddress(new InfrastructureOptions { AuthMode = "Entra" }, directory).BaseUrlFor(ScopeA));
        Assert.Equal("http://localhost:3000", new PortalAddress(new InfrastructureOptions { AuthMode = "Development" }, directory).BaseUrlFor(ScopeA));
    }
}
