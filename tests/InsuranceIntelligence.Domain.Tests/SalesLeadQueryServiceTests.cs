using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class SalesLeadQueryServiceTests
{
    [Fact]
    public async Task GetLeadDetailAsync_RequiresSalesLeadsReadPermission()
    {
        var service = new SalesLeadQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(),
            new RecordingSalesLeadReadRepository(Lead("lead-1")));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetLeadDetailAsync("lead-1"));
    }

    [Fact]
    public async Task GetLeadDetailAsync_ReturnsOnlyTenantWorkspaceLead()
    {
        var repository = new RecordingSalesLeadReadRepository(Lead("lead-1"));
        var service = new SalesLeadQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewSalesLeads),
            repository);

        var lead = await service.GetLeadDetailAsync(" lead-1 ");

        Assert.NotNull(lead);
        Assert.Equal("tenant-a", repository.LastTenantId);
        Assert.Equal("workspace-a", repository.LastWorkspaceId);
        Assert.Equal("lead-1", repository.LastLeadId);
        Assert.Equal("lead-1", lead.LeadId);
        Assert.Equal("Broker referral", lead.LeadSource);
    }

    [Fact]
    public async Task GetLeadDetailAsync_ReturnsNullWhenNotFoundOrNotVisible()
    {
        var service = new SalesLeadQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewSalesLeads),
            new RecordingSalesLeadReadRepository(null));

        var lead = await service.GetLeadDetailAsync("lead-1");

        Assert.Null(lead);
    }

    [Fact]
    public async Task GetLeadDetailAsync_RejectsBlankLeadId()
    {
        var service = new SalesLeadQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewSalesLeads),
            new RecordingSalesLeadReadRepository(Lead("lead-1")));

        var ex = await Assert.ThrowsAsync<SalesLeadQueryValidationException>(() => service.GetLeadDetailAsync(" "));

        Assert.Equal("leadId", ex.Field);
    }

    [Fact]
    public async Task GetLeadDetailAsync_DoesNotExposeHiddenScoringInputsOrRawTranscripts()
    {
        var service = new SalesLeadQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewSalesLeads),
            new RecordingSalesLeadReadRepository(Lead("lead-1")));

        var lead = await service.GetLeadDetailAsync("lead-1");
        var json = JsonSerializer.Serialize(lead, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("conversationSummary", json, StringComparison.Ordinal);
        Assert.Contains("conversionProbability", json, StringComparison.Ordinal);
        Assert.DoesNotContain("rawTranscript", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hiddenScoring", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("providerMetadata", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modelNotes", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalesLeadDetailDto_SerializesToExpectedCamelCaseContract()
    {
        var lead = ToDto(Lead("lead-1"));

        var json = JsonSerializer.Serialize(lead, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"leadId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"leadSource\"", json, StringComparison.Ordinal);
        Assert.Contains("\"customerNeed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"conversationSummary\"", json, StringComparison.Ordinal);
        Assert.Contains("\"productFit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"quoteStatus\"", json, StringComparison.Ordinal);
        Assert.Contains("\"requiredInformation\"", json, StringComparison.Ordinal);
        Assert.Contains("\"followUpActions\"", json, StringComparison.Ordinal);
        Assert.Contains("\"assignedSalesOfficer\"", json, StringComparison.Ordinal);
        Assert.Contains("\"conversionProbability\"", json, StringComparison.Ordinal);
    }

    private static SalesLeadDetailReadModel Lead(string leadId) =>
        new(
            leadId,
            "Broker referral",
            "Customer requested comprehensive motor cover.",
            "Broker asked for a quote and claims history requirements.",
            "Motor comprehensive",
            "Needs information",
            ["Vehicle registration", "Prior claims declaration"],
            ["Request vehicle details", "Assign sales officer"],
            "sales-1",
            0.62m);

    private static SalesLeadDetailDto ToDto(SalesLeadDetailReadModel lead) =>
        new(
            lead.LeadId,
            lead.LeadSource,
            lead.CustomerNeed,
            lead.ConversationSummary,
            lead.ProductFit,
            lead.QuoteStatus,
            lead.RequiredInformation,
            lead.FollowUpActions,
            lead.AssignedSalesOfficer,
            lead.ConversionProbability);

    private static TenantContextAccessor CurrentContext(string tenantId, string workspaceId, string userId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(tenantId, workspaceId, userId, []));
        return accessor;
    }

    private sealed class FixedPermissionService(params Permission[] permissions) : IPermissionService
    {
        private readonly HashSet<Permission> _permissions = permissions.ToHashSet();

        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(_permissions.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!_permissions.Contains(permission))
            {
                throw new PermissionDeniedException(permission);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSalesLeadReadRepository(
        SalesLeadDetailReadModel? lead) : ISalesLeadReadRepository
    {
        public string? LastTenantId { get; private set; }
        public string? LastWorkspaceId { get; private set; }
        public string? LastLeadId { get; private set; }

        public Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(
            string tenantId,
            string workspaceId,
            string leadId,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastWorkspaceId = workspaceId;
            LastLeadId = leadId;
            return Task.FromResult(lead);
        }
    }
}
