using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class CaseIntakeServiceTests
{
    [Fact]
    public async Task CreateCaseAsync_DefaultsStatusAndWritesAudit()
    {
        var caseRepository = new RecordingCaseRepository();
        var auditRepository = new RecordingAuditRepository();
        var service = new CaseIntakeService(caseRepository, auditRepository);

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Equal(CaseStatus.New, claimCase.Status);
        Assert.Equal(0m, claimCase.CompletenessScore);
        Assert.Equal("tenant-demo", claimCase.Scope.TenantId);
        Assert.Equal("workspace-claims", claimCase.Scope.WorkspaceId);
        Assert.Equal(ClaimRoutingPolicy.ClaimsIntakeTeam, claimCase.AssignedTeam);
        var auditEvent = Assert.Single(auditRepository.Events);
        Assert.Equal("case.created", auditEvent.ActionName);
        Assert.Equal(claimCase.CaseId, auditEvent.CaseId);
        Assert.Equal("New", auditEvent.AfterStatus);
        Assert.Equal("corr-001", auditEvent.CorrelationId);
    }

    [Fact]
    public async Task CreateCaseAsync_InjuryClaim_StartsOnDesignatedEscalationRoute()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());
        var command = ValidCommand() with
        {
            ClaimType = "Bodily injury",
            MotorFnolDetails = new MotorFnolDetails(null, null, null, null, null, null, "Yes", null, null, null)
        };

        var claimCase = await service.CreateCaseAsync(command);

        Assert.Equal(CaseStatus.Escalated, claimCase.Status);
        Assert.Equal(ClaimRoutingPolicy.DesignatedInjuryEscalationTeam, claimCase.AssignedTeam);
        Assert.Equal(CasePriority.Urgent, claimCase.Priority);
    }

    [Fact]
    public async Task CreateCaseAsync_TheftClaim_RoutesToTotalLossDesk()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());
        var command = ValidCommand() with { ClaimType = "Theft" };

        var claimCase = await service.CreateCaseAsync(command);

        Assert.Equal(CaseStatus.New, claimCase.Status);
        Assert.Equal(ClaimRoutingPolicy.TotalLossDeskTeam, claimCase.AssignedTeam);
        Assert.Equal(CasePriority.High, claimCase.Priority);
    }

    [Fact]
    public async Task CreateCaseAsync_RejectsMissingTenantScope()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());
        var command = ValidCommand() with { Scope = new TenantScope("tenant-demo", "") };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCaseAsync(command));

        Assert.Equal("workspaceId", ex.ParamName);
    }

    [Fact]
    public async Task CreateCaseAsync_MedicalClaim_PersistsMedicalFnolDetails_AndDoesNotForceEscalate()
    {
        // WO-15: proves the second product line end to end through the same intake path Motor
        // uses — including that it no longer force-escalates on the word "medical" (the bug fixed
        // alongside this work order).
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());
        var medicalDetails = new MedicalFnolDetails(
            "Amina Okello", "Self", "2026-05-20", "Nakasero Hospital", "Outpatient",
            "Dr. Kintu", "Malaria treatment", "Phone", null);
        var command = ValidCommand() with
        {
            ProductLine = "Medical",
            ClaimType = "Medical reimbursement",
            MotorFnolDetails = null,
            MedicalFnolDetails = medicalDetails,
        };

        var claimCase = await service.CreateCaseAsync(command);

        Assert.Equal("Medical", claimCase.ProductLine);
        Assert.NotNull(claimCase.MedicalFnolDetails);
        Assert.Equal("Amina Okello", claimCase.MedicalFnolDetails!.PatientName);
        Assert.Equal("Nakasero Hospital", claimCase.MedicalFnolDetails.ProviderName);
        Assert.Null(claimCase.MotorFnolDetails);
        Assert.Equal(CaseStatus.New, claimCase.Status);
        Assert.Equal(CasePriority.Normal, claimCase.Priority);
        Assert.Equal(ClaimRoutingPolicy.ClaimsIntakeTeam, claimCase.AssignedTeam);
    }

    [Fact]
    public async Task GetCaseAsync_ReturnsNullForWrongWorkspace()
    {
        var caseRepository = new RecordingCaseRepository();
        var auditRepository = new RecordingAuditRepository();
        var service = new CaseIntakeService(caseRepository, auditRepository);
        var claimCase = await service.CreateCaseAsync(ValidCommand());

        var result = await service.GetCaseAsync(
            claimCase.CaseId,
            TenantScope.Create("tenant-demo", "other-workspace"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ConfirmCaseAsync_StartsSlaClockAndClearsFlag()
    {
        var caseRepository = new RecordingCaseRepository();
        var auditRepository = new RecordingAuditRepository();
        var service = new CaseIntakeService(caseRepository, auditRepository);
        var claimCase = await service.CreateCaseAsync(ValidCommand() with { RequiresConfirmation = true });
        Assert.True(claimCase.RequiresConfirmation);
        Assert.Null(claimCase.SlaDueAt);

        var result = await service.ConfirmCaseAsync(
            claimCase.CaseId,
            claimCase.Scope,
            new ConfirmCaseCorrections());

        Assert.True(result.Found);
        Assert.False(result.WasAlreadyConfirmed);
        Assert.False(result.After!.RequiresConfirmation);
        Assert.NotNull(result.After.SlaDueAt);
        var stored = await caseRepository.GetAsync(claimCase.CaseId);
        Assert.False(stored!.RequiresConfirmation);
    }

    [Fact]
    public async Task ConfirmCaseAsync_AppliesCorrections()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());
        var claimCase = await service.CreateCaseAsync(ValidCommand() with
        {
            ClaimType = "WhatsApp Intake",
            IncidentDescription = "Reported via WhatsApp — no description provided; documents attached for review.",
            RequiresConfirmation = true
        });

        var result = await service.ConfirmCaseAsync(
            claimCase.CaseId,
            claimCase.Scope,
            new ConfirmCaseCorrections(ClaimType: "Accident", IncidentDescription: "Rear-ended at a roundabout."));

        Assert.Equal("Accident", result.After!.ClaimType);
        Assert.Equal("Rear-ended at a roundabout.", result.After.IncidentDescription);
    }

    [Fact]
    public async Task ConfirmCaseAsync_AlreadyConfirmed_IsIdempotentAndDoesNotResetSla()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());
        var claimCase = await service.CreateCaseAsync(ValidCommand());
        Assert.False(claimCase.RequiresConfirmation);
        var originalSlaDueAt = claimCase.SlaDueAt;

        var result = await service.ConfirmCaseAsync(claimCase.CaseId, claimCase.Scope, new ConfirmCaseCorrections());

        Assert.True(result.Found);
        Assert.True(result.WasAlreadyConfirmed);
        Assert.Equal(originalSlaDueAt, result.After!.SlaDueAt);
    }

    [Fact]
    public async Task ConfirmCaseAsync_UnknownCase_ReturnsNotFound()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());

        var result = await service.ConfirmCaseAsync(
            Guid.NewGuid(),
            TenantScope.Create("tenant-demo", "workspace-claims"),
            new ConfirmCaseCorrections());

        Assert.False(result.Found);
    }

    private static CreateCaseCommand ValidCommand() =>
        new(
            TenantScope.Create("tenant-demo", "workspace-claims"),
            "Amina Okello",
            "amina@example.test",
            "+256700000000",
            "POL-1001",
            "Motor",
            "Accident",
            "WebPortal",
            new DateOnly(2026, 5, 24),
            "Minor collision reported at branch intake.",
            "user-claims-1",
            "corr-001");

    private sealed class RecordingCaseRepository : ICaseRepository
    {
        private readonly Dictionary<Guid, ClaimCase> cases = [];

        public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default)
        {
            cases[claimCase.CaseId] = claimCase;
            return Task.CompletedTask;
        }

        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default)
        {
            cases.TryGetValue(caseId, out var claimCase);
            return Task.FromResult(claimCase);
        }

        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ClaimCase>>(cases.Values.Where(claimCase => claimCase.Scope.Matches(scope)).ToArray());
    }

    private sealed class RecordingAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(x => x.CaseId == caseId).ToArray());

        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(x => x.Scope.Matches(scope)).ToArray());
    }
}
