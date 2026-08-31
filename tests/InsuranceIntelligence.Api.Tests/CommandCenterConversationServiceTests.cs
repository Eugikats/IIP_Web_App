using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CommandCenterConversationServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-conversations", "workspace-claims");

    [Fact]
    public async Task QueryCreatesOwnerScopedConversationAndReopenMarksHistoryForRegrounding()
    {
        var harness = new Harness("owner-1");

        var response = await harness.Service.QueryAsync(new CommandCenterQueryRequest("Show SLA breaches."), "corr-1");
        var conversationId = Assert.IsType<Guid>(response.ConversationId);
        Assert.IsType<Guid>(response.TurnId);

        var list = await harness.Service.ListAsync(archived: false, limit: 20);
        var summary = Assert.Single(list.Items);
        Assert.Equal(conversationId, summary.ConversationId);
        Assert.Equal("Review SLA status", summary.Title);

        var detail = await harness.Service.GetAsync(conversationId, "corr-2");
        Assert.Equal("required_before_next_turn", detail.GroundingStatus);
        Assert.True(detail.CanContinue);
        Assert.Equal("Show SLA breaches.", Assert.Single(detail.Turns).Question);
        Assert.Contains(harness.AuditRepositoryEvents(), item => item.ActionName == "conversation.viewed");
    }

    [Fact]
    public async Task OwnerWorkspaceAndTenantBoundariesReturnNoConversation()
    {
        var repository = new InMemoryCommandCenterConversationRepository();
        var owner = new Harness("owner-1", repository);
        var response = await owner.Service.QueryAsync(new CommandCenterQueryRequest("Show claims needing attention."), "corr-owner");
        var conversationId = Assert.IsType<Guid>(response.ConversationId);

        var otherUser = new Harness("owner-2", repository);
        await Assert.ThrowsAsync<CommandCenterConversationNotFoundException>(() => otherUser.Service.GetAsync(conversationId, "corr-other"));

        var otherWorkspace = new Harness("owner-1", repository, TenantScope.Create("tenant-conversations", "workspace-other"));
        await Assert.ThrowsAsync<CommandCenterConversationNotFoundException>(() => otherWorkspace.Service.GetAsync(conversationId, "corr-workspace"));
        Assert.Empty((await otherUser.Service.ListAsync(false, 20)).Items);
    }

    [Fact]
    public async Task ArchiveUsesExpectedVersionAndPreventsContinuationUntilRestore()
    {
        var harness = new Harness("owner-1");
        var response = await harness.Service.QueryAsync(new CommandCenterQueryRequest("Show missing documents."), "corr-create");
        var conversationId = Assert.IsType<Guid>(response.ConversationId);
        var current = Assert.Single((await harness.Service.ListAsync(false, 20)).Items);

        var archived = await harness.Service.ArchiveAsync(conversationId, new ChangeConversationStatusRequest(current.Version), "corr-archive");
        Assert.Equal("archived", archived.Status);
        await Assert.ThrowsAsync<CommandCenterConversationConflictException>(() =>
            harness.Service.ArchiveAsync(conversationId, new ChangeConversationStatusRequest(current.Version), "corr-stale"));
        await Assert.ThrowsAsync<CommandCenterConversationConflictException>(() =>
            harness.Service.QueryAsync(new CommandCenterQueryRequest("Continue.", conversationId), "corr-continue"));

        var restored = await harness.Service.RestoreAsync(conversationId, new ChangeConversationStatusRequest(archived.Version), "corr-restore");
        Assert.Equal("active", restored.Status);
    }

    private sealed class Harness
    {
        private readonly InMemoryAuditRepository auditRepository = new();

        public Harness(string userId, InMemoryCommandCenterConversationRepository? repository = null, TenantScope? scope = null)
        {
            var context = new TenantContextAccessor();
            context.SetCurrent(new TenantContext((scope ?? Scope).TenantId, (scope ?? Scope).WorkspaceId, userId, ["ClaimsSupervisor"]));
            var permissions = new PermissionService(context, new LocalTestPermissionReadRepository());
            Service = new CommandCenterConversationService(context, permissions, new StubQueryService(), repository ?? new InMemoryCommandCenterConversationRepository(), auditRepository);
        }

        public ICommandCenterConversationService Service { get; }

        public IReadOnlyCollection<InsuranceIntelligence.Domain.Audit.AuditEvent> AuditRepositoryEvents() =>
            auditRepository.ListAsync(Scope).GetAwaiter().GetResult();
    }

    private sealed class StubQueryService : ICommandCenterQueryService
    {
        public Task<CommandCenterQueryResponse> QueryAsync(CommandCenterQueryRequest request, string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommandCenterQueryResponse(
                [new TextBlock("Current workspace data checked.")],
                Guid.NewGuid(),
                correlationId,
                "Deterministic",
                "command-center-query-v1",
                "Internal",
                CanTriggerDecision: false,
                DateTimeOffset.UtcNow));
    }
}
