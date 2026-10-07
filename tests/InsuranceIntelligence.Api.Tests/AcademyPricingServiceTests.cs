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
        Assert.All(pricing.Prices, price => Assert.Equal(50m, price.Amount));
        Assert.All(pricing.Prices, price => Assert.Equal("USD", price.Currency));
    }

    [Fact]
    public async Task Changing_one_price_leaves_the_others_at_the_seed_price_and_is_remembered_and_audited()
    {
        var (service, repo, audit) = Build();

        await service.SaveAsync([new CoursePrice("claims", 75m)]);
        var again = await service.GetAsync();

        Assert.Equal(75m, again.Prices.Single(price => price.CourseId == "claims").Amount);
        Assert.Equal(50m, again.Prices.Single(price => price.CourseId == "governance").Amount);
        Assert.NotNull(repo.Saved);
        Assert.Contains(audit.Events, entry => entry.ActionName == "academy.pricing_changed");
    }

    [Theory]
    [InlineData("claims", -1)]
    [InlineData("claims", 10001)]
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
