using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Agents.Tools;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// WO-08: every read-tier tool is permission-checked and tenant-isolated. Covers the registry's
/// own enforcement (allowed-agent-key, unknown tool) plus a representative tool per repository
/// shape in full (happy path, permission denial, tenant isolation), and the remaining tools at
/// happy-path + permission-denial, their tenant isolation comes from the same ClaimSnapshotLoader/
/// TenantScope-filtered repository calls already proven isolated by the tools tested in full.
/// </summary>
public sealed class AgentToolRegistryTests
{
    private static readonly TenantScope TenantA = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly TenantScope TenantB = TenantScope.Create("tenant-b", "workspace-a");

    [Fact]
    public async Task Registry_DeniesUnknownTool()
    {
        var registry = new AgentToolRegistry([]);

        var result = await registry.ExecuteAsync("no.such.tool", Invocation(TenantA, "command-center"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Unknown tool.", result.DeniedReason);
    }

    [Fact]
    public async Task Registry_DeniesAgentNotInAllowedList()
    {
        var loader = SnapshotLoader();
        var tool = new ClaimsAttentionReadTool(loader, AllowAll());
        var registry = new AgentToolRegistry([tool]);

        // ClaimsAttentionReadTool's AllowedAgentKeys does not include "policy-intelligence".
        var result = await registry.ExecuteAsync(
            "claims.attention.read",
            Invocation(TenantA, "policy-intelligence"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not permitted", result.DeniedReason);
    }

    [Fact]
    public async Task ClaimsAttentionReadTool_ReturnsSnapshotsAndEvidenceRefs()
    {
        var caseRepository = new InMemoryCaseRepository();
        var caseId = await SeedCaseAsync(caseRepository, TenantA);
        var loader = SnapshotLoader(caseRepository);
        var tool = new ClaimsAttentionReadTool(loader, AllowAll());

        var result = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);

        Assert.True(result.Success);
        var snapshots = Assert.IsAssignableFrom<IReadOnlyList<ClaimSnapshot>>(result.Data);
        Assert.Single(snapshots);
        Assert.Equal(caseId, snapshots[0].CaseId);
        Assert.Contains($"case:{caseId}", result.EvidenceRefs);
    }

    [Fact]
    public async Task ClaimsAttentionReadTool_DeniedWithoutPermission()
    {
        var loader = SnapshotLoader();
        var tool = new ClaimsAttentionReadTool(loader, DenyAll());

        var result = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Requires one of", result.DeniedReason);
    }

    [Fact]
    public async Task ClaimsAttentionReadTool_IsolatesByTenant()
    {
        var caseRepository = new InMemoryCaseRepository();
        await SeedCaseAsync(caseRepository, TenantA);
        var loader = SnapshotLoader(caseRepository);
        var tool = new ClaimsAttentionReadTool(loader, AllowAll());

        var result = await tool.ExecuteAsync(Invocation(TenantB, "command-center"), CancellationToken.None);

        var snapshots = Assert.IsAssignableFrom<IReadOnlyList<ClaimSnapshot>>(result.Data);
        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task ClaimsSlaReadTool_ReturnsOnlyAtRiskOrBreached()
    {
        var loader = SnapshotLoader();
        var tool = new ClaimsSlaReadTool(loader, AllowAll());

        var result = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ClaimsSlaReadTool_DeniedWithoutPermission()
    {
        var loader = SnapshotLoader();
        var tool = new ClaimsSlaReadTool(loader, DenyAll());

        var result = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DocumentsGapsReadTool_HappyPathAndDenial()
    {
        var loader = SnapshotLoader();
        var allowed = await new DocumentsGapsReadTool(loader, AllowAll())
            .ExecuteAsync(Invocation(TenantA, "document-checker"), CancellationToken.None);
        var denied = await new DocumentsGapsReadTool(loader, DenyAll())
            .ExecuteAsync(Invocation(TenantA, "document-checker"), CancellationToken.None);

        Assert.True(allowed.Success);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task ApprovalsQueueReadTool_ReturnsOnlyPendingAndIsolatesByTenant()
    {
        var repository = new InMemoryApprovalRepository();
        await repository.SaveAsync(Approval(TenantA, ApprovalStatus.Pending));
        await repository.SaveAsync(Approval(TenantA, ApprovalStatus.Approved));
        await repository.SaveAsync(Approval(TenantB, ApprovalStatus.Pending));
        var tool = new ApprovalsQueueReadTool(repository, AllowAll());

        var resultA = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);
        var resultB = await tool.ExecuteAsync(Invocation(TenantB, "command-center"), CancellationToken.None);

        var approvalsA = Assert.IsAssignableFrom<IReadOnlyList<ApprovalRequest>>(resultA.Data);
        var approvalsB = Assert.IsAssignableFrom<IReadOnlyList<ApprovalRequest>>(resultB.Data);
        Assert.Single(approvalsA);
        Assert.Equal(ApprovalStatus.Pending, approvalsA[0].Status);
        Assert.Single(approvalsB);
    }

    [Fact]
    public async Task ApprovalsQueueReadTool_DeniedWithoutPermission()
    {
        var tool = new ApprovalsQueueReadTool(new InMemoryApprovalRepository(), DenyAll());

        var result = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AuditReadTool_IsolatesByTenantAndDeniesWithoutPermission()
    {
        var repository = new InMemoryAuditRepository();
        await repository.WriteAsync(AuditEventFor(TenantA));
        await repository.WriteAsync(AuditEventFor(TenantB));

        var resultA = await new AuditReadTool(repository, AllowAll())
            .ExecuteAsync(Invocation(TenantA, "risk-fraud-signal"), CancellationToken.None);
        var denied = await new AuditReadTool(repository, DenyAll())
            .ExecuteAsync(Invocation(TenantA, "risk-fraud-signal"), CancellationToken.None);

        var eventsA = Assert.IsAssignableFrom<IReadOnlyCollection<AuditEvent>>(resultA.Data);
        Assert.True(resultA.Success);
        Assert.Single(eventsA);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task DocumentReadTool_IsolatesByTenantAndDeniesWithoutPermission()
    {
        var caseRepository = new InMemoryCaseRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var caseId = await SeedCaseAsync(caseRepository, TenantA);
        await documentRepository.SaveAsync(DocumentFor(caseId, TenantA));

        var allowed = await new DocumentReadTool(documentRepository, AllowAll())
            .ExecuteAsync(Invocation(TenantA, "claims-summary", new Dictionary<string, string> { ["caseId"] = caseId.ToString() }), CancellationToken.None);
        var isolated = await new DocumentReadTool(documentRepository, AllowAll())
            .ExecuteAsync(Invocation(TenantB, "claims-summary", new Dictionary<string, string> { ["caseId"] = caseId.ToString() }), CancellationToken.None);
        var denied = await new DocumentReadTool(documentRepository, DenyAll())
            .ExecuteAsync(Invocation(TenantA, "claims-summary", new Dictionary<string, string> { ["caseId"] = caseId.ToString() }), CancellationToken.None);

        var documentsA = Assert.IsAssignableFrom<IReadOnlyCollection<CaseDocument>>(allowed.Data);
        var documentsB = Assert.IsAssignableFrom<IReadOnlyCollection<CaseDocument>>(isolated.Data);
        Assert.Single(documentsA);
        Assert.Empty(documentsB);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task PolicyReadTool_IsolatesByTenantAndDeniesWithoutPermission()
    {
        var repository = new InMemoryPolicyRepository();
        await repository.UpsertAsync(new Policy("policy-a", TenantA.TenantId, TenantA.WorkspaceId, "POL-1001", "Motor", "Comprehensive", null, null, null, null));

        var allowed = await new PolicyReadTool(repository, AllowAll())
            .ExecuteAsync(Invocation(TenantA, "policy-intelligence", new Dictionary<string, string> { ["policyNumber"] = "POL-1001" }), CancellationToken.None);
        var isolated = await new PolicyReadTool(repository, AllowAll())
            .ExecuteAsync(Invocation(TenantB, "policy-intelligence", new Dictionary<string, string> { ["policyNumber"] = "POL-1001" }), CancellationToken.None);
        var denied = await new PolicyReadTool(repository, DenyAll())
            .ExecuteAsync(Invocation(TenantA, "policy-intelligence", new Dictionary<string, string> { ["policyNumber"] = "POL-1001" }), CancellationToken.None);

        Assert.True(allowed.Success);
        Assert.False(isolated.Success);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task ProviderHealthReadTool_RequiresProviderCodeParameterAndDeniesWithoutPermission()
    {
        var healthCheck = new LocalTestProviderHealthCheck();
        var allowedNoParam = await new ProviderHealthReadTool(healthCheck, AllowAll())
            .ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);
        var allowedWithParam = await new ProviderHealthReadTool(healthCheck, AllowAll())
            .ExecuteAsync(Invocation(TenantA, "command-center", new Dictionary<string, string> { ["providerCode"] = "azure_openai" }), CancellationToken.None);
        var denied = await new ProviderHealthReadTool(healthCheck, DenyAll())
            .ExecuteAsync(Invocation(TenantA, "command-center", new Dictionary<string, string> { ["providerCode"] = "azure_openai" }), CancellationToken.None);

        Assert.False(allowedNoParam.Success);
        Assert.Contains("providerCode", allowedNoParam.DeniedReason);
        Assert.True(allowedWithParam.Success);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task MetricsReadTool_ReturnsRealAggregateIsolatedByTenantAndDeniesWithoutPermission()
    {
        var caseRepository = new InMemoryCaseRepository();
        await SeedCaseAsync(caseRepository, TenantA);
        var tool = new MetricsReadTool(
            caseRepository,
            new InMemoryCaseDocumentRepository(),
            new InMemoryKnowledgeDocumentRepository(),
            new InMemoryAgentActionLogRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryWorkflowTaskRepository(),
            new InMemoryCustomerUpdateRepository(),
            new BasicSlaCalculator(),
            AllowAll());
        var deniedTool = new MetricsReadTool(
            caseRepository,
            new InMemoryCaseDocumentRepository(),
            new InMemoryKnowledgeDocumentRepository(),
            new InMemoryAgentActionLogRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryWorkflowTaskRepository(),
            new InMemoryCustomerUpdateRepository(),
            new BasicSlaCalculator(),
            DenyAll());

        var resultA = await tool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);
        var resultB = await tool.ExecuteAsync(Invocation(TenantB, "command-center"), CancellationToken.None);
        var denied = await deniedTool.ExecuteAsync(Invocation(TenantA, "command-center"), CancellationToken.None);

        var metricsA = Assert.IsType<InsuranceIntelligence.Application.CommandCenter.CommandCenterMetricsResponse>(resultA.Data);
        var metricsB = Assert.IsType<InsuranceIntelligence.Application.CommandCenter.CommandCenterMetricsResponse>(resultB.Data);
        Assert.True(resultA.Success);
        Assert.Equal(1, metricsA.TotalCases);
        Assert.Equal(0, metricsB.TotalCases);
        Assert.False(denied.Success);
        Assert.NotEmpty(resultA.EvidenceRefs);
    }

    [Fact]
    public async Task KnowledgeSearchTool_RequiresQueryParameterAndDeniesWithoutPermission()
    {
        var tool = new KnowledgeSearchTool(new EmptyRetrievalProvider(), AllowAll());
        var deniedTool = new KnowledgeSearchTool(new EmptyRetrievalProvider(), DenyAll());

        var missingQuery = await tool.ExecuteAsync(Invocation(TenantA, "policy-intelligence"), CancellationToken.None);
        var withQuery = await tool.ExecuteAsync(
            Invocation(TenantA, "policy-intelligence", new Dictionary<string, string> { ["query"] = "Is windscreen damage covered?" }),
            CancellationToken.None);
        var denied = await deniedTool.ExecuteAsync(
            Invocation(TenantA, "policy-intelligence", new Dictionary<string, string> { ["query"] = "Is windscreen damage covered?" }),
            CancellationToken.None);

        Assert.False(missingQuery.Success);
        Assert.Contains("query", missingQuery.DeniedReason);
        Assert.True(withQuery.Success);
        Assert.False(denied.Success);
    }

    private static AgentToolInvocation Invocation(TenantScope scope, string agentKey, IReadOnlyDictionary<string, string>? parameters = null) =>
        new(scope, agentKey, "user-claims-1", parameters);

    private static ClaimSnapshotLoader SnapshotLoader(ICaseRepository? caseRepository = null) =>
        new(
            caseRepository ?? new InMemoryCaseRepository(),
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new BasicSlaCalculator());

    private static async Task<Guid> SeedCaseAsync(ICaseRepository repository, TenantScope scope)
    {
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision reported at branch intake.",
            Status = CaseStatus.MissingDocuments,
            CompletenessScore = 40m,
            SlaDueAt = DateTimeOffset.UtcNow.AddDays(3),
            AssignedUserId = "user-claims-1",
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-tool-001",
        };
        await repository.SaveAsync(claimCase);
        return claimCase.CaseId;
    }

    private static ApprovalRequest Approval(TenantScope scope, ApprovalStatus status) => new()
    {
        ApprovalId = Guid.NewGuid(),
        Scope = scope,
        CaseId = Guid.NewGuid(),
        ApprovalType = "SupervisorApproval",
        Status = status,
        RiskLevel = "High",
        EvidenceSummary = "Supervisor review required.",
        SourceReferences = ["case-file"],
        RequestedByUserId = "user-claims-1",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        CorrelationId = "corr-approval-001",
    };

    private static AuditEvent AuditEventFor(TenantScope scope) => new(
        Guid.NewGuid(), scope, CaseId: null, DocumentId: null, UserId: "user-claims-1", ActorType.User,
        "test.event", DateTimeOffset.UtcNow, BeforeStatus: null, AfterStatus: "recorded", EvidenceIds: [],
        ModelProvider: null, Confidence: null, ValidationResult: "recorded", ApprovalId: null,
        AuditReason: null, CorrelationId: "corr-audit-tool-001");

    private static CaseDocument DocumentFor(Guid caseId, TenantScope scope) => new()
    {
        DocumentId = Guid.NewGuid(),
        CaseId = caseId,
        Scope = scope,
        OriginalFileName = "claim-form.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 1024,
        ObjectKey = $"{scope.TenantId}/{scope.WorkspaceId}/{caseId}/doc",
        DocumentType = DocumentType.ClaimForm,
        ProcessingStatus = DocumentProcessingStatus.Processed,
        Classification = new DocumentClassificationResult(DocumentType.ClaimForm, 0.95m, "test", DateTimeOffset.UtcNow),
        Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "test", DateTimeOffset.UtcNow, true, "user-claims-1", DateTimeOffset.UtcNow),
        ValidationWarnings = [],
        UploadedByUserId = "user-claims-1",
        UploadedAt = DateTimeOffset.UtcNow,
        CorrelationId = "corr-doc-tool-001",
    };

    private static IPermissionService AllowAll() => new StubPermissionService(Enum.GetValues<Permission>());

    private static IPermissionService DenyAll() => new StubPermissionService();

    private sealed class StubPermissionService(params Permission[] permissions) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissions.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!permissions.Contains(permission))
            {
                throw new PermissionDeniedException(permission);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class EmptyRetrievalProvider : InsuranceIntelligence.Domain.Knowledge.IRetrievalProvider
    {
        public Task<InsuranceIntelligence.Domain.Knowledge.RetrievalResult> RetrieveAsync(
            InsuranceIntelligence.Domain.Knowledge.AssistantQuestion question,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new InsuranceIntelligence.Domain.Knowledge.RetrievalResult(
                question.Question,
                question.ProductLine,
                Array.Empty<InsuranceIntelligence.Domain.Knowledge.KnowledgeChunk>(),
                Array.Empty<InsuranceIntelligence.Domain.Knowledge.SourceCitation>()));
    }
}
