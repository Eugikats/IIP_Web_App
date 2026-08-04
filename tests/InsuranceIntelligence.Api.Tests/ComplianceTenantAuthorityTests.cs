using System.Text.Json;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// RF-01 HTTP API tenant-isolation seam for Compliance: membership-resolved TenantContext only;
/// spoofed headers ignored; missing context fail-closed; cross-tenant case not-found.
/// </summary>
public sealed class ComplianceTenantAuthorityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CompliancePosture_FailsClosedWhenMembershipContextMissing_IgnoresSpoofedHeaders()
    {
        var context = HeaderOnlyContext();
        var result = await ComplianceEndpoints.GetCompliancePosture(
            CreateService(context),
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("tenantScope", out _));
    }

    [Fact]
    public async Task ListAuditEvents_FailsClosedWhenMembershipContextMissing_IgnoresSpoofedHeaders()
    {
        var context = HeaderOnlyContext();
        var result = await ComplianceEndpoints.ListAuditEvents(
            CreateService(context),
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("tenantScope", out _));
    }

    [Fact]
    public async Task GetAuditPack_ReturnsNotFoundForCrossTenantCase()
    {
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var foreignCase = Case(TenantScope.Create("attacker-tenant", "attacker-workspace"));
        var caseRepository = new InMemoryCaseRepository();
        await caseRepository.SaveAsync(foreignCase);

        var context = MembershipContext(membershipScope, "membership-user", spoofTenant: "attacker-tenant", spoofWorkspace: "attacker-workspace");
        var result = await ComplianceEndpoints.GetAuditPack(
            foreignCase.CaseId,
            CreateService(context, caseRepository: caseRepository),
            Options(),
            context,
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task CompliancePosture_UsesResolvedMembershipScopeAndIgnoresSpoofedHeaders()
    {
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var tenantRepository = new InMemoryTenantConfigurationRepository();
        var caseRepository = new InMemoryCaseRepository();
        var onboardingRepository = new FixedOnboardingRepository(
        [
            new TenantOnboardingRecord(
                "onboarding-1",
                "Insurer A",
                "insurer-a.example",
                "admin@example.test",
                ["workspace-membership"],
                ["00000000-0000-0000-0000-000000000001"],
                "tenant-membership",
                "state-1",
                TenantOnboardingStatus.Consented,
                "external-tenant-1",
                "admin-user",
                "operator",
                DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-07-02T00:00:00Z")),
            new TenantOnboardingRecord(
                "attacker-onboarding",
                "Attacker",
                "attacker.example",
                "attacker@example.test",
                ["attacker-workspace"],
                ["00000000-0000-0000-0000-000000000002"],
                "attacker-tenant",
                "state-2",
                TenantOnboardingStatus.Blocked,
                null,
                null,
                "operator",
                DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                null)
        ]);
        await tenantRepository.SaveAsync(TenantConfiguration.Demo() with
        {
            Scope = membershipScope,
            Country = "Uganda",
            Region = "East Africa",
            AuditRetentionDays = 365,
            ProviderModes = new ProviderModeConfiguration(
                "SqlServer",
                "Entra",
                "NotConfigured",
                "NotConfigured",
                "AzureAiSearch",
                "NotConfigured",
                "NotConfigured",
                "NotConfigured",
                false),
            UpdatedAt = DateTimeOffset.Parse("2026-07-03T00:00:00Z")
        });
        await caseRepository.SaveAsync(Case(membershipScope) with { SlaDueAt = DateTimeOffset.UtcNow.AddHours(-1) });
        await caseRepository.SaveAsync(Case(membershipScope) with { SlaDueAt = DateTimeOffset.UtcNow.AddHours(4) });
        await caseRepository.SaveAsync(Case(TenantScope.Create("attacker-tenant", "attacker-workspace")) with { SlaDueAt = DateTimeOffset.UtcNow.AddHours(-1) });

        var context = MembershipContext(membershipScope, "membership-compliance-user", "attacker-tenant", "attacker-workspace");
        var result = await ComplianceEndpoints.GetCompliancePosture(
            CreateService(context, tenantRepository, onboardingRepository, caseRepository),
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<CompliancePostureResponse>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.Equal(365, body!.Retention.AuditRetentionDays);
        Assert.Equal(1, body.Consent.OnboardingRecords);
        Assert.False(body.Consent.BlockedConsent);
        Assert.Equal(2, body.Sla.TotalOpenClaims);
        Assert.Equal(1, body.Sla.AtRisk);
        Assert.Equal(1, body.Sla.Breached);
        Assert.Equal("Uganda", body.DataResidency.TenantCountry);
        Assert.Equal("Compliant", body.DataResidency.Status);
        Assert.Equal("Entra", body.Providers.AuthMode);
    }

    [Fact]
    public async Task AuditPackExport_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var claimCase = Case(membershipScope);
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var storageProvider = new CapturingEvidencePackStorage();
        const string auditReason = "Compliance officer exported the evidence pack using resolved membership context.";
        await caseRepository.SaveAsync(claimCase);

        var context = MembershipContext(
            membershipScope,
            "membership-compliance-user",
            "attacker-tenant",
            "attacker-workspace",
            auditReason);
        var result = await ComplianceEndpoints.ExportAuditPack(
            claimCase.CaseId,
            CreateService(context, caseRepository: caseRepository, auditRepository: auditRepository, storageProvider: storageProvider),
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<EvidencePackExportResponse>(result);
        var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.StartsWith("tenant-membership/workspace-membership/evidence-packs/", body!.ObjectKey, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker-tenant", body.ObjectKey, StringComparison.Ordinal);
        Assert.Contains(body.ObjectKey, storageProvider.StoredPayloads.Keys);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "evidence_pack.exported"
            && auditEvent.UserId == "membership-compliance-user"
            && auditEvent.AuditReason == auditReason);
    }

    [Fact]
    public async Task AuditPack_ExcludesUnsafeContent()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var claimCase = Case(scope);
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var actionRepository = new InMemoryAgentActionLogRepository();
        await caseRepository.SaveAsync(claimCase);
        await auditRepository.WriteAsync(AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), "user", "corr"));
        await actionRepository.SaveAsync(new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = claimCase.CaseId,
            UserId = "user",
            AgentName = "PolicyQnA",
            AssistantMode = AssistantMode.PolicyQnA,
            InputSummary = "raw prompt",
            RetrievedSources = [],
            ModelOutput = "system message",
            ConfidenceScore = AiConfidenceScore.Create(0.5m),
            RiskFlag = "needs_review",
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        });

        var context = MembershipContext(scope, "local-admin", "attacker-tenant", "attacker-workspace");
        var result = await ComplianceEndpoints.GetAuditPack(
            claimCase.CaseId,
            CreateService(context, caseRepository: caseRepository, auditRepository: auditRepository, actionRepository: actionRepository),
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(body.GetProperty("assistantActions").EnumerateArray());
    }

    private static IComplianceQueryService CreateService(
        HttpContext httpContext,
        ITenantConfigurationRepository? tenantRepository = null,
        ITenantOnboardingRepository? onboardingRepository = null,
        ICaseRepository? caseRepository = null,
        IAuditRepository? auditRepository = null,
        ICaseDocumentRepository? documentRepository = null,
        IAgentActionLogRepository? actionRepository = null,
        IApprovalRepository? approvalRepository = null,
        ICustomerUpdateRepository? updateRepository = null,
        IEvidencePackStorageProvider? storageProvider = null)
    {
        var tenantContext = httpContext.RequestServices.GetService<ITenantContextAccessor>()
            ?? new TenantContextAccessor();
        var options = Options();
        return new ComplianceQueryService(
            tenantContext,
            new PermissionService(tenantContext, new LocalTestPermissionReadRepository()),
            tenantRepository ?? new InMemoryTenantConfigurationRepository(),
            onboardingRepository ?? new FixedOnboardingRepository([]),
            caseRepository ?? new InMemoryCaseRepository(),
            auditRepository ?? new InMemoryAuditRepository(),
            documentRepository ?? new InMemoryCaseDocumentRepository(),
            actionRepository ?? new InMemoryAgentActionLogRepository(),
            approvalRepository ?? new InMemoryApprovalRepository(),
            updateRepository ?? new InMemoryCustomerUpdateRepository(),
            storageProvider ?? new CapturingEvidencePackStorage(),
            new BasicSlaCalculator(),
            new ComplianceResidencySettings(
                options.DataResidencyCountry,
                options.DataResidencyGeography,
                options.AzureRegion,
                options.DataResidencyMode,
                options.AzureOpenAI.DeploymentType,
                options.DataResidencyAllowedAzureRegions,
                options.DataResidencyApprovalReference,
                options.AiProvider));
    }

    private static InfrastructureOptions Options() =>
        new()
        {
            AuthMode = "Development",
            StorageProvider = "InMemory",
            AzureRegion = "approved-region-1",
            DataResidencyCountry = "Approved jurisdiction",
            DataResidencyGeography = "Approved geography",
            DataResidencyMode = "regional",
            DataResidencyAllowedAzureRegions = "approved-region-1",
            DataResidencyApprovalReference = "RES-TEST-001"
        };

    private static DefaultHttpContext HeaderOnlyContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-compliance" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["x-demo-role"] = "Admin";
        context.Request.Headers["x-demo-user-id"] = "local-admin";
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext MembershipContext(
        TenantScope membershipScope,
        string membershipUserId,
        string spoofTenant,
        string spoofWorkspace,
        string? auditReason = null)
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            membershipScope.TenantId,
            membershipScope.WorkspaceId,
            membershipUserId,
            ["Admin"]));
        var context = HeaderOnlyContext();
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenantContext)
            .BuildServiceProvider();
        context.Request.Headers["X-Tenant-Id"] = spoofTenant;
        context.Request.Headers["X-Workspace-Id"] = spoofWorkspace;
        context.Request.Headers["X-User-Id"] = "attacker-user";
        context.Request.Headers["x-demo-user-id"] = "attacker-user";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }

        return context;
    }

    private static ClaimCase Case(TenantScope scope) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Amina",
            CustomerEmail = "amina@example.test",
            CustomerPhone = "+256700000000",
            PolicyNumber = "POL-1",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IncidentDescription = "Demo",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        };

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed class FixedOnboardingRepository(IReadOnlyList<TenantOnboardingRecord> records) : ITenantOnboardingRepository
    {
        public Task UpsertAsync(TenantOnboardingRecord record, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TenantOnboardingRecord?> GetAsync(string onboardingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(item => item.OnboardingId == onboardingId));

        public Task<TenantOnboardingRecord?> GetByStateAsync(string state, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(item => item.State == state));

        public Task<IReadOnlyList<TenantOnboardingRecord>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(records);
    }

    private sealed class CapturingEvidencePackStorage : IEvidencePackStorageProvider
    {
        public Dictionary<string, byte[]> StoredPayloads { get; } = new(StringComparer.Ordinal);

        public async Task<EvidencePackStorageResult> StoreAsync(
            EvidencePackStorageRequest request,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            var objectKey = $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/evidence-packs/{request.FileName}";
            StoredPayloads[objectKey] = buffer.ToArray();
            return new EvidencePackStorageResult(objectKey, request.ContentType, buffer.Length);
        }
    }
}
