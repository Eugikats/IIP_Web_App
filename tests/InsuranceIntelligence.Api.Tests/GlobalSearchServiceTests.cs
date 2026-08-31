using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Search;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class GlobalSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_OmitsMatchingCaseOutsideAssignedClaimAccess()
    {
        var scope = TenantScope.Create("tenant-search", "workspace-claims");
        var caseRepository = new InMemoryCaseRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var taskRepository = new InMemoryWorkflowTaskRepository();
        var approvalRepository = new InMemoryApprovalRepository();
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            scope.TenantId,
            scope.WorkspaceId,
            "claims-user-1",
            [PlatformRole.ClaimsOfficer.ToString()]));

        var visibleCase = Case(scope, "POL-SEARCH-001", "claims-user-1");
        var restrictedCase = Case(scope, "POL-SEARCH-002", "another-user");
        var otherTenantCase = Case(
            TenantScope.Create("tenant-other", "workspace-claims"),
            "POL-SEARCH-003",
            "claims-user-1");
        await caseRepository.SaveAsync(visibleCase);
        await caseRepository.SaveAsync(restrictedCase);
        await caseRepository.SaveAsync(otherTenantCase);

        var auditWriter = new StubAuditWriter();
        var service = new GlobalSearchService(
            caseRepository,
            documentRepository,
            taskRepository,
            approvalRepository,
            tenantContext,
            new StubPermissionService(Permission.ViewAssignedClaims),
            auditWriter);

        var response = await service.SearchAsync("POL-SEARCH", 20, CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.Equal(GlobalSearchResultType.Case, result.Type);
        Assert.Equal(visibleCase.CaseId.ToString(), result.Id);
        Assert.Equal(1, response.ReturnedCount);
        Assert.False(response.HasMore);
        Assert.DoesNotContain("POL-SEARCH-002", response.Results.Select(item => item.Subtitle));
        Assert.DoesNotContain("POL-SEARCH-003", response.Results.Select(item => item.Subtitle));
        Assert.Equal("global-search.executed", auditWriter.LastEvent?.ActionName);
        Assert.DoesNotContain("POL-SEARCH", auditWriter.LastEvent?.AuditReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_IncludesOnlyPermittedCaseRelatedObjectTypes()
    {
        var scope = TenantScope.Create("tenant-search", "workspace-claims");
        var caseRepository = new InMemoryCaseRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var taskRepository = new InMemoryWorkflowTaskRepository();
        var approvalRepository = new InMemoryApprovalRepository();
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            scope.TenantId,
            scope.WorkspaceId,
            "claims-user-1",
            [PlatformRole.ClaimsOfficer.ToString()]));
        var claimCase = Case(scope, "POL-SEARCH-003", "claims-user-1");
        await caseRepository.SaveAsync(claimCase);
        await documentRepository.SaveAsync(Document(scope, claimCase.CaseId, "SEARCH-estimate.pdf"));
        await taskRepository.SaveAsync(WorkflowTaskItem(scope, claimCase.CaseId, "SEARCH document review"));
        await approvalRepository.SaveAsync(Approval(scope, claimCase.CaseId, "SEARCH settlement review"));

        var withoutApprovalAuthority = new GlobalSearchService(
            caseRepository,
            documentRepository,
            taskRepository,
            approvalRepository,
            tenantContext,
            new StubPermissionService(Permission.ViewAssignedClaims),
            new StubAuditWriter());

        var response = await withoutApprovalAuthority.SearchAsync("SEARCH", 20, CancellationToken.None);

        Assert.Contains(response.Results, result => result.Type == GlobalSearchResultType.Case);
        Assert.Contains(response.Results, result => result.Type == GlobalSearchResultType.Document);
        Assert.Contains(response.Results, result => result.Type == GlobalSearchResultType.WorkflowTask);
        Assert.DoesNotContain(response.Results, result => result.Type == GlobalSearchResultType.Approval);

        var withApprovalAuthority = new GlobalSearchService(
            caseRepository,
            documentRepository,
            taskRepository,
            approvalRepository,
            tenantContext,
            new StubPermissionService(Permission.ViewAssignedClaims, Permission.ApproveSettlementWorkflowStep),
            new StubAuditWriter());

        var approvalResponse = await withApprovalAuthority.SearchAsync("SEARCH", 20, CancellationToken.None);

        Assert.Contains(approvalResponse.Results, result => result.Type == GlobalSearchResultType.Approval);
    }

    [Fact]
    public async Task SearchAsync_RanksExactReferenceBeforeMoreUrgentPartialMatch()
    {
        var scope = TenantScope.Create("tenant-search", "workspace-claims");
        var caseRepository = new InMemoryCaseRepository();
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            scope.TenantId,
            scope.WorkspaceId,
            "claims-user-1",
            [PlatformRole.ClaimsSupervisor.ToString()]));
        var exact = Case(scope, "POL-EXACT", "claims-user-1") with { Priority = CasePriority.Normal };
        var urgentPartial = Case(scope, "POL-EXACT-URGENT", "claims-user-1") with { Priority = CasePriority.Urgent };
        await caseRepository.SaveAsync(exact);
        await caseRepository.SaveAsync(urgentPartial);

        var service = new GlobalSearchService(
            caseRepository,
            new InMemoryCaseDocumentRepository(),
            new InMemoryWorkflowTaskRepository(),
            new InMemoryApprovalRepository(),
            tenantContext,
            new StubPermissionService(Permission.ViewAllClaims),
            new StubAuditWriter());

        var response = await service.SearchAsync("POL-EXACT", 20, CancellationToken.None);

        Assert.Equal(exact.CaseId.ToString(), response.Results[0].Id);
        Assert.Equal(urgentPartial.CaseId.ToString(), response.Results[1].Id);
    }

    private static ClaimCase Case(TenantScope scope, string policyNumber, string assignedUserId) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Search Test Customer",
            PolicyNumber = policyNumber,
            ProductLine = "Motor",
            ClaimType = "Own damage",
            Channel = "Portal",
            IncidentDate = new DateOnly(2026, 8, 1),
            IncidentDescription = "Test incident",
            Status = CaseStatus.InReview,
            CompletenessScore = 80,
            AssignedUserId = assignedUserId,
            AssignedTeam = "Motor Claims",
            CreatedByUserId = assignedUserId,
            CreatedAt = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static CaseDocument Document(TenantScope scope, Guid caseId, string fileName)
    {
        var now = new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero);
        return new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = caseId,
            Scope = scope,
            OriginalFileName = fileName,
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            ObjectKey = $"test/{Guid.NewGuid():N}",
            DocumentType = DocumentType.GarageEstimate,
            ProcessingStatus = DocumentProcessingStatus.ReviewRequired,
            Classification = new DocumentClassificationResult(DocumentType.GarageEstimate, 0.9m, "test", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "test",
                now,
                false,
                null,
                null),
            ValidationWarnings = [],
            UploadedByUserId = "claims-user-1",
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
    }

    private static WorkflowTask WorkflowTaskItem(TenantScope scope, Guid caseId, string title) =>
        new()
        {
            TaskId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            TaskType = WorkflowTaskType.ReviewCase,
            Status = WorkflowTaskStatus.Open,
            Title = title,
            Summary = "Sensitive task detail excluded from global search.",
            AssignedUserId = "claims-user-1",
            CreatedByUserId = "claims-user-1",
            CreatedAt = new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static ApprovalRequest Approval(TenantScope scope, Guid caseId, string approvalType) =>
        new()
        {
            ApprovalId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ApprovalType = approvalType,
            Status = ApprovalStatus.Pending,
            RiskLevel = "High",
            EvidenceSummary = "Sensitive approval evidence excluded from global search.",
            SourceReferences = [],
            RequestedByUserId = "claims-user-1",
            CreatedAt = new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private sealed class StubPermissionService(params Permission[] permissions) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissions.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            HasPermissionAsync(permission, cancellationToken).ContinueWith(
                task =>
                {
                    if (!task.Result)
                    {
                        throw new PermissionDeniedException(permission);
                    }
                },
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private sealed class StubAuditWriter : IAuditWriter
    {
        public AuditEvent? LastEvent { get; private set; }

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            LastEvent = auditEvent;
            return Task.CompletedTask;
        }
    }
}
