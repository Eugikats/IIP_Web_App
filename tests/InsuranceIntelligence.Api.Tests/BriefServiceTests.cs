using InsuranceIntelligence.Application.Brief;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Retention;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class BriefServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task GetAsync_RequiresAClaimsViewPermission()
    {
        var service = BuildService("officer-1", []);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetAsync());
    }

    [Fact]
    public async Task GetAsync_OnlyRanksClaimsAssignedToThisUser()
    {
        var caseRepository = new InMemoryCaseRepository();
        await caseRepository.SaveAsync(Claim("mine", "officer-1"));
        await caseRepository.SaveAsync(Claim("someone-elses", "officer-2"));
        var service = BuildService("officer-1", [Permission.ViewAssignedClaims], caseRepository);

        var brief = await service.GetAsync();

        var item = Assert.Single(brief.Items);
        Assert.Equal("Customer mine", item.CustomerName);
    }

    [Fact]
    public async Task GetAsync_WithNoWork_ReturnsAnHonestlyEmptyList()
    {
        var service = BuildService("officer-1", [Permission.ViewAssignedClaims]);

        var brief = await service.GetAsync();

        Assert.Empty(brief.Items);
    }

    [Fact]
    public async Task GetAsync_HandledForYou_OnlyShowsThisUsersRecentApprovals()
    {
        var auditRepository = new InMemoryAuditRepository();
        var caseId = Guid.NewGuid();
        await auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), Scope, caseId, null, "officer-1", ActorType.User,
            "customer_update.approved", DateTimeOffset.UtcNow.AddHours(-1), null, "Approved",
            [], null, null, "approved", null, "Reviewed and approved.", "corr-1"));
        await auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), Scope, caseId, null, "officer-2", ActorType.User,
            "customer_update.approved", DateTimeOffset.UtcNow.AddHours(-1), null, "Approved",
            [], null, null, "approved", null, "Someone else's approval.", "corr-2"));
        var service = BuildService("officer-1", [Permission.ViewAssignedClaims], auditRepository: auditRepository);

        var brief = await service.GetAsync();

        var handled = Assert.Single(brief.HandledForYou);
        Assert.Equal(caseId.ToString(), handled.CaseId);
    }

    [Fact]
    public async Task GetAsync_WithRetentionServiceAndViewCustomersPermission_IncludesTheRetentionLine()
    {
        var service = BuildService(
            "officer-1",
            [Permission.ViewAssignedClaims, Permission.ViewCustomers],
            retentionWatchService: new FixedRetentionWatchService(new RetentionWatchSummaryDto(3, 4_500_000m, "UGX", [])));

        var brief = await service.GetAsync();

        Assert.NotNull(brief.Retention);
        Assert.Equal(3, brief.Retention!.PoliciesAtRisk);
        Assert.Equal(4_500_000m, brief.Retention.PremiumAtRisk);
    }

    [Fact]
    public async Task GetAsync_WithRetentionServiceButNoViewCustomersPermission_OmitsTheRetentionLine()
    {
        var service = BuildService(
            "officer-1",
            [Permission.ViewAssignedClaims],
            retentionWatchService: new FixedRetentionWatchService(new RetentionWatchSummaryDto(3, 4_500_000m, "UGX", [])));

        var brief = await service.GetAsync();

        Assert.Null(brief.Retention);
    }

    [Fact]
    public async Task GetAsync_NoPoliciesAtRisk_OmitsTheRetentionLineRatherThanShowingAZero()
    {
        var service = BuildService(
            "officer-1",
            [Permission.ViewAssignedClaims, Permission.ViewCustomers],
            retentionWatchService: new FixedRetentionWatchService(new RetentionWatchSummaryDto(0, 0m, "UGX", [])));

        var brief = await service.GetAsync();

        Assert.Null(brief.Retention);
    }

    private sealed class FixedRetentionWatchService(RetentionWatchSummaryDto summary) : IRetentionWatchService
    {
        public Task<RetentionWatchSummaryDto> GetWatchAsync(CancellationToken cancellationToken = default) => Task.FromResult(summary);
        public Task<RetentionReminderDto> DraftReminderAsync(string policyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RetentionReminderDto> ApproveAndSendAsync(Guid reminderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static ClaimCase Claim(string suffix, string assignedUserId) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = $"Customer {suffix}",
            PolicyNumber = $"POL-{suffix}",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 9, 1),
            IncidentDescription = "Test claim.",
            Status = CaseStatus.Assigned,
            CompletenessScore = 100m,
            AssignedUserId = assignedUserId,
            CreatedByUserId = assignedUserId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CorrelationId = $"corr-{suffix}",
        };

    private static BriefService BuildService(
        string userId,
        Permission[] permissions,
        InMemoryCaseRepository? caseRepository = null,
        InMemoryAuditRepository? auditRepository = null,
        IRetentionWatchService? retentionWatchService = null)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, userId, []));
        return new BriefService(
            caseRepository ?? new InMemoryCaseRepository(),
            auditRepository ?? new InMemoryAuditRepository(),
            accessor,
            new FixedPermissionService(permissions),
            retentionWatchService);
    }

    private sealed class FixedPermissionService(params Permission[] permissions) : IPermissionService
    {
        private readonly HashSet<Permission> permissionSet = permissions.ToHashSet();

        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissionSet.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!permissionSet.Contains(permission))
            {
                throw new PermissionDeniedException(permission);
            }

            return Task.CompletedTask;
        }
    }
}
