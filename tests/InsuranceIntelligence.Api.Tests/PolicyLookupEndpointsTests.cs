using System.Text.Json;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Infrastructure.Policies;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

// FNOL six-step intake, step 2 (docs/03-design-architecture/iip_fnol_six_step_intake_design.md):
// resolve a policy number into real context and flag a possible duplicate before the claimant is
// asked anything else. PolicyId (an internal GUID nobody types) and PolicyNumber (what a claimant
// actually has) are deliberately different fields, this proves lookup works by the human one.
public sealed class PolicyLookupEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task LookupByPolicyNumber_ReturnsPolicyAndCustomerContext_ForTheHumanReadableNumber()
    {
        var dependencies = CreateDependencies();
        await dependencies.PolicyRepository.UpsertAsync(
            new Policy(
                Guid.NewGuid().ToString(), Scope.TenantId, Scope.WorkspaceId, "NSA/MO/2026/00033", "Motor",
                "Comprehensive", "cust-1", "branch-1",
                DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10))),
            CancellationToken.None);
        await dependencies.CustomerRepository.UpsertAsync(
            new InsuranceIntelligence.Domain.Customers.Customer(
                "cust-1", Scope.TenantId, Scope.WorkspaceId, "Patricia Okello", "patricia@example.ug", "+256700000001"),
            CancellationToken.None);

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            "NSA/MO/2026/00033",
            dependencies.PolicyLookupProvider,
            dependencies.CustomerRepository,
            dependencies.CaseRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<PolicyLookupResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Motor", body!.ProductLine);
        Assert.Equal("Patricia Okello", body.CustomerName);
        Assert.True(body.IsActive);
        Assert.Empty(body.PossibleDuplicateClaims);
    }

    [Fact]
    public async Task LookupByPolicyNumber_IsCaseInsensitive_AndTrimsWhitespace()
    {
        var dependencies = CreateDependencies();
        await dependencies.PolicyRepository.UpsertAsync(
            new Policy(Guid.NewGuid().ToString(), Scope.TenantId, Scope.WorkspaceId, "NSA/MO/2026/00033", "Motor",
                null, null, null, null, null),
            CancellationToken.None);

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            "  nsa/mo/2026/00033  ",
            dependencies.PolicyLookupProvider, dependencies.CustomerRepository, dependencies.CaseRepository,
            ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<PolicyLookupResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("NSA/MO/2026/00033", body!.PolicyNumber);
    }

    [Fact]
    public async Task LookupByPolicyNumber_ReturnsNotFound_ForAnUnknownNumber()
    {
        var dependencies = CreateDependencies();

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            "NSA/MO/2026/99999",
            dependencies.PolicyLookupProvider, dependencies.CustomerRepository, dependencies.CaseRepository,
            ScopedContext(), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task LookupByPolicyNumber_RequiresAPolicyNumber()
    {
        var dependencies = CreateDependencies();

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            null, dependencies.PolicyLookupProvider, dependencies.CustomerRepository, dependencies.CaseRepository,
            ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("policyNumber", out _));
    }

    [Fact]
    public async Task LookupByPolicyNumber_NeverReturnsPolicyFromAnotherTenantWorkspace()
    {
        var dependencies = CreateDependencies();
        await dependencies.PolicyRepository.UpsertAsync(
            new Policy(Guid.NewGuid().ToString(), "tenant-other", "workspace-other", "NSA/MO/2026/00033", "Motor",
                null, null, null, null, null),
            CancellationToken.None);

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            "NSA/MO/2026/00033",
            dependencies.PolicyLookupProvider, dependencies.CustomerRepository, dependencies.CaseRepository,
            ScopedContext(), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Theory]
    [InlineData(CaseStatus.New, true)]
    [InlineData(CaseStatus.Assigned, true)]
    [InlineData(CaseStatus.InReview, true)]
    [InlineData(CaseStatus.Escalated, true)]
    [InlineData(CaseStatus.Settled, false)]
    [InlineData(CaseStatus.Closed, false)]
    public async Task LookupByPolicyNumber_FlagsAnOpenClaimAsAPossibleDuplicate_ButNotATerminalOne(CaseStatus status, bool expectDuplicateFlagged)
    {
        var dependencies = CreateDependencies();
        await dependencies.PolicyRepository.UpsertAsync(
            new Policy(Guid.NewGuid().ToString(), Scope.TenantId, Scope.WorkspaceId, "NSA/MO/2026/00033", "Motor",
                null, null, null, null, null),
            CancellationToken.None);
        var existingCaseId = Guid.NewGuid();
        await dependencies.CaseRepository.SaveAsync(BuildCase(existingCaseId, "NSA/MO/2026/00033", status), CancellationToken.None);

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            "NSA/MO/2026/00033",
            dependencies.PolicyLookupProvider, dependencies.CustomerRepository, dependencies.CaseRepository,
            ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<PolicyLookupResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        if (expectDuplicateFlagged)
        {
            Assert.Contains(body!.PossibleDuplicateClaims, warning => warning.CaseId == existingCaseId);
        }
        else
        {
            Assert.Empty(body!.PossibleDuplicateClaims);
        }
    }

    [Fact]
    public async Task LookupByPolicyNumber_NeverFlagsAnotherPolicysClaimAsADuplicate()
    {
        var dependencies = CreateDependencies();
        await dependencies.PolicyRepository.UpsertAsync(
            new Policy(Guid.NewGuid().ToString(), Scope.TenantId, Scope.WorkspaceId, "NSA/MO/2026/00033", "Motor",
                null, null, null, null, null),
            CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(
            BuildCase(Guid.NewGuid(), "NSA/MO/2026/00099", CaseStatus.New), CancellationToken.None);

        var result = await PolicyLookupEndpoints.LookupByPolicyNumber(
            "NSA/MO/2026/00033",
            dependencies.PolicyLookupProvider, dependencies.CustomerRepository, dependencies.CaseRepository,
            ScopedContext(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<PolicyLookupResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Empty(body!.PossibleDuplicateClaims);
    }

    private static ClaimCase BuildCase(Guid caseId, string policyNumber, CaseStatus status) => new()
    {
        CaseId = caseId,
        Scope = Scope,
        CustomerName = "Existing Claimant",
        PolicyNumber = policyNumber,
        ProductLine = "Motor",
        ClaimType = "Own damage",
        Channel = "StaffEntry",
        IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
        IncidentDescription = "Pre-existing claim used to prove duplicate detection.",
        Status = status,
        CompletenessScore = 0m,
        CreatedByUserId = "user-claims-1",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-3),
        CorrelationId = "corr-duplicate-seed",
    };

    private sealed record Dependencies(
        InMemoryPolicyRepository PolicyRepository,
        IPolicyLookupProvider PolicyLookupProvider,
        InMemoryCustomerRepository CustomerRepository,
        InMemoryCaseRepository CaseRepository);

    private static Dependencies CreateDependencies()
    {
        var policyRepository = new InMemoryPolicyRepository();
        return new(policyRepository, new InternalPolicyLookupProvider(policyRepository), new InMemoryCustomerRepository(), new InMemoryCaseRepository());
    }

    private static DefaultHttpContext ScopedContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }
}
