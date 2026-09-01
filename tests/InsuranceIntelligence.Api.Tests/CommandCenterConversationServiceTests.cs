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
        Assert.Equal("current", detail.GroundingStatus);
        Assert.True(detail.CanContinue);
        Assert.Equal("Show SLA breaches.", Assert.Single(detail.Turns).Question);
        Assert.Contains(harness.AuditRepositoryEvents(), item => item.ActionName == "conversation.viewed");
    }

    [Fact]
    public async Task ReopenReportsChangedEvidenceWithoutMakingHistoricalAnswerAuthoritative()
    {
        var grounding = new StubGroundingService();
        var harness = new Harness("owner-1", grounding: grounding);
        var response = await harness.Service.QueryAsync(new CommandCenterQueryRequest("Show claims needing attention."), "corr-create");
        grounding.Status = "changed";

        var detail = await harness.Service.GetAsync(Assert.IsType<Guid>(response.ConversationId), "corr-reopen");

        Assert.Equal("changed", detail.Grounding.Status);
        Assert.Contains("changed", detail.Grounding.SafeChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(detail.CanContinue);
    }

    [Fact]
    public async Task RetryWithSameClientRequestIdReturnsStoredTurnWithoutDuplicateQuery()
    {
        var queryService = new StubQueryService();
        var harness = new Harness("owner-1", queryService: queryService);
        var requestId = Guid.NewGuid();
        var request = new CommandCenterQueryRequest("Show SLA breaches.", ClientRequestId: requestId);

        var first = await harness.Service.QueryAsync(request, "corr-first");
        var reconciled = await harness.Service.QueryAsync(request, "corr-retry");

        Assert.Equal(first.TurnId, reconciled.TurnId);
        Assert.Equal(1, queryService.CallCount);
        Assert.Single((await harness.Service.GetAsync(Assert.IsType<Guid>(first.ConversationId), "corr-get")).Turns);
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

        public Harness(
            string userId,
            InMemoryCommandCenterConversationRepository? repository = null,
            TenantScope? scope = null,
            StubGroundingService? grounding = null,
            StubQueryService? queryService = null)
        {
            var context = new TenantContextAccessor();
            context.SetCurrent(new TenantContext((scope ?? Scope).TenantId, (scope ?? Scope).WorkspaceId, userId, ["ClaimsSupervisor"]));
            var permissions = new PermissionService(context, new LocalTestPermissionReadRepository());
            Service = new CommandCenterConversationService(
                context,
                permissions,
                queryService ?? new StubQueryService(),
                repository ?? new InMemoryCommandCenterConversationRepository(),
                auditRepository,
                grounding ?? new StubGroundingService());
        }

        public ICommandCenterConversationService Service { get; }

        public IReadOnlyCollection<InsuranceIntelligence.Domain.Audit.AuditEvent> AuditRepositoryEvents() =>
            auditRepository.ListAsync(Scope).GetAwaiter().GetResult();
    }

    private sealed class StubQueryService : ICommandCenterQueryService
    {
        public int CallCount { get; private set; }

        public Task<CommandCenterQueryResponse> QueryAsync(CommandCenterQueryRequest request, string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse(request, correlationId));

        private CommandCenterQueryResponse CreateResponse(CommandCenterQueryRequest request, string correlationId)
        {
            CallCount++;
            return new CommandCenterQueryResponse(
                [new TextBlock("Current workspace data checked.")],
                Guid.NewGuid(),
                correlationId,
                "Deterministic",
                "command-center-query-v1",
                "Internal",
                CanTriggerDecision: false,
                DateTimeOffset.UtcNow);
        }
    }

    private sealed class StubGroundingService : IConversationGroundingService
    {
        public string Status { get; set; } = "current";

        public Task<(ConversationFocusReference? Focus, ConversationEvidenceSnapshot Snapshot)> CaptureAsync(
            CommandCenterQueryResponse response,
            TenantScope scope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(ConversationFocusReference?, ConversationEvidenceSnapshot)>((
                new ConversationFocusReference("claim", Guid.Empty.ToString(), "Claim test", "/app/claims/test"),
                new ConversationEvidenceSnapshot(response.DataAsOf, response.SnapshotId)));

        public Task<ConversationGroundingSummary> CompareAsync(CommandCenterConversation conversation, CancellationToken cancellationToken = default)
        {
            var historical = conversation.EvidenceSnapshot ?? new ConversationEvidenceSnapshot(conversation.LastActivityAt, "snapshot-test");
            return Task.FromResult(new ConversationGroundingSummary(
                historical.Timestamp,
                historical.Version,
                DateTimeOffset.UtcNow,
                Status == "changed" ? "snapshot-changed" : historical.Version,
                Status,
                Status == "changed" ? "Claim evidence changed after this answer was saved." : "No recorded changes."));
        }
    }
}
