using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class CaseIntakeServiceTests
{
    [Fact]
    public async Task CreateCaseAsync_WithNoProductRepository_LeavesProductPinNull()
    {
        // The optional-dependency default: every existing caller that never wired a product
        // repository keeps behaving exactly as before, the claim simply carries no pin.
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Null(claimCase.ProductId);
        Assert.Null(claimCase.ProductVersion);
    }

    [Fact]
    public async Task CreateCaseAsync_ResolvesAndPinsThePublishedProductMatchingProductLine()
    {
        var products = new RecordingProductRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var product = PublishedProduct(scope, "Motor", version: 3);
        await products.SaveAsync(product);
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository(), products);

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Equal(product.ProductId, claimCase.ProductId);
        Assert.Equal(3, claimCase.ProductVersion);
    }

    [Fact]
    public async Task CreateCaseAsync_MatchesByProductClass_NotByTheProductsDisplayName()
    {
        // The real bug this guards against: the demo seed (and any admin who names a product
        // something other than the bare line, e.g. "Motor Comprehensive" or "Motor Cover") set
        // ProductClass to the line ("Motor") but Name to a display name. Matching on Name meant a
        // freshly filed claim never pinned to a real product even though one existed and was
        // published for that exact line, confirmed live in dev before this fix.
        var products = new RecordingProductRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var product = PublishedProduct(scope, "Motor", version: 1) with { Name = "Motor Cover" };
        await products.SaveAsync(product);
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository(), products);

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Equal(product.ProductId, claimCase.ProductId);
        Assert.Equal(1, claimCase.ProductVersion);
    }

    [Fact]
    public async Task CreateCaseAsync_IgnoresADraftProductWithTheSameName()
    {
        var products = new RecordingProductRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await products.SaveAsync(PublishedProduct(scope, "Motor", version: 1) with { Status = ProductStatus.Draft });
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository(), products);

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Null(claimCase.ProductId);
        Assert.Null(claimCase.ProductVersion);
    }

    [Fact]
    public async Task AnOpenClaim_KeepsItsProductVersion_AfterTheProductIsLaterRepublished()
    {
        // Outcome C's acceptance test: changing a product after a claim was filed against it must
        // never rewrite the version that claim is running under.
        var products = new RecordingProductRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var v1 = PublishedProduct(scope, "Motor", version: 1);
        await products.SaveAsync(v1);
        var caseRepository = new RecordingCaseRepository();
        var service = new CaseIntakeService(caseRepository, new RecordingAuditRepository(), products);

        var claimCase = await service.CreateCaseAsync(ValidCommand());
        Assert.Equal(1, claimCase.ProductVersion);

        // The product is edited and republished as version 2, exactly like ProductCommandService's
        // own versioning would produce (a new, higher-Version row, same ProductId).
        var v2 = v1 with { VersionId = Guid.NewGuid(), Version = 2, Name = "Motor", Status = ProductStatus.Published };
        await products.SaveAsync(v2);
        var currentProduct = await products.GetCurrentAsync(scope, v1.ProductId);

        var storedClaim = await caseRepository.GetAsync(claimCase.CaseId);

        Assert.Equal(2, currentProduct!.Version);
        Assert.Equal(1, storedClaim!.ProductVersion); // the open claim did not move
        Assert.Equal(v1.ProductId, storedClaim.ProductId);
    }

    [Fact]
    public async Task CreateCaseAsync_WithNoPolicyRepository_LeavesAdvisoryRiskSignalsEmpty()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Empty(claimCase.AdvisoryRiskSignals);
    }

    [Fact]
    public async Task CreateCaseAsync_PolicyLapsedBeforeIncident_RecordsAnAdvisorySignal()
    {
        var policies = new RecordingPolicyRepository();
        policies.Seed(new Policy(
            "POL-1001", "tenant-demo", "workspace-claims", "POL-1001", "Motor", "Comprehensive",
            null, null, new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1), null, null, 5_000_000m));
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository(), policyRepository: policies);

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Contains(PolicyRiskSignalEvaluator.PolicyNotInForceSignal, claimCase.AdvisoryRiskSignals);
    }

    [Fact]
    public async Task CreateCaseAsync_ClaimedAmountAboveSumInsured_RecordsAnAdvisorySignal()
    {
        var policies = new RecordingPolicyRepository();
        policies.Seed(new Policy(
            "POL-1001", "tenant-demo", "workspace-claims", "POL-1001", "Motor", "Comprehensive",
            null, null, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), null, null, 2_000_000m));
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository(), policyRepository: policies);

        var claimCase = await service.CreateCaseAsync(ValidCommand() with { ClaimedAmount = 9_000_000m });

        Assert.Contains(PolicyRiskSignalEvaluator.ClaimedAmountExceedsSumInsuredSignal, claimCase.AdvisoryRiskSignals);
    }

    [Fact]
    public async Task CreateCaseAsync_HealthyPolicyAndAmount_RecordsNoAdvisorySignals()
    {
        var policies = new RecordingPolicyRepository();
        policies.Seed(new Policy(
            "POL-1001", "tenant-demo", "workspace-claims", "POL-1001", "Motor", "Comprehensive",
            null, null, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), null, null, 20_000_000m));
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository(), policyRepository: policies);

        var claimCase = await service.CreateCaseAsync(ValidCommand() with { ClaimedAmount = 1_000_000m });

        Assert.Empty(claimCase.AdvisoryRiskSignals);
    }

    private sealed class RecordingPolicyRepository : IPolicyRepository
    {
        private readonly List<Policy> policies = [];

        public void Seed(Policy policy) => policies.Add(policy);

        public Task<Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(policies.FirstOrDefault(p => p.PolicyId == policyId && p.TenantId == scope.TenantId && p.WorkspaceId == scope.WorkspaceId));

        public Task<Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(policies.FirstOrDefault(p =>
                string.Equals(p.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase)
                && p.TenantId == scope.TenantId && p.WorkspaceId == scope.WorkspaceId));

        public Task<IReadOnlyCollection<Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Policy>>(policies.Where(p => p.TenantId == scope.TenantId && p.WorkspaceId == scope.WorkspaceId).ToArray());

        public Task<IReadOnlyCollection<Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Policy>>(policies.Where(p => p.CustomerId == customerId).ToArray());

        public Task UpsertAsync(Policy policy, CancellationToken cancellationToken)
        {
            policies.RemoveAll(p => p.PolicyId == policy.PolicyId);
            policies.Add(policy);
            return Task.CompletedTask;
        }
    }

    private static Product PublishedProduct(TenantScope scope, string name, int version) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = version,
            Name = name,
            ProductClass = name,
            IraApprovalReference = "IRA/2026/0001",
            IraApprovalEffectiveDate = new DateOnly(2026, 1, 1),
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = [],
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Published for test.",
        };

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
        // uses, including that it no longer force-escalates on the word "medical" (the bug fixed
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
            IncidentDescription = "Reported via WhatsApp, no description provided; documents attached for review.",
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

    [Fact]
    public async Task CreateCaseAsync_NoExistingTeamHistory_LeavesCaseTeamOnlyAssigned()
    {
        var service = new CaseIntakeService(new RecordingCaseRepository(), new RecordingAuditRepository());

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Null(claimCase.AssignedUserId);
        Assert.Equal(CaseStatus.New, claimCase.Status); // never bumped to Assigned without a named owner
    }

    [Fact]
    public async Task CreateCaseAsync_ExistingTeamHistory_NamesTheLeastLoadedMember()
    {
        var caseRepository = new RecordingCaseRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await caseRepository.SaveAsync(SeedCase(scope, "officer-busy", ClaimRoutingPolicy.ClaimsIntakeTeam, CaseStatus.InReview));
        await caseRepository.SaveAsync(SeedCase(scope, "officer-busy", ClaimRoutingPolicy.ClaimsIntakeTeam, CaseStatus.InReview));
        await caseRepository.SaveAsync(SeedCase(scope, "officer-quiet", ClaimRoutingPolicy.ClaimsIntakeTeam, CaseStatus.Closed));
        var service = new CaseIntakeService(caseRepository, new RecordingAuditRepository());

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Equal("officer-quiet", claimCase.AssignedUserId); // one open case beats two
        Assert.Equal(CaseStatus.Assigned, claimCase.Status);
    }

    [Fact]
    public async Task CreateCaseAsync_EqualLoad_RotatesToWhoeverWasAssignedLeastRecently()
    {
        var caseRepository = new RecordingCaseRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await caseRepository.SaveAsync(SeedCase(scope, "officer-a", ClaimRoutingPolicy.ClaimsIntakeTeam, CaseStatus.New, DateTimeOffset.UtcNow.AddHours(-1)));
        await caseRepository.SaveAsync(SeedCase(scope, "officer-b", ClaimRoutingPolicy.ClaimsIntakeTeam, CaseStatus.New, DateTimeOffset.UtcNow.AddHours(-3)));
        var service = new CaseIntakeService(caseRepository, new RecordingAuditRepository());

        var claimCase = await service.CreateCaseAsync(ValidCommand());

        Assert.Equal("officer-b", claimCase.AssignedUserId); // least recently touched, at equal load
    }

    [Fact]
    public async Task CreateCaseAsync_OnlyConsidersHistoryFromTheSameRoutedTeam()
    {
        var caseRepository = new RecordingCaseRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await caseRepository.SaveAsync(SeedCase(scope, "fraud-desk-officer", ClaimRoutingPolicy.DesignatedFraudEscalationTeam, CaseStatus.New));
        var service = new CaseIntakeService(caseRepository, new RecordingAuditRepository());

        var claimCase = await service.CreateCaseAsync(ValidCommand()); // routes to ClaimsIntakeTeam, not the fraud desk

        Assert.Null(claimCase.AssignedUserId);
    }

    private static ClaimCase SeedCase(TenantScope scope, string assignedUserId, string team, CaseStatus status, DateTimeOffset? updatedAt = null) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Seed Customer",
            PolicyNumber = "POL-SEED",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 1),
            IncidentDescription = "Seeded for assignment-policy test.",
            Status = status,
            CompletenessScore = 50,
            AssignedUserId = assignedUserId,
            AssignedTeam = team,
            CreatedByUserId = "seed",
            CreatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            CorrelationId = "seed-correlation",
        };

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

    private sealed class RecordingProductRepository : IProductRepository
    {
        private readonly List<Product> versions = [];

        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions
                .Where(v => v.Scope.Matches(scope) && v.ProductId == productId)
                .OrderByDescending(v => v.Version)
                .FirstOrDefault());

        public Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions.FirstOrDefault(v => v.Scope.Matches(scope) && v.ProductId == productId && v.Version == version));

        public Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(versions.Where(v => v.Scope.Matches(scope) && v.ProductId == productId).ToArray());

        public Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(versions
                .Where(v => v.Scope.Matches(scope))
                .GroupBy(v => v.ProductId)
                .Select(group => group.OrderByDescending(v => v.Version).First())
                .ToArray());

        public Task SaveAsync(Product version, CancellationToken cancellationToken = default)
        {
            versions.Add(version);
            return Task.CompletedTask;
        }
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
