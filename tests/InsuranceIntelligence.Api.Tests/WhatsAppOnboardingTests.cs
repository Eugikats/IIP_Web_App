using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppOnboardingTests
{
    [Fact]
    public async Task ManagedFlowStaysInWorkspaceAndNeverStoresCodeOrPin()
    {
        await using var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var store = new EfWorkspaceRecordStore(db);
        var audit = new InMemoryAuditRepository();
        var configurations = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        var graph = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000001", "IIP test", null));
        var meta = new FakeLifecycle();
        var runtime = new FakeRuntime();
        var options = new InfrastructureOptions
        {
            AuthMode = "Development",
            ApiPublicBaseUrl = "https://api.example.test",
            WhatsApp = new WhatsAppOptions
            {
                ManagedAccessToken = "test-managed-token",
                ManagedAppSecret = "test-app-secret"
            }
        };
        var owner = Context("Admin", "bank-a");
        var support = Context("PublisherSupport", "platform");
        var other = Context("Admin", "other-bank");
        var request = new SubmitOnboardingRequest("+256700000001", "IIP test", "test@example.test", true);
        Assert.Equal(201, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.Submit(request, store, audit, options, owner, default)).StatusCode);
        Assert.Equal(403, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.AssignNumber("bank-a", "main", new("waba-1", "phone-1"), store, graph, configurations, audit, options, owner, default)).StatusCode);
        Assert.Equal(200, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.AssignNumber("bank-a", "main", new("waba-1", "phone-1"), store, graph, configurations, audit, options, support, default)).StatusCode);
        Assert.Equal(409, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.RequestCode(new("SMS"), store, meta, audit, options, other, default)).StatusCode);
        Assert.Equal(200, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.RequestCode(new("SMS"), store, meta, audit, options, owner, default)).StatusCode);
        Assert.Equal(200, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.VerifyCode(new("123456"), store, meta, audit, options, owner, default)).StatusCode);
        Assert.Equal(200, ((IStatusCodeHttpResult)await WhatsAppOnboardingEndpoints.RegisterNumber(new("654321"), store, meta, graph, configurations, protector, runtime, audit, options, owner, default)).StatusCode);

        var saved = await store.GetAsync(TenantScope.Create("bank-a", "main"), "whatsapp-onboarding", "current");
        Assert.NotNull(saved);
        Assert.Contains("Active", saved.PayloadJson);
        Assert.DoesNotContain("123456", saved.PayloadJson);
        Assert.DoesNotContain("654321", saved.PayloadJson);
        Assert.Equal("123456", meta.Code);
        Assert.Equal("654321", meta.Pin);
        var channel = await configurations.GetAsync(TenantScope.Create("bank-a", "main"));
        Assert.NotNull(channel);
        Assert.True(channel.IsActive);
        Assert.Equal("protected:test-managed-token", channel.EncryptedAccessToken);
        Assert.Equal("Registered", channel.WebhookRegistrationStatus);
        Assert.Equal(1, runtime.Refreshes);
    }

    [Fact]
    public async Task Registered_number_can_finish_webhook_after_a_failure_without_registering_twice()
    {
        await using var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var store = new EfWorkspaceRecordStore(db);
        var audit = new InMemoryAuditRepository();
        var channels = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        var facts = new WhatsAppGraphValidationResult(true, "+256700000001", "IIP test", null);
        var brokenGraph = new FakeWhatsAppGraphValidationClient(facts, new WhatsAppWebhookRegistrationResult(false, "Webhook unavailable"));
        var healthyGraph = new FakeWhatsAppGraphValidationClient(facts);
        var meta = new FakeLifecycle();
        var runtime = new FakeRuntime();
        var options = new InfrastructureOptions { AuthMode = "Development", ApiPublicBaseUrl = "https://api.example.test",
            WhatsApp = new WhatsAppOptions { ManagedAccessToken = "token", ManagedAppSecret = "secret" } };
        var owner = Context("Admin", "bank-a");
        var support = Context("PublisherSupport", "platform");
        await WhatsAppOnboardingEndpoints.Submit(new("+256700000001", "IIP test", "test@example.test", true), store, audit, options, owner, default);
        await WhatsAppOnboardingEndpoints.AssignNumber("bank-a", "main", new("waba-1", "phone-1"), store, healthyGraph, channels, audit, options, support, default);
        await WhatsAppOnboardingEndpoints.RequestCode(new("SMS"), store, meta, audit, options, owner, default);
        await WhatsAppOnboardingEndpoints.VerifyCode(new("123456"), store, meta, audit, options, owner, default);

        var first = await WhatsAppOnboardingEndpoints.RegisterNumber(new("654321"), store, meta, brokenGraph, channels, protector, runtime, audit, options, owner, default);
        Assert.Equal(409, ((IStatusCodeHttpResult)first).StatusCode);
        Assert.False((await channels.GetAsync(TenantScope.Create("bank-a", "main")))!.IsActive);
        var second = await WhatsAppOnboardingEndpoints.RegisterNumber(new(null), store, meta, healthyGraph, channels, protector, runtime, audit, options, owner, default);
        Assert.Equal(200, ((IStatusCodeHttpResult)second).StatusCode);
        Assert.Equal(1, meta.RegisterCalls);
        Assert.True((await channels.GetAsync(TenantScope.Create("bank-a", "main")))!.IsActive);
    }

    private static DefaultHttpContext Context(string role, string tenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = role;
        context.Request.Headers["X-Tenant-Id"] = tenant;
        context.Request.Headers["X-Workspace-Id"] = "main";
        context.Request.Headers["X-User-Id"] = "test-user";
        return context;
    }

    private sealed class FakeLifecycle : IWhatsAppPhoneNumberLifecycleClient
    {
        public string? Code { get; private set; }
        public string? Pin { get; private set; }
        public int RegisterCalls { get; private set; }
        public Task<WhatsAppPhoneNumberResult> RequestCodeAsync(string phoneNumberId, string method, string language, string accessToken, CancellationToken cancellationToken = default) => Task.FromResult(new WhatsAppPhoneNumberResult(true));
        public Task<WhatsAppPhoneNumberResult> VerifyCodeAsync(string phoneNumberId, string code, string accessToken, CancellationToken cancellationToken = default)
        {
            Code = code;
            return Task.FromResult(new WhatsAppPhoneNumberResult(true));
        }
        public Task<WhatsAppPhoneNumberResult> RegisterAsync(string phoneNumberId, string pin, string accessToken, CancellationToken cancellationToken = default)
        {
            Pin = pin;
            RegisterCalls++;
            return Task.FromResult(new WhatsAppPhoneNumberResult(true));
        }
        public Task<WhatsAppPhoneNumberResult> SubscribeAppAsync(string wabaId, string accessToken, CancellationToken cancellationToken = default) => Task.FromResult(new WhatsAppPhoneNumberResult(true));
    }

    private sealed class FakeRuntime : IWhatsAppRuntimeSettingsProvider
    {
        public WhatsAppEffectiveSettings? Current => null;
        public int Refreshes { get; private set; }
        public Task RefreshAsync(CancellationToken cancellationToken = default) { Refreshes++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task PlatformQueueCanReadRequestsAcrossScopes_WhileWorkspaceReadsRemainIsolated()
    {
        await using var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var store = new EfWorkspaceRecordStore(db);
        var bank = TenantScope.Create("bank-a", "main");
        var insurer = TenantScope.Create("insurer-b", "main");
        await store.SaveAsync(bank, "whatsapp-onboarding", "current", "{\"id\":\"bank\"}");
        await store.SaveAsync(insurer, "whatsapp-onboarding", "current", "{\"id\":\"insurer\"}");

        Assert.Null(await store.GetAsync(bank, "whatsapp-onboarding", "insurer"));
        Assert.Equal(2, (await store.ListPlatformAsync("whatsapp-onboarding")).Count);
        Assert.Single(await store.ListAsync(bank, "whatsapp-onboarding"));
    }

    [Fact]
    public async Task OnlyPublisherSupportCanListTheCrossTenantQueue()
    {
        await using var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var store = new EfWorkspaceRecordStore(db);
        await store.SaveAsync(TenantScope.Create("bank-a", "main"), "whatsapp-onboarding", "current", "{}");
        var options = new InfrastructureOptions { AuthMode = "Development" };
        var customer = new DefaultHttpContext();
        customer.Request.Headers["x-demo-role"] = "Client";
        var denied = await WhatsAppOnboardingEndpoints.SupportQueue(store, options, customer, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, ((IStatusCodeHttpResult)denied).StatusCode);

        var support = new DefaultHttpContext();
        support.Request.Headers["x-demo-role"] = "PublisherSupport";
        var allowed = await WhatsAppOnboardingEndpoints.SupportQueue(store, options, support, CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)allowed).StatusCode);
    }
}
