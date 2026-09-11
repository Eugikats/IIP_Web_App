using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Api.Tests;

public sealed class RetentionEnforcementServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-retention", "workspace-claims");

    [Fact]
    public async Task EnforceAsync_DeletesTurnsOlderThanTheConfiguredWindow_KeepsNewerOnes()
    {
        var now = DateTimeOffset.UtcNow;
        var conversations = new FakeConversationRepository();
        await conversations.SaveAsync(Turn(now.AddDays(-400))); // older than 365-day default -> expired
        await conversations.SaveAsync(Turn(now.AddDays(-10)));  // recent -> kept
        var policies = new FakePolicyRepository();
        var audit = new FakeAuditRepository();
        var service = new RetentionEnforcementService(policies, conversations, audit);

        var results = await service.EnforceAsync(Scope, "corr-retention-1");

        var result = Assert.Single(results);
        Assert.Equal(RetentionDataClass.CustomerConversationTurns, result.DataClass);
        Assert.Equal(1, result.RecordsDeleted);
        Assert.Equal(365, result.RetentionDaysApplied); // built-in default, nothing configured
        Assert.Single(conversations.Saved); // only the recent turn survives
    }

    [Fact]
    public async Task EnforceAsync_UsesConfiguredRetentionDaysOverTheDefault()
    {
        var now = DateTimeOffset.UtcNow;
        var conversations = new FakeConversationRepository();
        await conversations.SaveAsync(Turn(now.AddDays(-40))); // within default 365 days, but outside a configured 30-day window
        var policies = new FakePolicyRepository();
        policies.Saved[RetentionDataClass.CustomerConversationTurns] = new RetentionPolicy
        {
            Scope = Scope, DataClass = RetentionDataClass.CustomerConversationTurns, RetentionDays = 30,
            UpdatedByUserId = "compliance-officer-1", UpdatedAt = now,
        };
        var audit = new FakeAuditRepository();
        var service = new RetentionEnforcementService(policies, conversations, audit);

        var results = await service.EnforceAsync(Scope, "corr-retention-2");

        var result = Assert.Single(results);
        Assert.Equal(30, result.RetentionDaysApplied);
        Assert.Equal(1, result.RecordsDeleted);
        Assert.Empty(conversations.Saved);
    }

    [Fact]
    public async Task EnforceAsync_NothingExpired_StillWritesAnHonestQuietAuditEvent()
    {
        var conversations = new FakeConversationRepository();
        await conversations.SaveAsync(Turn(DateTimeOffset.UtcNow.AddDays(-1)));
        var policies = new FakePolicyRepository();
        var audit = new FakeAuditRepository();
        var service = new RetentionEnforcementService(policies, conversations, audit);

        var results = await service.EnforceAsync(Scope, "corr-retention-3");

        Assert.Equal(0, Assert.Single(results).RecordsDeleted);
        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal("retention.enforced", auditEvent.ActionName);
        Assert.Equal("no_expired_records", auditEvent.ValidationResult);
        Assert.Equal("system:retention-enforcement", auditEvent.UserId);
        Assert.Equal(ActorType.Integration, auditEvent.ActorType);
    }

    [Fact]
    public async Task EnforceAsync_DeletionWritesAnAuditEventWithTheOutcome()
    {
        var conversations = new FakeConversationRepository();
        await conversations.SaveAsync(Turn(DateTimeOffset.UtcNow.AddDays(-400)));
        var audit = new FakeAuditRepository();
        var service = new RetentionEnforcementService(new FakePolicyRepository(), conversations, audit);

        await service.EnforceAsync(Scope, "corr-retention-4");

        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal("deleted", auditEvent.ValidationResult);
        Assert.Contains("1 record(s) deleted", auditEvent.AuditReason);
    }

    private static CustomerConversationTurn Turn(DateTimeOffset occurredAt) => new()
    {
        TurnId = Guid.NewGuid(),
        Scope = Scope,
        ThreadId = "thread-1",
        ContactWaId = "256700111222",
        InboundMessageText = "What does my policy cover?",
        ClassifiedIntent = CustomerIntent.GeneralQuestion,
        Outcome = CustomerAssistantOutcome.QueuedForHumanApproval,
        OccurredAt = occurredAt,
        CorrelationId = "corr-seed",
    };

    private sealed class FakeConversationRepository : ICustomerConversationRepository
    {
        public List<CustomerConversationTurn> Saved { get; } = [];
        public Task SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken = default)
        {
            Saved.Add(turn);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CustomerConversationTurn>>(Saved.Where(t => t.ThreadId == threadId).ToArray());
        public Task<RetentionDeletionSummary> DeleteOlderThanAsync(TenantScope scope, DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            var expired = Saved.Where(t => t.Scope.Matches(scope) && t.OccurredAt < cutoff).ToArray();
            foreach (var turn in expired) Saved.Remove(turn);
            return Task.FromResult(expired.Length == 0
                ? RetentionDeletionSummary.None
                : new RetentionDeletionSummary(expired.Length, expired.Min(t => t.OccurredAt), expired.Max(t => t.OccurredAt)));
        }
    }

    private sealed class FakePolicyRepository : IRetentionPolicyRepository
    {
        public Dictionary<RetentionDataClass, RetentionPolicy> Saved { get; } = [];
        public Task<RetentionPolicy?> GetAsync(TenantScope scope, RetentionDataClass dataClass, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.TryGetValue(dataClass, out var policy) && policy.Scope.Matches(scope) ? policy : null);
        public Task<IReadOnlyCollection<RetentionPolicy>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<RetentionPolicy>>(Saved.Values.Where(p => p.Scope.Matches(scope)).ToArray());
        public Task SaveAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
        {
            Saved[policy.DataClass] = policy;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.CaseId == caseId).ToArray());
        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.Scope.Matches(scope)).ToArray());
    }
}
