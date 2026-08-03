using System.Text.Json;
using InsuranceIntelligence.Api.Claims;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ClaimsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ListClaims_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            "tenant-membership",
            "workspace-membership",
            "membership-claims-user",
            ["ClaimsOfficer"]));
        var service = new CapturingClaimsQueryService(tenantContext);
        var httpContext = Context("tenant-spoofed", "workspace-spoofed", "spoofed-user");

        var result = await ClaimsController.ListClaims(
            search: null,
            status: null,
            riskLevel: null,
            channel: null,
            assignedUserId: null,
            attentionOnly: null,
            skip: null,
            take: null,
            service,
            tenantContext,
            new InfrastructureOptions { AuthMode = "Development" },
            httpContext,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var item = body.EnumerateArray().Single();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("tenant-membership", service.ObservedContext?.TenantId);
        Assert.Equal("workspace-membership", service.ObservedContext?.WorkspaceId);
        Assert.Equal("membership-claims-user", service.ObservedContext?.UserId);
        Assert.Equal("tenant-membership", item.GetProperty("customerName").GetString());
        Assert.Equal(50, service.ObservedQuery?.Take);
    }

    private static DefaultHttpContext Context(string spoofedTenantId, string spoofedWorkspaceId, string spoofedUserId)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-claims-controller" };
        context.Request.Headers["X-Tenant-Id"] = spoofedTenantId;
        context.Request.Headers["X-Workspace-Id"] = spoofedWorkspaceId;
        context.Request.Headers["x-demo-role"] = "Admin";
        context.Request.Headers["x-demo-user-id"] = spoofedUserId;
        return context;
    }

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed class CapturingClaimsQueryService(ITenantContextAccessor tenantContextAccessor) : IClaimsQueryService
    {
        public TenantContext? ObservedContext { get; private set; }

        public ClaimsQuery? ObservedQuery { get; private set; }

        public Task<IReadOnlyCollection<ClaimListItemDto>> ListAsync(
            ClaimsQuery query,
            CancellationToken cancellationToken = default)
        {
            ObservedContext = tenantContextAccessor.Current;
            ObservedQuery = query;
            return Task.FromResult<IReadOnlyCollection<ClaimListItemDto>>(
            [
                new ClaimListItemDto(
                    Guid.NewGuid(),
                    ObservedContext.TenantId,
                    "POL-1",
                    "Motor",
                    "Accident",
                    "Portal",
                    "New",
                    0m,
                    "Low",
                    "Normal",
                    "OnTrack",
                    null,
                    null,
                    null,
                    DateTimeOffset.UtcNow)
            ]);
        }

        public Task<ClaimDetailDto?> GetAsync(Guid claimId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ClaimDetailDto?>(null);

        public Task<IReadOnlyCollection<ClaimListItemDto>> GetAttentionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ClaimListItemDto>>([]);

        public Task<ClaimsDailySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClaimsDailySummaryDto(date, 0, 0, 0, 0, 0));

        public Task<ClaimDraftContextDto?> GetDraftContextAsync(Guid claimId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ClaimDraftContextDto?>(null);
    }
}
