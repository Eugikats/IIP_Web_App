using System.Text.Json;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ChannelQueryServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_RequiresChannelsReadPermission()
    {
        var service = new ChannelQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(),
            new RecordingChannelReadRepository([]));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetOverviewAsync());
    }

    [Fact]
    public async Task GetOverviewAsync_UsesTenantWorkspaceScopeAndCanonicalDtoValues()
    {
        var repository = new RecordingChannelReadRepository(
        [
            new ChannelConversationReadModel(
                "conv-1",
                "Amina Okello",
                "WhatsApp",
                "Claim_Fnol",
                "Claims team",
                0.87m,
                "open",
                "claims-1",
                "Customer needs help uploading a police report.",
                new DateTimeOffset(2026, 7, 3, 10, 45, 0, TimeSpan.Zero))
        ]);
        var service = new ChannelQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewChannels),
            repository);

        var rows = await service.GetOverviewAsync();

        var row = Assert.Single(rows);
        Assert.Equal("tenant-a", repository.LastTenantId);
        Assert.Equal("workspace-a", repository.LastWorkspaceId);
        Assert.Equal("conv-1", row.Id);
        Assert.Equal(ChannelValues.WhatsApp, row.Channel);
        Assert.Equal(ChannelDetectedIntentValues.ClaimFnol, row.DetectedIntent);
        Assert.Equal("2026-07-03T10:45:00.0000000+00:00", row.LastActivityAt);
    }

    [Fact]
    public async Task GetOverviewAsync_ReturnsEmptyWhenNoRowsExist()
    {
        var service = new ChannelQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewChannels),
            new RecordingChannelReadRepository([]));

        var rows = await service.GetOverviewAsync();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetOverviewAsync_DoesNotExposeRawBodiesOrProviderIds()
    {
        var service = new ChannelQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewChannels),
            new RecordingChannelReadRepository(
            [
                new ChannelConversationReadModel(
                    "conv-1",
                    "Amina Okello",
                    ChannelValues.Email,
                    ChannelDetectedIntentValues.GeneralService,
                    "Service desk",
                    0.55m,
                    "open",
                    null,
                    "Safe snippet only.",
                    DateTimeOffset.UtcNow)
            ]));

        var row = Assert.Single(await service.GetOverviewAsync());
        var json = JsonSerializer.Serialize(row, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("lastMessage", json, StringComparison.Ordinal);
        Assert.DoesNotContain("body", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChannelConversationRowDto_SerializesToExpectedCamelCaseContract()
    {
        var row = new ChannelConversationRowDto(
            "conv-1",
            "Amina Okello",
            ChannelValues.Email,
            ChannelDetectedIntentValues.PolicyInquiry,
            "Policy support",
            0.73m,
            "open",
            "service-1",
            "Policy question received.",
            "2026-07-03T10:45:00.0000000+00:00");

        var json = JsonSerializer.Serialize(row, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"customerName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"channel\"", json, StringComparison.Ordinal);
        Assert.Contains("\"detectedIntent\"", json, StringComparison.Ordinal);
        Assert.Contains("\"suggestedRouting\"", json, StringComparison.Ordinal);
        Assert.Contains("\"aiConfidence\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\"", json, StringComparison.Ordinal);
        Assert.Contains("\"assignedTo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lastMessage\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lastActivityAt\"", json, StringComparison.Ordinal);
    }

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

    private sealed class RecordingChannelReadRepository(
        IReadOnlyList<ChannelConversationReadModel> rows) : IChannelReadRepository
    {
        public string? LastTenantId { get; private set; }
        public string? LastWorkspaceId { get; private set; }

        public Task<IReadOnlyList<ChannelConversationReadModel>> GetOverviewAsync(
            string tenantId,
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastWorkspaceId = workspaceId;
            return Task.FromResult(rows);
        }
    }
}
