using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Fraud;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Fraud;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

// WO-19: proves the fraud signal disposition workflow — permission-gated, actor-recorded on every
// transition (I7), and only movable in the documented order.
public sealed class FraudDispositionServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task FullLifecycle_TriageThroughClose_RecordsRealActorOnEveryTransition()
    {
        var audit = new InMemoryAuditRepository();
        var repository = new InMemoryFraudSignalDispositionRepository();
        var service = CreateService("supervisor-1", "ClaimsSupervisor", repository, audit);
        var caseId = Guid.NewGuid();

        var triaged = await service.TriageAsync("R-VELOCITY", [caseId], "Reviewed the velocity signal; investigating.", "corr-1");
        Assert.Equal("Triaged", triaged.Status);
        Assert.Contains(triaged.AllowedActions, a => a.Action == "assignFraudSignal");

        var assigned = await service.AssignAsync(SignalId("R-VELOCITY", caseId), "investigator-1", "Assigning to SIU liaison.", "corr-2");
        Assert.Equal("UnderInvestigation", assigned.Status);
        Assert.Equal("investigator-1", assigned.AssignedToUserId);

        var outcome = await service.RecordOutcomeAsync(
            SignalId("R-VELOCITY", caseId), FraudDispositionOutcome.NoFurtherActionRequired, "Confirmed distinct genuine incidents.",
            "Investigation complete, no concern substantiated.", "corr-3");
        Assert.Equal("OutcomeRecorded", outcome.Status);
        Assert.Equal("NoFurtherActionRequired", outcome.Outcome);

        var closed = await service.CloseAsync(SignalId("R-VELOCITY", caseId), "Closing after recorded outcome.", "corr-4");
        Assert.Equal("Closed", closed.Status);

        var auditEvents = await audit.ListAsync(Scope);
        Assert.Contains(auditEvents, e => e.ActionName == "fraud_signal.triaged" && e.UserId == "supervisor-1");
        Assert.Contains(auditEvents, e => e.ActionName == "fraud_signal.assigned" && e.UserId == "supervisor-1" && e.AuditReason == "Assigning to SIU liaison.");
        Assert.Contains(auditEvents, e => e.ActionName == "fraud_signal.outcome_recorded" && e.AfterStatus == "OutcomeRecorded");
        Assert.Contains(auditEvents, e => e.ActionName == "fraud_signal.closed" && e.AfterStatus == "Closed");
    }

    [Fact]
    public async Task Triage_RequiresViewFraudSignalsPermission()
    {
        var service = CreateService("officer-1", "ClaimsOfficer", new InMemoryFraudSignalDispositionRepository(), new InMemoryAuditRepository());

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            service.TriageAsync("R-VELOCITY", [Guid.NewGuid()], "Attempted triage without permission.", "corr-1"));
    }

    [Fact]
    public async Task Triage_CannotBeRepeated_ForTheSameSignal()
    {
        var repository = new InMemoryFraudSignalDispositionRepository();
        var service = CreateService("supervisor-1", "ClaimsSupervisor", repository, new InMemoryAuditRepository());
        var caseId = Guid.NewGuid();
        await service.TriageAsync("R-VELOCITY", [caseId], "First triage.", "corr-1");

        var exception = await Assert.ThrowsAsync<FraudDispositionConflictException>(() =>
            service.TriageAsync("R-VELOCITY", [caseId], "Second triage attempt.", "corr-2"));

        Assert.Equal("triageFraudSignal", exception.BlockedAction);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("outcome")]
    [InlineData("close")]
    public async Task Transitions_CannotSkipAheadOfTheUntriagedState(string action)
    {
        var repository = new InMemoryFraudSignalDispositionRepository();
        var service = CreateService("supervisor-1", "ClaimsSupervisor", repository, new InMemoryAuditRepository());
        var signalId = SignalId("R-VELOCITY", Guid.NewGuid());

        Task act() => action switch
        {
            "assign" => service.AssignAsync(signalId, "investigator-1", "Skip ahead attempt.", "corr-1"),
            "outcome" => service.RecordOutcomeAsync(signalId, FraudDispositionOutcome.NoFurtherActionRequired, null, "Skip ahead attempt.", "corr-1"),
            _ => service.CloseAsync(signalId, "Skip ahead attempt.", "corr-1"),
        };

        await Assert.ThrowsAsync<FraudSignalNotFoundException>(act);
    }

    [Fact]
    public async Task RecordOutcome_CannotHappenBeforeAssignment()
    {
        var repository = new InMemoryFraudSignalDispositionRepository();
        var service = CreateService("supervisor-1", "ClaimsSupervisor", repository, new InMemoryAuditRepository());
        var caseId = Guid.NewGuid();
        await service.TriageAsync("R-VELOCITY", [caseId], "Triaged only.", "corr-1");

        var exception = await Assert.ThrowsAsync<FraudDispositionConflictException>(() =>
            service.RecordOutcomeAsync(SignalId("R-VELOCITY", caseId), FraudDispositionOutcome.NoFurtherActionRequired, null, "Too early.", "corr-2"));

        Assert.Equal("recordFraudDispositionOutcome", exception.BlockedAction);
    }

    [Fact]
    public async Task Close_CannotHappenBeforeAnOutcomeIsRecorded()
    {
        var repository = new InMemoryFraudSignalDispositionRepository();
        var service = CreateService("supervisor-1", "ClaimsSupervisor", repository, new InMemoryAuditRepository());
        var caseId = Guid.NewGuid();
        await service.TriageAsync("R-VELOCITY", [caseId], "Triaged.", "corr-1");
        await service.AssignAsync(SignalId("R-VELOCITY", caseId), "investigator-1", "Assigned.", "corr-2");

        var exception = await Assert.ThrowsAsync<FraudDispositionConflictException>(() =>
            service.CloseAsync(SignalId("R-VELOCITY", caseId), "Too early.", "corr-3"));

        Assert.Equal("closeFraudSignal", exception.BlockedAction);
    }

    [Fact]
    public async Task Dispositions_AreIsolatedByTenant()
    {
        var repository = new InMemoryFraudSignalDispositionRepository();
        var service = CreateService("supervisor-1", "ClaimsSupervisor", repository, new InMemoryAuditRepository());
        var caseId = Guid.NewGuid();
        await service.TriageAsync("R-VELOCITY", [caseId], "Triaged in tenant-demo.", "corr-1");

        var otherTenantResult = await repository.GetAsync(SignalId("R-VELOCITY", caseId), TenantScope.Create("tenant-other", "workspace-other"));

        Assert.Null(otherTenantResult);
    }

    [Fact]
    public void ClientPortalClaimResponse_NeverExposesFraudSignals()
    {
        // WO-19: fraud signals are restricted content — never exposed on policyholder-facing
        // surfaces. Reflective guardrail: fails the moment anyone adds a fraud-shaped field to the
        // client-portal response, forcing that choice to be reconsidered rather than slipping in
        // via a generic "include everything" mapper.
        var properties = typeof(InsuranceIntelligence.Api.ClientPortal.ClientPortalClaimResponse)
            .GetProperties()
            .Select(p => p.Name);

        Assert.All(properties, name =>
            Assert.DoesNotContain("fraud", name, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid SignalId(string ruleId, params Guid[] relatedCaseIds) =>
        FraudSignalDisposition.DeterministicId(ruleId, relatedCaseIds);

    private static FraudDispositionService CreateService(
        string userId, string roleCode, InMemoryFraudSignalDispositionRepository repository, InMemoryAuditRepository audit)
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, userId, [roleCode]));
        var permissionService = new PermissionService(tenantContext, new LocalTestPermissionReadRepository());
        return new FraudDispositionService(tenantContext, permissionService, repository, audit);
    }
}
