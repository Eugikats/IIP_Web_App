using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Build-loop item 35: the everyday lookups (a claim by policy number, phone, email, status or team; a customer by contact; a policy by
/// number) are answered with indexes, not by reading every record in the workspace, and rows saved before the lookup columns existed are still found.
/// </summary>
public sealed class IndexedLookupTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    /// <summary>Counts every whole-workspace read, and forwards the targeted lookups to the real store so what it measures is what runs.</summary>
    private sealed class CountingCases(ICaseRepository inner) : ICaseRepository
    {
        public int WholeWorkspaceReads { get; private set; }
        public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default) => inner.SaveAsync(claimCase, cancellationToken);
        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) => inner.GetAsync(caseId, cancellationToken);
        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
        {
            WholeWorkspaceReads++;
            return inner.ListAsync(scope, cancellationToken);
        }

        public Task<IReadOnlyCollection<ClaimCase>> ListByPolicyNumbersAsync(TenantScope scope, IReadOnlyCollection<string> policyNumbers, CancellationToken cancellationToken = default) => inner.ListByPolicyNumbersAsync(scope, policyNumbers, cancellationToken);
        public Task<IReadOnlyCollection<ClaimCase>> ListByContactAsync(TenantScope scope, string? phone, string? email, CancellationToken cancellationToken = default) => inner.ListByContactAsync(scope, phone, email, cancellationToken);
        public Task<IReadOnlyCollection<ClaimCase>> ListByStatusesAsync(TenantScope scope, IReadOnlyCollection<CaseStatus> statuses, CancellationToken cancellationToken = default) => inner.ListByStatusesAsync(scope, statuses, cancellationToken);
        public Task<IReadOnlyCollection<ClaimCase>> ListByTeamAsync(TenantScope scope, string team, CancellationToken cancellationToken = default) => inner.ListByTeamAsync(scope, team, cancellationToken);
    }

    private static ClaimCase Claim(TenantScope scope, string policy, string? phone, string? email, CaseStatus status = CaseStatus.New, string? team = "Motor Claims") =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Test Customer",
            CustomerEmail = email,
            CustomerPhone = phone,
            PolicyNumber = policy,
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Phone",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            IncidentDescription = "Collision.",
            Status = status,
            CompletenessScore = 0,
            AssignedTeam = team,
            CreatedByUserId = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "test",
        };

    private (IServiceScope Scope, ICaseRepository Cases, InsuranceIntelligenceDbContext Db) Open()
    {
        var scope = factory.Services.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<ICaseRepository>(), scope.ServiceProvider.GetRequiredService<InsuranceIntelligenceDbContext>());
    }

    // ---- the keys

    [Theory]
    [InlineData("+256 700 123 456", "123456")]
    [InlineData("0700123456", "123456")]
    [InlineData("256700123456", "123456")]
    public void TheSamePhoneNumberWrittenDifferentlyGetsTheSameKey(string phone, string tail) =>
        Assert.EndsWith(tail, LookupKeys.PhoneKey(phone));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("abc")]
    public void ANumberTooShortToIdentifyAnyoneHasNoKey(string? phone) => Assert.Null(LookupKeys.PhoneKey(phone));

    [Fact]
    public void PolicyAndEmailKeysIgnoreCaseAndSurroundingSpace()
    {
        Assert.Equal("POL-123/A", LookupKeys.PolicyKey("  pol-123/a "));
        Assert.Equal("pat@example.test", LookupKeys.EmailKey(" Pat@Example.TEST "));
        Assert.Null(LookupKeys.PolicyKey("  "));
        Assert.Null(LookupKeys.EmailKey(null));
    }

    [Fact]
    public void EveryLookupColumnHasAnIndexLedByTheWorkspace()
    {
        var (scope, _, db) = Open();
        using var _ = scope;

        string[][] indexed = [
            ["TenantId", "WorkspaceId", "PolicyKey"], ["TenantId", "WorkspaceId", "PhoneKey"], ["TenantId", "WorkspaceId", "EmailKey"],
            ["TenantId", "WorkspaceId", "StatusName"], ["TenantId", "WorkspaceId", "TeamKey"], ["LookupVersion"]];
        var caseIndexes = db.Model.FindEntityType(typeof(PersistedAggregate))!.GetIndexes().Select(index => index.Properties.Select(property => property.Name).ToArray()).ToArray();
        var customerIndexes = db.Model.FindEntityType(typeof(PersistedCustomer))!.GetIndexes().Select(index => index.Properties.Select(property => property.Name).ToArray()).ToArray();
        var policyIndexes = db.Model.FindEntityType(typeof(PersistedPolicy))!.GetIndexes().Select(index => index.Properties.Select(property => property.Name).ToArray()).ToArray();

        foreach (var wanted in indexed)
        {
            Assert.Contains(caseIndexes, found => found.SequenceEqual(wanted));
        }

        Assert.Contains(customerIndexes, found => found.SequenceEqual(["TenantId", "WorkspaceId", "PhoneKey"]));
        Assert.Contains(customerIndexes, found => found.SequenceEqual(["TenantId", "WorkspaceId", "EmailKey"]));
        Assert.Contains(policyIndexes, found => found.SequenceEqual(["TenantId", "WorkspaceId", "PolicyKey"]));
        Assert.Contains(policyIndexes, found => found.SequenceEqual(["TenantId", "WorkspaceId", "CustomerKey"]));
    }

    // ---- claims

    [Fact]
    public async Task AClaimIsFoundByPolicyNumberIgnoringCaseAndOnlyInItsOwnWorkspace()
    {
        var (open, cases, _) = Open();
        using var _ = open;
        var mine = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var theirs = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var claim = Claim(mine, "POL-ABC-1", null, null);
        await cases.SaveAsync(claim);
        await cases.SaveAsync(Claim(theirs, "POL-ABC-1", null, null));
        await cases.SaveAsync(Claim(mine, "POL-OTHER", null, null));

        var found = await cases.ListByPolicyNumbersAsync(mine, ["pol-abc-1 "]);

        Assert.Equal(claim.CaseId, Assert.Single(found).CaseId);
        Assert.Empty(await cases.ListByPolicyNumbersAsync(mine, ["POL-NOBODY"]));
        Assert.Empty(await cases.ListByPolicyNumbersAsync(mine, []));
    }

    [Fact]
    public async Task AClaimIsFoundByPhoneHoweverItWasWrittenOrByEmailIgnoringCase()
    {
        var (open, cases, _) = Open();
        using var _ = open;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var byPhone = Claim(scope, "P1", "+256 700 123 456", null);
        var byEmail = Claim(scope, "P2", null, "Pat@Example.test");
        var neither = Claim(scope, "P3", "+256 711 999 999", "other@example.test");
        foreach (var claim in new[] { byPhone, byEmail, neither })
        {
            await cases.SaveAsync(claim);
        }

        var phone = await cases.ListByContactAsync(scope, "0700123456", null);
        var email = await cases.ListByContactAsync(scope, null, "pat@example.TEST");
        var both = await cases.ListByContactAsync(scope, "0700123456", "pat@example.test");

        Assert.Equal(byPhone.CaseId, Assert.Single(phone).CaseId);
        Assert.Equal(byEmail.CaseId, Assert.Single(email).CaseId);
        Assert.Equal(2, both.Count);
        Assert.Empty(await cases.ListByContactAsync(scope, null, null));
    }

    [Fact]
    public async Task ClaimsAreFoundByStatusAndByTeamAndAStatusChangeMovesThem()
    {
        var (open, cases, _) = Open();
        using var _ = open;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var fresh = Claim(scope, "P1", null, null, CaseStatus.New, "Motor Claims");
        var closed = Claim(scope, "P2", null, null, CaseStatus.Closed, "Medical Claims");
        await cases.SaveAsync(fresh);
        await cases.SaveAsync(closed);

        Assert.Equal(fresh.CaseId, Assert.Single(await cases.ListByStatusesAsync(scope, CaseLookups.OpenStatuses)).CaseId);
        Assert.Equal(closed.CaseId, Assert.Single(await cases.ListByStatusesAsync(scope, [CaseStatus.Closed, CaseStatus.Settled])).CaseId);
        Assert.Equal(closed.CaseId, Assert.Single(await cases.ListByTeamAsync(scope, "Medical Claims")).CaseId);
        await cases.SaveAsync(fresh with { Status = CaseStatus.Closed });
        Assert.Empty(await cases.ListByStatusesAsync(scope, CaseLookups.OpenStatuses));
        Assert.Equal(2, (await cases.ListByStatusesAsync(scope, [CaseStatus.Closed])).Count);
    }

    // ---- rows saved before the lookup columns existed

    private static async Task SaveLegacyAsync(InsuranceIntelligenceDbContext db, ClaimCase claim)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(claim, Json.Options);
        db.Cases.Add(new PersistedAggregate { Id = claim.CaseId, TenantId = claim.Scope.TenantId, WorkspaceId = claim.Scope.WorkspaceId, PayloadJson = json, UpdatedAt = DateTimeOffset.UtcNow, LookupVersion = 0 });
        await db.SaveChangesAsync();
    }

    private static class Json
    {
        public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    }

    [Fact]
    public async Task ARowSavedBeforeTheColumnsExistedIsStillFoundAndTheBackfillFillsItInPlace()
    {
        var (open, cases, db) = Open();
        using var _ = open;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var old = Claim(scope, "POL-OLD-1", "+256 788 000 111", "old@example.test", CaseStatus.InReview);
        await SaveLegacyAsync(db, old);

        Assert.Equal(old.CaseId, Assert.Single(await cases.ListByPolicyNumbersAsync(scope, ["pol-old-1"])).CaseId);
        Assert.Equal(old.CaseId, Assert.Single(await cases.ListByContactAsync(scope, "0788000111", null)).CaseId);
        Assert.Equal(old.CaseId, Assert.Single(await cases.ListByStatusesAsync(scope, [CaseStatus.InReview])).CaseId);

        var filled = await open.ServiceProvider.GetRequiredService<LookupKeyBackfill>().RunAsync();

        Assert.True(filled >= 1);
        var row = await db.Cases.AsNoTracking().SingleAsync(item => item.Id == old.CaseId);
        Assert.Equal(1, row.LookupVersion);
        Assert.Equal("POL-OLD-1", row.PolicyKey);
        Assert.Equal("InReview", row.StatusName);
        Assert.Equal(old.CaseId, Assert.Single(await cases.ListByPolicyNumbersAsync(scope, ["POL-OLD-1"])).CaseId);
        Assert.Equal(0, await open.ServiceProvider.GetRequiredService<LookupKeyBackfill>().RunAsync());
    }

    [Fact]
    public async Task APolicyAndACustomerSavedBeforeTheColumnsExistedAreStillFound()
    {
        var (open, _, db) = Open();
        using var _ = open;
        var tenant = MockedApiFactory.NewTenant();
        var scope = TenantScope.Create(tenant, "workspace-main");
        var customerId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var customer = new InsuranceIntelligence.Domain.Customers.Customer(customerId.ToString(), tenant, "workspace-main", "Old Customer", "oldcustomer@example.test", "+256 777 222 333");
        var policy = new Policy(policyId.ToString(), tenant, "workspace-main", "POL-OLD-9", "Motor", "Comprehensive", customerId.ToString(), null, null, null);
        db.Customers.Add(new PersistedCustomer { Id = customerId, TenantId = tenant, WorkspaceId = "workspace-main", PayloadJson = System.Text.Json.JsonSerializer.Serialize(customer, Json.Options), UpdatedAt = DateTimeOffset.UtcNow });
        db.Policies.Add(new PersistedPolicy { Id = policyId, TenantId = tenant, WorkspaceId = "workspace-main", PayloadJson = System.Text.Json.JsonSerializer.Serialize(policy, Json.Options), UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var customers = open.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var policies = open.ServiceProvider.GetRequiredService<IPolicyRepository>();

        Assert.Single(await customers.FindByContactAsync(scope, "0777222333", null, default));
        Assert.Single(await customers.FindByContactAsync(scope, null, "OLDCUSTOMER@example.test", default));
        Assert.Equal("POL-OLD-9", (await policies.GetByPolicyNumberAsync("pol-old-9", scope, default))!.PolicyNumber);
        Assert.Single(await policies.ListByCustomerAsync(customerId.ToString(), scope, default));

        await open.ServiceProvider.GetRequiredService<LookupKeyBackfill>().RunAsync();

        Assert.Single(await customers.FindByContactAsync(scope, "0777222333", null, default));
        Assert.Equal("POL-OLD-9", (await policies.GetByPolicyNumberAsync("POL-OLD-9", scope, default))!.PolicyNumber);
        Assert.Single(await policies.ListByCustomerAsync(customerId.ToString(), scope, default));
        Assert.Null(await policies.GetByPolicyNumberAsync("POL-OLD-9", TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main"), default));
    }

    // ---- the everyday paths no longer read the whole workspace

    private WebApplicationFactory<Program> Counting(out Func<CountingCases?> counter)
    {
        CountingCases? last = null;
        counter = () => last;
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICaseRepository>();
            services.AddScoped<ICaseRepository>(sp =>
            {
                last = new CountingCases(ActivatorUtilities.CreateInstance<EfCaseRepository>(sp));
                return last;
            });
        }));
    }

    [Fact]
    public async Task AskingForALoginLinkReadsTheClaimsOnThatPolicyAndNotTheWholeWorkspace()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        for (var i = 0; i < 5; i++)
        {
            await TestData.CreateCaseAsync(staff, tenant);
        }

        using var host = Counting(out var counter);
        using var anonymous = host.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", tenant);
        anonymous.DefaultRequestHeaders.Add("X-Workspace-Id", "workspace-main");
        var response = await anonymous.PostAsJsonAsync("/api/v1/client-portal/magic-links", new { policyNumber = claim.PolicyNumber, contactValue = claim.Phone });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, counter()?.WholeWorkspaceReads ?? 0);
        using var other = host.CreateScopedClient(tenant);
        var outbox = (await MockedApiFactory.ReadJsonAsync(await other.GetAsync("/api/v1/dev/mock-outbox"))).EnumerateArray().ToArray();
        Assert.Contains(outbox, message => message.GetProperty("recipient").GetString() == claim.Phone && message.GetProperty("body").GetString()!.Contains("token=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpeningAClaimAndRunningTheFollowThroughNeverReadTheWholeWorkspace()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var seed = factory.CreateScopedClient(tenant);
        for (var i = 0; i < 4; i++)
        {
            await TestData.CreateCaseAsync(seed, tenant);
        }

        using var host = Counting(out var counter);
        using var admin = host.CreateScopedClient(tenant);
        var created = await TestData.CreateCaseAsync(admin, tenant);
        var follow = await admin.PostAsync("/api/v1/cases/follow-through/run", null);

        Assert.NotEqual(Guid.Empty, created.CaseId);
        Assert.True(follow.IsSuccessStatusCode, await follow.Content.ReadAsStringAsync());
        Assert.Equal(0, counter()?.WholeWorkspaceReads ?? 0);
    }

    [Fact]
    public async Task AClaimsOwnerIsStillChosenFromTheTeamsExistingMembers()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var first = await TestData.CreateCaseAsync(admin, tenant);
        using var services = factory.Services.CreateScope();
        var cases = services.ServiceProvider.GetRequiredService<ICaseRepository>();
        var saved = (await cases.GetAsync(first.CaseId))!;
        await cases.SaveAsync(saved with { AssignedUserId = "officer-1", AssignedTeam = saved.AssignedTeam ?? "Motor Claims" });

        var second = await TestData.CreateCaseAsync(admin, tenant);

        var claim = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{second.CaseId}"));
        Assert.Equal("officer-1", claim.GetProperty("assignedUserId").GetString());
    }
}
