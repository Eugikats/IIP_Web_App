using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppOnboardingTests
{
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
