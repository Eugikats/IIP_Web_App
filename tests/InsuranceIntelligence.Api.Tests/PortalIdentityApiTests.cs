using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.ClientPortal;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 29 over HTTP: a workspace's customer portal identity, the branding customers see, a verified web address with
/// its own CORS origin, and an SMS delivery callback that reaches only its own workspace.</summary>
public sealed class PortalIdentityApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static string Slug(string prefix = "co") => $"{prefix}-{Guid.NewGuid():N}"[..14];

    private static object Identity(string slug, string? hostname = null) => new
    {
        slug,
        displayName = "Acme Insurance",
        accentColour = "#1F5EFF",
        logoUrl = "https://cdn.example.test/acme.png",
        supportPhone = "+256 700 000 111",
        supportEmail = "help@acme.test",
        hostname,
    };

    private WebApplicationFactory<Program> WithVerifier(FakeVerifier verifier) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("INSURANCEINTELLIGENCE_API_PUBLIC_BASE_URL", "https://api.example.test");
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IHostnameVerifier>(verifier)));
        });

    private static async Task<HttpResponseMessage> SaveAsync(HttpClient admin, object identity) =>
        await admin.PutAsJsonAsync("/api/v1/admin/portal-identity", identity);

    [Fact]
    public async Task AnAdministratorSavesTheIdentity_AndCustomersSeeOnlyTheFacesOfIt()
    {
        var tenant = MockedApiFactory.NewTenant();
        var slug = Slug();
        using var admin = factory.CreateScopedClient(tenant);

        var saved = await SaveAsync(admin, Identity(slug));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var identity = await MockedApiFactory.ReadJsonAsync(saved);
        Assert.Equal(slug, identity.GetProperty("slug").GetString());
        Assert.Contains($"&t={slug}", identity.GetProperty("exampleLink").GetString());
        Assert.False(identity.GetProperty("smsWebhookConfigured").GetBoolean());

        // A customer's page knows nothing but the link: no staff headers, only the short name.
        using var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Add("X-Portal-Slug", slug);
        var branding = await MockedApiFactory.ReadJsonAsync(await customer.GetAsync("/api/v1/client-portal/branding"));
        Assert.Equal("Acme Insurance", branding.GetProperty("displayName").GetString());
        Assert.Equal("#1F5EFF", branding.GetProperty("accentColour").GetString());
        Assert.Equal("help@acme.test", branding.GetProperty("supportEmail").GetString());
        var names = branding.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(["accentColour", "displayName", "logoUrl", "slug", "supportEmail", "supportPhone"], names);
    }

    [Fact]
    public async Task AnUnknownShortNameGetsNoBranding_AndAnOfficerCannotChangeTheIdentity()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Add("X-Portal-Slug", "nobody-here");
        using var officer = factory.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");

        var unknown = await customer.GetAsync("/api/v1/client-portal/branding");
        var forbiddenWrite = await SaveAsync(officer, Identity(Slug()));
        var forbiddenRead = await officer.GetAsync("/api/v1/admin/portal-identity");

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
    }

    [Theory]
    [InlineData("slug", "admin", "reserved")]
    [InlineData("slug", "A", "short name")]
    [InlineData("accentColour", "#FFCC00", "too light")]
    [InlineData("accentColour", "blue", "six-digit")]
    [InlineData("logoUrl", "http://cdn.example.test/a.png", "https")]
    [InlineData("displayName", " ", "name")]
    [InlineData("supportEmail", "not-an-email", "email")]
    [InlineData("hostname", "https://claims.acme.test", "plain host name")]
    public async Task AnInvalidDetailIsRefusedWithAReason(string field, string value, string expected)
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        var body = new Dictionary<string, object?>
        {
            ["slug"] = Slug(), ["displayName"] = "Acme", ["accentColour"] = "#1F5EFF", ["logoUrl"] = null, ["supportPhone"] = null, ["supportEmail"] = null, ["hostname"] = null,
        };
        body[field] = value;

        var response = await admin.PutAsJsonAsync("/api/v1/admin/portal-identity", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AShortNameAndAWebAddressBelongToOneWorkspace()
    {
        var slug = Slug();
        var host = $"claims.{slug}.test";
        using var first = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var second = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(first, Identity(slug, host))).StatusCode);

        var sameName = await SaveAsync(second, Identity(slug));
        var sameAddress = await SaveAsync(second, Identity(Slug(), host));
        var again = await SaveAsync(first, Identity(slug, host));

        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, sameAddress.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task AWebAddressMustBeProvedBeforeCustomersAreSentThere_ThenItIsAllowedByCorsAndNamesTheWorkspace()
    {
        var verifier = new FakeVerifier();
        using var host = WithVerifier(verifier);
        var tenant = MockedApiFactory.NewTenant();
        var slug = Slug();
        var hostname = $"claims.{slug}.test";
        using var admin = host.CreateScopedClient(tenant);
        var saved = await MockedApiFactory.ReadJsonAsync(await SaveAsync(admin, Identity(slug, hostname)));
        Assert.False(saved.GetProperty("hostnameVerified").GetBoolean());
        var challenge = saved.GetProperty("hostnameChallenge").GetString()!;
        Assert.StartsWith("iip-", challenge);

        // Before proof, the address routes nowhere and CORS does not allow it.
        using var browser = host.CreateClient();
        using var before = new HttpRequestMessage(HttpMethod.Options, "/api/v1/client-portal/branding");
        before.Headers.Add("Origin", $"https://{hostname}");
        before.Headers.Add("Access-Control-Request-Method", "GET");
        Assert.False((await browser.SendAsync(before)).Headers.Contains("Access-Control-Allow-Origin"));
        using var branded = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client-portal/branding");
        branded.Headers.Add("Origin", $"https://{hostname}");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(branded)).StatusCode);

        // A failed proof says why and changes nothing.
        verifier.Result = new HostnameCheck(false, "The address published different text from the verification text.");
        var failed = await admin.PostAsync("/api/v1/admin/portal-identity/verify-hostname", null);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Contains("different text", await failed.Content.ReadAsStringAsync());

        verifier.Result = new HostnameCheck(true, null);
        var verified = await MockedApiFactory.ReadJsonAsync(await admin.PostAsync("/api/v1/admin/portal-identity/verify-hostname", null));
        Assert.True(verified.GetProperty("hostnameVerified").GetBoolean());
        Assert.Equal(hostname, verifier.LastHostname);
        Assert.Equal(challenge, verifier.LastChallenge);

        using var after = new HttpRequestMessage(HttpMethod.Options, "/api/v1/client-portal/branding");
        after.Headers.Add("Origin", $"https://{hostname}");
        after.Headers.Add("Access-Control-Request-Method", "GET");
        Assert.Equal($"https://{hostname}", (await browser.SendAsync(after)).Headers.GetValues("Access-Control-Allow-Origin").Single());
        using var named = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client-portal/branding");
        named.Headers.Add("Origin", $"https://{hostname}");
        var branding = await MockedApiFactory.ReadJsonAsync(await browser.SendAsync(named));
        Assert.Equal(slug, branding.GetProperty("slug").GetString());

        // Another origin is still not allowed.
        using var stranger = new HttpRequestMessage(HttpMethod.Options, "/api/v1/client-portal/branding");
        stranger.Headers.Add("Origin", "https://evil.example.test");
        stranger.Headers.Add("Access-Control-Request-Method", "GET");
        Assert.False((await browser.SendAsync(stranger)).Headers.Contains("Access-Control-Allow-Origin"));

        // Changing the address asks for proof again.
        var changed = await MockedApiFactory.ReadJsonAsync(await SaveAsync(admin, Identity(slug, $"other.{slug}.test")));
        Assert.False(changed.GetProperty("hostnameVerified").GetBoolean());
        Assert.NotEqual(challenge, changed.GetProperty("hostnameChallenge").GetString());
    }

    [Fact]
    public async Task VerifyingBeforeAnAddressIsSavedIsRefused()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        await SaveAsync(admin, Identity(Slug()));

        var response = await admin.PostAsync("/api/v1/admin/portal-identity/verify-hostname", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EachWorkspaceHasItsOwnSmsCallback_AndAReportReachesOnlyTheWorkspaceThatOwnsTheAddress()
    {
        using var host = WithVerifier(new FakeVerifier());
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var adminA = host.CreateScopedClient(tenantA);
        using var adminB = host.CreateScopedClient(tenantB);
        await SaveAsync(adminA, Identity(Slug("sa")));
        await SaveAsync(adminB, Identity(Slug("sb")));
        var secretA = await RotateAsync(adminA);
        var secretB = await RotateAsync(adminB);
        Assert.NotEqual(secretA, secretB);

        var scopeA = TenantScope.Create(tenantA, "workspace-main");
        using (var services = host.Services.CreateScope())
        {
            await services.ServiceProvider.GetRequiredService<ICommunicationRepository>().SaveAsync(new Communication
            {
                CommunicationId = Guid.NewGuid(),
                Scope = scopeA,
                CaseId = Guid.NewGuid(),
                Channel = CommunicationChannel.SMS,
                Direction = "Outbound",
                MessageBody = "Hello",
                SentBy = "test",
                DeliveryStatus = "Sent",
                CreatedAt = DateTimeOffset.UtcNow,
                CorrelationId = "corr",
                ProviderMessageId = "msg-of-a",
            });
        }

        using var gateway = host.CreateClient();
        var report = new { MsgFollowUpUniqueCode = "msg-of-a", Number = "256700000000", Status = "Success" };

        var crossed = await MockedApiFactory.ReadJsonAsync(await gateway.PostAsJsonAsync($"/api/v1/integrations/sms/status/{secretB}", report));
        var own = await MockedApiFactory.ReadJsonAsync(await gateway.PostAsJsonAsync($"/api/v1/integrations/sms/status/{secretA}", report));
        var wrong = await gateway.PostAsJsonAsync("/api/v1/integrations/sms/status/not-a-secret", report);

        Assert.Equal("Unmatched", crossed.GetProperty("status").GetString()); // B's address cannot touch A's messages
        Assert.Equal("Updated", own.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);

        // A new address stops the old one working at once.
        var newSecretA = await RotateAsync(adminA);
        Assert.Equal(HttpStatusCode.NotFound, (await gateway.PostAsJsonAsync($"/api/v1/integrations/sms/status/{secretA}", report)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await gateway.PostAsJsonAsync($"/api/v1/integrations/sms/status/{newSecretA}", report)).StatusCode);
    }

    [Fact]
    public async Task TheCallbackAddressIsShownOnce_AndTheSavedIdentityNeverCarriesIt()
    {
        using var host = WithVerifier(new FakeVerifier());
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());
        await SaveAsync(admin, Identity(Slug()));

        var secret = await RotateAsync(admin);

        var identity = await (await admin.GetAsync("/api/v1/admin/portal-identity")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, identity);
        Assert.Contains("\"smsWebhookConfigured\":true", identity);
        Assert.DoesNotContain("smsWebhookSecretHash", identity, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACallbackCannotBeMadeBeforeTheIdentityIsSaved()
    {
        using var host = WithVerifier(new FakeVerifier());
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await admin.PostAsync("/api/v1/admin/portal-identity/sms-webhook", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> RotateAsync(HttpClient admin)
    {
        var response = await admin.PostAsync("/api/v1/admin/portal-identity/sms-webhook", null);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var url = (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("callbackUrl").GetString()!;
        Assert.StartsWith("https://api.example.test/api/v1/integrations/sms/status/", url);
        return url[(url.LastIndexOf('/') + 1)..];
    }

    private sealed class FakeVerifier : IHostnameVerifier
    {
        public HostnameCheck Result { get; set; } = new(false, "not tried");
        public string? LastHostname { get; private set; }
        public string? LastChallenge { get; private set; }

        public Task<HostnameCheck> VerifyAsync(string hostname, string expectedChallenge, CancellationToken cancellationToken = default)
        {
            LastHostname = hostname;
            LastChallenge = expectedChallenge;
            return Task.FromResult(Result);
        }
    }
}
