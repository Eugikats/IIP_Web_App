using System.Text.Json;
using InsuranceIntelligence.Application.Tenants;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Infrastructure.Simulated;

/// <summary>
/// LocalTest/InMemory-only demo data. Seeds a believable motor-claim spread so every
/// Command Center block type renders with real-looking data during local rehearsal.
/// NEVER invoke against Azure providers — it is guarded by <c>storageProvider == "InMemory"</c>
/// in Program.cs and marked obsolete so any production call surfaces as a build warning.
/// </summary>
[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");

        var tenantRepository = serviceProvider.GetRequiredService<ITenantConfigurationRepository>();
        await tenantRepository.SaveAsync(TenantConfiguration.Demo(), cancellationToken);

        // Multi-workspace data model (B4): seed two workspaces for the demo tenant so the
        // /me available-workspaces list is exercised. EF-backed storage only.
        var workspaceDb = serviceProvider.GetService<InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext>();
        if (workspaceDb is not null
            && !await workspaceDb.Workspaces.AnyAsync(w => w.TenantId == scope.TenantId, cancellationToken))
        {
            workspaceDb.Workspaces.AddRange(
                DemoWorkspace(scope.TenantId, "workspace-claims", "Kampala Motor Claims"),
                DemoWorkspace(scope.TenantId, "workspace-life", "Kampala Life Claims"));
            await workspaceDb.SaveChangesAsync(cancellationToken);
        }

        var userRepository = serviceProvider.GetRequiredService<IUserAccountRepository>();
        foreach (var user in DemoUsers(scope))
        {
            await userRepository.SaveAsync(user, cancellationToken);
        }

        var caseRepository = serviceProvider.GetRequiredService<ICaseRepository>();
        var existingCases = await caseRepository.ListAsync(scope, cancellationToken);
        if (existingCases.Count > 0)
        {
            // Idempotent: already seeded (or real data present) — don't duplicate.
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var auditRepository = serviceProvider.GetRequiredService<IAuditRepository>();

        // ── Claims — spread across SLA state (breached / at-risk / on-track),
        //    completeness, status and assignment so every block populates. ──────
        var claims = new[]
        {
            // Breached, badly incomplete — top of the attention queue.
            MotorCase(scope, now, "11111111-1111-1111-1111-111111111111", "Amina Okello", "amina@example.test", "+256700000000",
                "POL-1001", "Accident", CaseStatus.MissingDocuments, 0.15m, "High", CasePriority.Urgent,
                slaDueAt: now.AddHours(-6), assignedUserId: "local-claims-officer"),

            // Breached, escalated.
            MotorCase(scope, now, "a1000002-0000-0000-0000-000000000002", "Brian Mwangi", "brian@example.test", "+254711000002",
                "POL-1002", "Theft", CaseStatus.Escalated, 0.35m, "High", CasePriority.High,
                slaDueAt: now.AddDays(-2), assignedUserId: "local-supervisor"),

            // At-risk, missing documents (breaches within 24h).
            MotorCase(scope, now, "a1000003-0000-0000-0000-000000000003", "Chipo Ncube", "chipo@example.test", "+263772000003",
                "POL-1003", "Windscreen", CaseStatus.MissingDocuments, 0.55m, "Medium", CasePriority.High,
                slaDueAt: now.AddHours(12), assignedUserId: "local-claims-officer"),

            // At-risk, ready for review.
            MotorCase(scope, now, "a1000004-0000-0000-0000-000000000004", "Daudi Juma", "daudi@example.test", "+255744000004",
                "POL-1004", "Accident", CaseStatus.ReadyForReview, 0.7m, "Medium", CasePriority.Normal,
                slaDueAt: now.AddHours(20), assignedUserId: "local-supervisor"),

            // On-track, nearly complete.
            MotorCase(scope, now, "a1000005-0000-0000-0000-000000000005", "Esther Adeyemi", "esther@example.test", "+234805000005",
                "POL-1005", "Third-party", CaseStatus.ReadyForReview, 0.9m, "Low", CasePriority.Normal,
                slaDueAt: now.AddDays(3), assignedUserId: "local-claims-officer"),

            // On-track, freshly intook.
            MotorCase(scope, now, "a1000006-0000-0000-0000-000000000006", "Fatima Sesay", "fatima@example.test", "+232766000006",
                "POL-1006", "Accident", CaseStatus.New, 0.25m, "Medium", CasePriority.Normal,
                slaDueAt: now.AddDays(5), assignedUserId: "local-claims-officer"),

            // On-track, in progress with gaps.
            MotorCase(scope, now, "a1000007-0000-0000-0000-000000000007", "George Otieno", "george@example.test", "+254733000007",
                "POL-1007", "Fire", CaseStatus.MissingDocuments, 0.6m, "Medium", CasePriority.Normal,
                slaDueAt: now.AddDays(2), assignedUserId: "local-supervisor"),

            // Breached, low completeness — second breach for a visible cluster.
            MotorCase(scope, now, "a1000008-0000-0000-0000-000000000008", "Hawa Diallo", "hawa@example.test", "+224622000008",
                "POL-1008", "Accident", CaseStatus.MissingDocuments, 0.4m, "High", CasePriority.High,
                slaDueAt: now.AddHours(-30), assignedUserId: "local-claims-officer"),
        };

        foreach (var claimCase in claims)
        {
            await caseRepository.SaveAsync(claimCase, cancellationToken);
            await auditRepository.WriteAsync(
                AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), claimCase.CreatedByUserId, claimCase.CorrelationId),
                cancellationToken);
        }

        // ── Knowledge — one approved, indexed policy wording for grounded answers. ──
        var knowledgeRepository = serviceProvider.GetRequiredService<IKnowledgeDocumentRepository>();
        await knowledgeRepository.SaveAsync(DemoKnowledgeDocument(scope, now), cancellationToken);

        // ── Approvals — three pending, awaiting human sign-off. ──────────────────
        var approvalRepository = serviceProvider.GetRequiredService<IApprovalRepository>();
        var approvals = new[]
        {
            PendingApproval(scope, now, "33333333-3333-3333-3333-333333333333", claims[0].CaseId, "SupervisorApproval", "High",
                "Breached SLA on an incomplete claim — supervisor review required."),
            PendingApproval(scope, now, "a3000002-0000-0000-0000-000000000002", claims[1].CaseId, "EscalationApproval", "High",
                "Suspected theft escalation pending a human decision."),
            PendingApproval(scope, now, "a3000003-0000-0000-0000-000000000003", claims[3].CaseId, "SettlementReviewApproval", "Medium",
                "Claim ready for review — settlement path needs sign-off."),
        };

        foreach (var approval in approvals)
        {
            await approvalRepository.SaveAsync(approval, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.ApprovalCreated(approval), cancellationToken);
        }

        // ── Customer updates — draft-only; each still requires human approval. ───
        var updateRepository = serviceProvider.GetRequiredService<ICustomerUpdateRepository>();
        var updates = new[]
        {
            DraftUpdate(scope, now, "a4000001-0000-0000-0000-000000000001", claims[0].CaseId, CustomerUpdateReasonCategory.MissingDocuments,
                "Hello Amina, to progress your motor claim we still need your police abstract and repair estimate. Please reply with these documents."),
            DraftUpdate(scope, now, "a4000002-0000-0000-0000-000000000002", claims[2].CaseId, CustomerUpdateReasonCategory.MissingDocuments,
                "Hello Chipo, please share your windscreen replacement invoice so we can finalise your claim before the review deadline."),
        };

        foreach (var update in updates)
        {
            await updateRepository.SaveAsync(update, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.CustomerUpdateDrafted(update), cancellationToken);
        }
    }

    private static ClaimCase MotorCase(
        TenantScope scope,
        DateTimeOffset now,
        string caseId,
        string customerName,
        string customerEmail,
        string customerPhone,
        string policyNumber,
        string claimType,
        CaseStatus status,
        decimal completeness,
        string riskLevel,
        CasePriority priority,
        DateTimeOffset slaDueAt,
        string assignedUserId) =>
        new()
        {
            CaseId = Guid.Parse(caseId),
            Scope = scope,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            PolicyNumber = policyNumber,
            ProductLine = "Motor",
            ClaimType = claimType,
            Channel = "WebPortal",
            IncidentDate = DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-3)),
            IncidentDescription = $"Demo motor {claimType.ToLowerInvariant()} claim created by seed data.",
            Status = status,
            CompletenessScore = completeness,
            RiskLevel = riskLevel,
            Priority = priority,
            SlaDueAt = slaDueAt,
            AssignedUserId = assignedUserId,
            AssignedTeam = "Claims",
            CreatedByUserId = "local-claims-officer",
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now,
            CorrelationId = "seed-demo"
        };

    private static ApprovalRequest PendingApproval(
        TenantScope scope,
        DateTimeOffset now,
        string approvalId,
        Guid caseId,
        string approvalType,
        string riskLevel,
        string evidenceSummary) =>
        new()
        {
            ApprovalId = Guid.Parse(approvalId),
            Scope = scope,
            CaseId = caseId,
            ApprovalType = approvalType,
            Status = ApprovalStatus.Pending,
            RiskLevel = riskLevel,
            EvidenceSummary = evidenceSummary,
            SourceReferences = ["case-file"],
            RequestedByUserId = "local-claims-officer",
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = "seed-demo"
        };

    private static CustomerUpdateDraft DraftUpdate(
        TenantScope scope,
        DateTimeOffset now,
        string updateId,
        Guid caseId,
        CustomerUpdateReasonCategory reasonCategory,
        string messageBody) =>
        new()
        {
            UpdateId = Guid.Parse(updateId),
            Scope = scope,
            CaseId = caseId,
            ReasonCategory = reasonCategory,
            Channel = CommunicationChannel.Email,
            Status = CustomerUpdateStatus.Draft,
            MessageBody = messageBody,
            RequiresApproval = true,
            Sensitive = false,
            CreatedByUserId = "local-claims-officer",
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = "seed-demo"
        };

    private static KnowledgeDocument DemoKnowledgeDocument(TenantScope scope, DateTimeOffset now)
    {
        var knowledgeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        return new KnowledgeDocument
        {
            KnowledgeDocumentId = knowledgeId,
            Scope = scope,
            DocumentName = "Demo Motor Policy Wording",
            ProductLine = "Motor",
            DocumentType = KnowledgeDocumentType.PolicyWording,
            Version = "1.0",
            EffectiveDate = DateOnly.FromDateTime(now.UtcDateTime.Date.AddYears(-1)),
            ExpiryDate = null,
            OriginalFileName = "demo-motor-policy.md",
            ContentType = "text/markdown",
            FileSizeBytes = 128,
            ObjectKey = "demo/knowledge/demo-motor-policy.md",
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.Indexed,
            Chunks =
            [
                new KnowledgeChunk
                {
                    ChunkId = "demo-motor-policy-0",
                    KnowledgeDocumentId = knowledgeId,
                    Scope = scope,
                    DocumentName = "Demo Motor Policy Wording",
                    ProductLine = "Motor",
                    Version = "1.0",
                    ChunkIndex = 0,
                    Content = "Demo policy wording for local grounded answers.",
                    SourceReference = new KnowledgeSourceReference(knowledgeId, "demo-motor-policy-0", "Demo Motor Policy Wording", "Motor", "1.0", "demo/knowledge/demo-motor-policy.md")
                }
            ],
            UploadedByUserId = "local-knowledge-manager",
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = "seed-demo"
        };
    }

    private static PersistedWorkspace DemoWorkspace(string tenantId, string workspaceId, string displayName) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            PayloadJson = JsonSerializer.Serialize(
                new WorkspaceSummaryDto(workspaceId, displayName, "af-east"),
                JsonSerializerSettings.Options),
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static IReadOnlyCollection<InsuranceUserAccount> DemoUsers(TenantScope scope) =>
    [
        User("local-admin", "Local Admin", "admin@example.test", PlatformRole.Admin, scope),
        User("local-it-admin", "Local IT Admin", "it@example.test", PlatformRole.ITAdmin, scope),
        User("local-claims-officer", "Claims Officer", "claims@example.test", PlatformRole.ClaimsOfficer, scope),
        User("local-supervisor", "Claims Supervisor", "supervisor@example.test", PlatformRole.ClaimsSupervisor, scope),
        User("local-service-agent", "Service Agent", "service@example.test", PlatformRole.CustomerService, scope),
        User("local-compliance", "Compliance Officer", "compliance@example.test", PlatformRole.Compliance, scope),
        User("local-knowledge-manager", "Knowledge Manager", "knowledge@example.test", PlatformRole.KnowledgeManager, scope)
    ];

    private static InsuranceUserAccount User(string id, string name, string email, PlatformRole role, TenantScope scope) =>
        new()
        {
            UserId = id,
            Scope = scope,
            Name = name,
            Email = email,
            Role = role,
            Department = "Demo",
            Branch = "Head Office"
        };
}
