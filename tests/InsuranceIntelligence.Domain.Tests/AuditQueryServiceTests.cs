using System.Text.Json;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class AuditQueryServiceTests
{
    [Fact]
    public async Task ListEventsAsync_RequiresAuditReadPermission()
    {
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(),
            new RecordingAuditReadRepository([AuditEvent("audit-1")]));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ListEventsAsync(new AuditEventListQuery()));
    }

    [Fact]
    public async Task ListEventsAsync_ReturnsOnlyTenantWorkspaceEvents()
    {
        var repository = new RecordingAuditReadRepository([AuditEvent("audit-1")]);
        var query = new AuditEventListQuery(
            EventType: AuditEventTypeValues.DataViewed,
            ActorUserId: "user-1",
            TargetType: "customer",
            TargetId: "customer-1",
            Page: 2,
            PageSize: 50);
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewAuditLog),
            repository);

        var rows = await service.ListEventsAsync(query);

        var row = Assert.Single(rows);
        Assert.Equal("tenant-a", repository.LastTenantId);
        Assert.Equal("workspace-a", repository.LastWorkspaceId);
        Assert.Equal(query, repository.LastQuery);
        Assert.Equal("audit-1", row.Id);
        Assert.Equal(AuditEventTypeValues.DataViewed, row.EventType);
    }

    [Fact]
    public async Task ListEventsAsync_ReturnsEmptyWhenNoEventsMatch()
    {
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewAuditLog),
            new RecordingAuditReadRepository([]));

        var rows = await service.ListEventsAsync(new AuditEventListQuery());

        Assert.Empty(rows);
    }

    [Theory]
    [InlineData(0, 100, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 201, "pageSize")]
    public async Task ListEventsAsync_RejectsInvalidPaging(int page, int pageSize, string field)
    {
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewAuditLog),
            new RecordingAuditReadRepository([]));

        var ex = await Assert.ThrowsAsync<AuditQueryValidationException>(() =>
            service.ListEventsAsync(new AuditEventListQuery(Page: page, PageSize: pageSize)));

        Assert.Equal(field, ex.Field);
    }

    [Fact]
    public async Task ListEventsAsync_RejectsInvalidDateRange()
    {
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewAuditLog),
            new RecordingAuditReadRepository([]));

        var ex = await Assert.ThrowsAsync<AuditQueryValidationException>(() =>
            service.ListEventsAsync(new AuditEventListQuery(
                FromUtc: DateTimeOffset.Parse("2026-07-04T00:00:00Z"),
                ToUtc: DateTimeOffset.Parse("2026-07-03T00:00:00Z"))));

        Assert.Equal("fromUtc", ex.Field);
    }

    [Fact]
    public async Task ListEventsAsync_DoesNotExposeRawPayloadsPromptsSecretsOrProviderExceptions()
    {
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewAuditLog),
            new RecordingAuditReadRepository(
            [
                AuditEvent(
                    "audit-1",
                    summary: "Raw provider exception payload included a system prompt and secret token.")
            ]));

        var row = Assert.Single(await service.ListEventsAsync(new AuditEventListQuery()));
        var json = JsonSerializer.Serialize(row, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("Sensitive audit summary redacted.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("provider exception", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListEventsAsync_UsesIsoOccurredAtAndNormalizesUnknownEventType()
    {
        var service = new AuditQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewAuditLog),
            new RecordingAuditReadRepository(
            [
                AuditEvent(
                    "audit-1",
                    eventType: "legacy.unknown",
                    occurredAt: new DateTimeOffset(2026, 7, 3, 10, 45, 0, TimeSpan.Zero))
            ]));

        var row = Assert.Single(await service.ListEventsAsync(new AuditEventListQuery()));

        Assert.Equal(AuditEventTypeValues.DataChanged, row.EventType);
        Assert.Equal("2026-07-03T10:45:00.0000000+00:00", row.OccurredAt);
    }

    [Fact]
    public void AuditEventRowDto_SerializesToExpectedCamelCaseContract()
    {
        var row = new AuditEventRowDto(
            "audit-1",
            AuditEventTypeValues.ReportExported,
            "user-1",
            "report",
            "report-1",
            "Report exported.",
            "2026-07-03T10:45:00.0000000+00:00",
            "correlation-1");

        var json = JsonSerializer.Serialize(row, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"eventType\"", json, StringComparison.Ordinal);
        Assert.Contains("\"actorUserId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"targetType\"", json, StringComparison.Ordinal);
        Assert.Contains("\"targetId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"summary\"", json, StringComparison.Ordinal);
        Assert.Contains("\"occurredAt\"", json, StringComparison.Ordinal);
        Assert.Contains("\"correlationId\"", json, StringComparison.Ordinal);
    }

    private static AuditEventReadModel AuditEvent(
        string id,
        string eventType = AuditEventTypeValues.DataViewed,
        string summary = "Customer profile viewed.",
        DateTimeOffset? occurredAt = null) =>
        new(
            id,
            eventType,
            "user-1",
            "customer",
            "customer-1",
            summary,
            occurredAt ?? new DateTimeOffset(2026, 7, 3, 10, 45, 0, TimeSpan.Zero),
            "correlation-1");

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

    private sealed class RecordingAuditReadRepository(
        IReadOnlyList<AuditEventReadModel> events) : IAuditReadRepository
    {
        public string? LastTenantId { get; private set; }
        public string? LastWorkspaceId { get; private set; }
        public AuditEventListQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<AuditEventReadModel>> ListEventsAsync(
            string tenantId,
            string workspaceId,
            AuditEventListQuery query,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastWorkspaceId = workspaceId;
            LastQuery = query;
            return Task.FromResult(events);
        }
    }
}
