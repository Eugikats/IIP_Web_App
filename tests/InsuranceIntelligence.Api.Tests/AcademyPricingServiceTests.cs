using InsuranceIntelligence.Application.Academy;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Academy;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AcademyPricingServiceTests
{
    private static (AcademyPricingService Service, Repo Repo, Audit Audit) Build(bool allowed = true)
    {
        var repo = new Repo();
        var audit = new Audit();
        return (new AcademyPricingService(new Ctx(), new Perms(allowed), repo, audit), repo, audit);
    }

    [Fact]
    public async Task A_workspace_that_has_set_nothing_prices_every_course_at_the_seed_price()
    {
        var (service, _, _) = Build();

        var pricing = await service.GetAsync();

        Assert.Equal(AcademyPricing.CourseIds.Count, pricing.Prices.Count);
        Assert.All(pricing.Prices, price => Assert.Equal(120m, price.Amount));
        Assert.Equal(120m, pricing.Prices.Single(price => price.CourseId == "ai-agentic-insurance").Amount);
        Assert.Equal(120m, pricing.Prices.Single(price => price.CourseId == "ai-sales-distribution").Amount);
        Assert.All(pricing.Prices, price => Assert.Equal("USD", price.Currency));
    }

    [Fact]
    public async Task Every_course_the_academy_offers_has_a_price_including_the_five_programme_courses()
    {
        var (service, _, _) = Build();

        var ids = (await service.GetAsync()).Prices.Select(price => price.CourseId).ToArray();

        foreach (var id in new[] { "ai-agentic-insurance", "ai-claims-fraud", "ai-underwriting-risk", "ai-sales-distribution", "agentic-systems", "claims", "underwriting", "fraud-identity", "governance", "claims-communication" })
        {
            Assert.Contains(id, ids);
        }
    }

    [Fact]
    public async Task Changing_one_price_leaves_the_others_at_the_seed_price_and_is_remembered_and_audited()
    {
        var (service, repo, audit) = Build();

        await service.SaveAsync([new CoursePrice("claims", 175m)]);
        var again = await service.GetAsync();

        Assert.Equal(175m, again.Prices.Single(price => price.CourseId == "claims").Amount);
        Assert.Equal(120m, again.Prices.Single(price => price.CourseId == "governance").Amount);
        Assert.NotNull(repo.Saved);
        Assert.Contains(audit.Events, entry => entry.ActionName == "academy.pricing_changed");
    }

    [Fact]
    public async Task A_price_saved_in_shillings_before_the_change_is_not_read_as_dollars()
    {
        var (service, repo, _) = Build();
        var scope = TenantScope.Create("t", "w");
        await repo.SaveAsync(new AcademyPricing(scope, [new CoursePrice("claims", 500_000m, "UGX")]));

        var claims = (await service.GetAsync()).Prices.Single(price => price.CourseId == "claims");

        Assert.Equal(120m, claims.Amount);
        Assert.Equal("USD", claims.Currency);
    }

    [Theory]
    [InlineData("claims", -1)]
    [InlineData("claims", 10_001)]
    [InlineData("claims", 10.555)]
    [InlineData("not-a-course", 20)]
    public async Task A_price_that_is_out_of_range_or_for_an_unknown_course_is_refused(string course, double amount)
    {
        var (service, repo, _) = Build();

        await Assert.ThrowsAsync<AcademyPriceValidationException>(() => service.SaveAsync([new CoursePrice(course, (decimal)amount)]));
        Assert.Null(repo.Saved);
    }

    [Fact]
    public async Task A_course_can_be_free()
    {
        var (service, _, _) = Build();

        var saved = await service.SaveAsync([new CoursePrice("governance", 0m)]);

        Assert.Equal(0m, saved.Prices.Single(price => price.CourseId == "governance").Amount);
    }

    [Fact]
    public async Task Only_someone_who_manages_settings_can_change_prices_but_anyone_can_read_them()
    {
        var (service, repo, _) = Build(allowed: false);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.SaveAsync([new CoursePrice("claims", 10m)]));
        Assert.Null(repo.Saved);
        Assert.NotEmpty((await service.GetAsync()).Prices);
    }

    private sealed class Ctx : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-operations", "user-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class Perms(bool allowed) : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            allowed ? Task.CompletedTask : throw new PermissionDeniedException(permission);

        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
    }

    private sealed class Audit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class Repo : IAcademyPricingRepository
    {
        public AcademyPricing? Saved { get; private set; }

        public Task<AcademyPricing?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(Saved);

        public Task SaveAsync(AcademyPricing pricing, CancellationToken cancellationToken = default)
        {
            Saved = pricing;
            return Task.CompletedTask;
        }
    }
}
