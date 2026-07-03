using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

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
        var auditEvent = Assert.Single(auditRepository.Events);
        Assert.Equal("case.created", auditEvent.ActionName);
        Assert.Equal(claimCase.CaseId, auditEvent.CaseId);
        Assert.Equal("New", auditEvent.AfterStatus);
        Assert.Equal("corr-001", auditEvent.CorrelationId);
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
