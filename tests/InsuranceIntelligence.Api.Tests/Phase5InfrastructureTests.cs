using System.Text.Json;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Compliance;
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
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class Phase5InfrastructureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EfRepository_CreatesRetrievesAndIsolatesCase()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfCaseRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var claimCase = Case(scope);
        await repository.SaveAsync(claimCase);

        var sameScope = await repository.ListAsync(scope);
        var otherScope = await repository.ListAsync(TenantScope.Create("tenant-b", "workspace-a"));

        Assert.Single(sameScope);
        Assert.Empty(otherScope);
        Assert.Equal(claimCase.CaseId, (await repository.GetAsync(claimCase.CaseId))!.CaseId);
    }

    [Fact]
    public async Task EfRepository_PreservesAuditEvents()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfAuditRepository(fixture.DbContext);
        var claimCase = Case(TenantScope.Create("tenant-a", "workspace-a"));
        await repository.WriteAsync(AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), "user", "corr"));

        var events = await repository.ListAsync(claimCase.Scope);

        Assert.Single(events);
        Assert.Equal("case.created", events.First().ActionName);
    }

    [Fact]
    public async Task EfAgentRunRepository_RoundTripsAndIsolatesByTenantAndCase()
    {
        // WO-07: proves the AgentRun persistence layer through real EF Core machinery (the
        // AddAgentRuns migration's model), not only the in-memory test double.
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfAgentRunRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var caseId = Guid.NewGuid();
        var run = new InsuranceIntelligence.Domain.Agents.AgentRun(
            Guid.NewGuid(),
            scope,
            "command-center",
            "command_center_query",
            ["What needs my attention today?"],
            InsuranceIntelligence.Domain.Agents.AgentRunStatus.Completed,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ErrorCategory: null,
            "corr-agent-run-001",
            "user-claims-1",
            caseId,
            [new InsuranceIntelligence.Domain.Agents.AgentRunToolTraceEntry("claims.attention.read", DateTimeOffset.UtcNow, "ok")]);

        await repository.SaveAsync(run);

        var fetched = await repository.GetAsync(run.AgentRunId, scope);
        var sameScope = await repository.ListAsync(scope);
        var otherScope = await repository.ListAsync(TenantScope.Create("tenant-b", "workspace-a"));
        var forCase = await repository.ListForCaseAsync(caseId, scope);
        var forOtherCase = await repository.ListForCaseAsync(Guid.NewGuid(), scope);

        Assert.NotNull(fetched);
        Assert.Equal("command-center", fetched!.AgentKey);
        Assert.Equal(InsuranceIntelligence.Domain.Agents.AgentRunStatus.Completed, fetched.Status);
        Assert.Single(fetched.ToolTrace);
        Assert.Equal("claims.attention.read", fetched.ToolTrace.First().ToolName);
        Assert.Single(sameScope);
        Assert.Empty(otherScope);
        Assert.Single(forCase);
        Assert.Empty(forOtherCase);
    }

    [Fact]
    public async Task EfNotificationRepository_RoundTripsAndIsolatesByTenantAndRecipient()
    {
        // WO-14: proves the Notification persistence layer through real EF Core machinery (the
        // AddNotifications migration's model), not only the in-memory test double.
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new InsuranceIntelligence.Infrastructure.Notifications.EfNotificationRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var caseId = Guid.NewGuid();
        var notification = new InsuranceIntelligence.Domain.Notifications.Notification(
            Guid.NewGuid(),
            scope,
            "user-claims-1",
            InsuranceIntelligence.Domain.Notifications.NotificationType.ClaimEscalated,
            "This claim was escalated.",
            caseId,
            DateTimeOffset.UtcNow,
            IsRead: false,
            ReadAt: null);

        await repository.SaveAsync(notification);

        var fetched = await repository.GetAsync(notification.NotificationId, scope, "user-claims-1");
        var forRecipient = await repository.ListForUserAsync(scope, "user-claims-1", 20);
        var forOtherRecipient = await repository.ListForUserAsync(scope, "someone-else", 20);
        var forOtherTenant = await repository.ListForUserAsync(TenantScope.Create("tenant-b", "workspace-a"), "user-claims-1", 20);
        var unreadCount = await repository.CountUnreadForUserAsync(scope, "user-claims-1");
        var existsSameType = await repository.ExistsAsync(
            scope, "user-claims-1", InsuranceIntelligence.Domain.Notifications.NotificationType.ClaimEscalated, caseId);
        var existsOtherType = await repository.ExistsAsync(
            scope, "user-claims-1", InsuranceIntelligence.Domain.Notifications.NotificationType.SlaBreach, caseId);

        Assert.NotNull(fetched);
        Assert.Equal("This claim was escalated.", fetched!.Message);
        Assert.Single(forRecipient);
        Assert.Empty(forOtherRecipient);
        Assert.Empty(forOtherTenant);
        Assert.Equal(1, unreadCount);
        Assert.True(existsSameType);
        Assert.False(existsOtherType);

        await repository.SaveAsync(notification with { IsRead = true, ReadAt = DateTimeOffset.UtcNow });
        Assert.Equal(0, await repository.CountUnreadForUserAsync(scope, "user-claims-1"));
    }

    [Fact]
    public async Task EfFraudSignalDispositionRepository_RoundTripsAndIsolatesByTenant()
    {
        // WO-19: proves the disposition persistence layer through real EF Core machinery (the
        // AddFraudSignalDispositions migration's model), not only the in-memory test double.
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new InsuranceIntelligence.Infrastructure.Fraud.EfFraudSignalDispositionRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var caseId = Guid.NewGuid();
        var signalId = InsuranceIntelligence.Domain.Fraud.FraudSignalDisposition.DeterministicId("R-VELOCITY", [caseId]);
        var disposition = new InsuranceIntelligence.Domain.Fraud.FraudSignalDisposition
        {
            SignalId = signalId,
            Scope = scope,
            RuleId = "R-VELOCITY",
            RelatedCaseIds = [caseId],
            Status = InsuranceIntelligence.Domain.Fraud.FraudDispositionStatus.Triaged,
            CreatedByUserId = "supervisor-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-fraud-001"
        };

        await repository.SaveAsync(disposition);

        var fetched = await repository.GetAsync(signalId, scope);
        var listed = await repository.ListAsync(scope);
        var otherTenantFetch = await repository.GetAsync(signalId, TenantScope.Create("tenant-b", "workspace-a"));
        var otherTenantList = await repository.ListAsync(TenantScope.Create("tenant-b", "workspace-a"));

        Assert.NotNull(fetched);
        Assert.Equal("R-VELOCITY", fetched!.RuleId);
        Assert.Equal(InsuranceIntelligence.Domain.Fraud.FraudDispositionStatus.Triaged, fetched.Status);
        Assert.Single(listed);
        Assert.Null(otherTenantFetch);
        Assert.Empty(otherTenantList);

        var reassigned = disposition with
        {
            Status = InsuranceIntelligence.Domain.Fraud.FraudDispositionStatus.UnderInvestigation,
            AssignedToUserId = "investigator-1",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await repository.SaveAsync(reassigned);
        var updated = await repository.GetAsync(signalId, scope);
        Assert.Equal(InsuranceIntelligence.Domain.Fraud.FraudDispositionStatus.UnderInvestigation, updated!.Status);
        Assert.Equal("investigator-1", updated.AssignedToUserId);
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_WorksForAdmin()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        const string auditReason = "Tenant administrator approved the insurer configuration update.";
        var context = ScopedContext(role: "Admin", auditReason: auditReason);
        var request = TenantConfigurationRequest();

        var result = await AdminEndpoints.UpdateTenantConfiguration(request, repository, auditRepository, Options(), context, CancellationToken.None);
        var (status, body) = await ExecuteAsync<TenantConfiguration>(result);
        var auditEvents = await auditRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims"));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Updated Insurer", body!.InsurerName);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "admin.tenant_configuration.updated"
            && auditEvent.UserId == "local-admin"
            && auditEvent.AuditReason == auditReason
            && auditEvent.EvidenceIds.Contains("insurerProfile")
            && auditEvent.EvidenceIds.Contains("auditRetention"));
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var spoofedScope = TenantScope.Create("tenant-spoofed", "workspace-spoofed");

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            TenantConfigurationRequest(),
            repository,
            auditRepository,
            Options(),
            MembershipScopedContext(
                membershipScope,
                "membership-admin-user",
                spoofedScope.TenantId,
                spoofedScope.WorkspaceId,
                "spoofed-user",
                "Membership admin approved tenant configuration hardening test."),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<TenantConfiguration>(result);
        var membershipConfiguration = await repository.GetAsync(membershipScope);
        var spoofedConfiguration = await repository.GetAsync(spoofedScope);
        var auditEvents = await auditRepository.ListAsync(membershipScope);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(membershipScope, body!.Scope);
        Assert.Equal("Updated Insurer", membershipConfiguration.InsurerName);
        Assert.NotEqual("Updated Insurer", spoofedConfiguration.InsurerName);
        Assert.Contains(
            auditEvents,
            auditEvent => auditEvent.ActionName == "admin.tenant_configuration.updated"
                && auditEvent.UserId == "membership-admin-user");
    }

    [Fact]
    public async Task AdminTenantConfigFirstUpdate_AuditsOnboardingTransition()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await repository.SaveAsync(TenantConfiguration.Unconfigured(scope));

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            TenantConfigurationRequest(),
            repository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: "Configure insurer profile during governed tenant onboarding."),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<TenantConfiguration>(result);
        var auditEvent = Assert.Single(await auditRepository.ListAsync(scope));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("NotConfigured", auditEvent.BeforeStatus);
        Assert.Equal("Configured", auditEvent.AfterStatus);
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_RejectsMissingReasonBeforePersistence()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var before = await repository.GetAsync(scope);

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            TenantConfigurationRequest(),
            repository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var after = await repository.GetAsync(scope);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(before.InsurerName, after.InsurerName);
        Assert.Equal(before.AuditRetentionDays, after.AuditRetentionDays);
        Assert.Empty(await auditRepository.ListAsync(scope));
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_RejectsNoOpMutation()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var current = await repository.GetAsync(TenantScope.Create("tenant-demo", "workspace-claims"));
        var request = new UpdateTenantConfigurationRequest(
            current.InsurerName,
            current.LogoUrl,
            current.Country,
            current.Region,
            current.BusinessLines,
            current.Channels,
            current.ApprovalThresholds,
            current.Checklists,
            current.AuditRetentionDays);

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            request,
            repository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: "Attempt no-op tenant update."),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task AdminUserRoleUpdate_PersistsAuditReasonHeader()
    {
        var userRepository = new InMemoryUserAccountRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await userRepository.SaveAsync(new InsuranceUserAccount
        {
            UserId = "user-claims-1",
            Scope = scope,
            Name = "Claims Handler",
            Email = "claims@example.test",
            Phone = null,
            Role = PlatformRole.ClaimsOfficer,
            Department = "Claims",
            Branch = "Head Office"
        });

        const string auditReason = "Tenant admin approved role escalation for claims supervision coverage.";
        var result = await AdminEndpoints.UpdateUserRole(
            "user-claims-1",
            new UpdateUserRoleRequest("ClaimsSupervisor"),
            userRepository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: auditReason),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<AdminUserResponse>(result);
        var auditEvents = await auditRepository.ListAsync(scope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("ClaimsSupervisor", body!.Role);
        Assert.Contains(body.AllowedActions, action => action.Action == "updateUserRole");
        Assert.Contains(auditEvents, audit =>
            audit.ActionName == "admin.user_role.updated"
            && audit.BeforeStatus == "ClaimsOfficer"
            && audit.AfterStatus == "ClaimsSupervisor"
            && audit.AuditReason == auditReason
            && audit.UserId == "local-admin");
    }

    [Fact]
    public async Task AdminUserRoleUpdate_RejectsMissingAuditReasonBeforeMutation()
    {
        var userRepository = new InMemoryUserAccountRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await userRepository.SaveAsync(new InsuranceUserAccount
        {
            UserId = "user-claims-1",
            Scope = scope,
            Name = "Claims Handler",
            Email = "claims@example.test",
            Role = PlatformRole.ClaimsOfficer
        });

        var result = await AdminEndpoints.UpdateUserRole(
            "user-claims-1",
            new UpdateUserRoleRequest("ClaimsSupervisor"),
            userRepository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var persisted = await userRepository.GetAsync("user-claims-1", scope, CancellationToken.None);
        var auditEvents = await auditRepository.ListAsync(scope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(PlatformRole.ClaimsOfficer, persisted!.Role);
        Assert.Empty(auditEvents);
    }

    [Fact]
    public async Task AdminUserRoleUpdate_BlocksDemotionOfLastActiveAdmin()
    {
        var userRepository = new InMemoryUserAccountRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await userRepository.SaveAsync(new InsuranceUserAccount
        {
            UserId = "last-admin",
            Scope = scope,
            Name = "Last Administrator",
            Email = "admin@example.test",
            Role = PlatformRole.Admin,
            Status = "Active"
        });

        var result = await AdminEndpoints.UpdateUserRole(
            "last-admin",
            new UpdateUserRoleRequest("ClaimsOfficer"),
            userRepository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: "Attempt tenant administrator demotion."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var persisted = await userRepository.GetAsync("last-admin", scope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("updateUserRole", body.GetProperty("blockedAction").GetString());
        Assert.Equal(PlatformRole.Admin, persisted!.Role);
    }

    [Fact]
    public async Task AdminRoles_AreDeterministicAndDoNotExposeDemoTenantScope()
    {
        var first = AdminEndpoints.GetRoles(Options(), ScopedContext(role: "Admin"));
        var second = AdminEndpoints.GetRoles(Options(), ScopedContext(role: "Admin"));
        var (firstStatus, firstBody) = await ExecuteAsync<AdminRoleResponse[]>(first);
        var (secondStatus, secondBody) = await ExecuteAsync<AdminRoleResponse[]>(second);
        var serialized = JsonSerializer.Serialize(firstBody);

        Assert.Equal(StatusCodes.Status200OK, firstStatus);
        Assert.Equal(StatusCodes.Status200OK, secondStatus);
        Assert.Equal(JsonSerializer.Serialize(firstBody), JsonSerializer.Serialize(secondBody));
        Assert.DoesNotContain("tenant-demo", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace-claims", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.All(firstBody!, role => Assert.NotEmpty(role.Permissions));
    }

    [Fact]
    public async Task RoleWithoutPermission_GetsForbidden()
    {
        var result = AdminEndpoints.GetProviderModes(Options(), ScopedContext(role: "Client"));
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task AuditPack_ExcludesUnsafeContent()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var claimCase = Case(scope);
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var actionRepository = new InMemoryAgentActionLogRepository();
        var approvalRepository = new InMemoryApprovalRepository();
        var updateRepository = new InMemoryCustomerUpdateRepository();
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

        var context = MembershipScopedContext(scope, "local-admin", "attacker-tenant", "attacker-workspace", "attacker-user");
        var service = CreateComplianceService(
            context,
            caseRepository: caseRepository,
            auditRepository: auditRepository,
            documentRepository: documentRepository,
            actionRepository: actionRepository,
            approvalRepository: approvalRepository,
            updateRepository: updateRepository);

        var result = await ComplianceEndpoints.GetAuditPack(
            claimCase.CaseId,
            service,
            Options(),
            context,
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(body.GetProperty("assistantActions").EnumerateArray());
    }

    [Fact]
    public async Task AuditPackExport_StoresArtifactAndWritesAuditReason()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var claimCase = Case(scope) with { Status = CaseStatus.ReadyForExport };
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var actionRepository = new InMemoryAgentActionLogRepository();
        var approvalRepository = new InMemoryApprovalRepository();
        var updateRepository = new InMemoryCustomerUpdateRepository();
        var storageProvider = new TestEvidencePackStorageProvider();
        const string auditReason = "Compliance officer exported the claim evidence pack.";
        await caseRepository.SaveAsync(claimCase);

        var context = MembershipScopedContext(
            scope,
            "local-admin",
            "attacker-tenant",
            "attacker-workspace",
            "attacker-user",
            auditReason);
        var service = CreateComplianceService(
            context,
            caseRepository: caseRepository,
            auditRepository: auditRepository,
            documentRepository: documentRepository,
            actionRepository: actionRepository,
            approvalRepository: approvalRepository,
            updateRepository: updateRepository,
            storageProvider: storageProvider);

        var result = await ComplianceEndpoints.ExportAuditPack(
            claimCase.CaseId,
            service,
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<EvidencePackExportResponse>(result);
        var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.Equal(claimCase.CaseId, body!.CaseId);
        Assert.Equal("application/json", body.ContentType);
        Assert.True(body.FileSizeBytes > 0);
        Assert.Contains(body.ObjectKey, storageProvider.StoredPayloads.Keys);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "evidence_pack.exported"
            && auditEvent.AuditReason == auditReason
            && auditEvent.EvidenceIds.Contains(body.ObjectKey));
    }

    [Fact]
    public async Task AuditPackExport_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var claimCase = Case(membershipScope) with { Status = CaseStatus.ReadyForExport };
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var actionRepository = new InMemoryAgentActionLogRepository();
        var approvalRepository = new InMemoryApprovalRepository();
        var updateRepository = new InMemoryCustomerUpdateRepository();
        var storageProvider = new TestEvidencePackStorageProvider();
        const string auditReason = "Compliance officer exported the evidence pack using resolved membership context.";
        await caseRepository.SaveAsync(claimCase);

        var context = MembershipScopedContext(
            membershipScope,
            "membership-compliance-user",
            "attacker-tenant",
            "attacker-workspace",
            "attacker-user",
            auditReason);
        var service = CreateComplianceService(
            context,
            caseRepository: caseRepository,
            auditRepository: auditRepository,
            documentRepository: documentRepository,
            actionRepository: actionRepository,
            approvalRepository: approvalRepository,
            updateRepository: updateRepository,
            storageProvider: storageProvider);

        var result = await ComplianceEndpoints.ExportAuditPack(
            claimCase.CaseId,
            service,
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
    public async Task CompliancePosture_FailsClosedWhenMembershipContextMissing_IgnoresSpoofedHeaders()
    {
        // Headers/query present but no server-resolved TenantContext ΓÇö must fail closed.
        var result = await ComplianceEndpoints.GetCompliancePosture(
            CreateComplianceService(ScopedContext()),
            Options(),
            ScopedContext(),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("tenantScope", out _));
    }

    [Fact]
    public async Task GetAuditPack_ReturnsNotFoundForCrossTenantCase()
    {
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var foreignScope = TenantScope.Create("attacker-tenant", "attacker-workspace");
        var claimCase = Case(foreignScope);
        var caseRepository = new InMemoryCaseRepository();
        await caseRepository.SaveAsync(claimCase);

        var context = MembershipScopedContext(
            membershipScope,
            "membership-compliance-user",
            "attacker-tenant",
            "attacker-workspace",
            "attacker-user");
        var service = CreateComplianceService(context, caseRepository: caseRepository);

        var result = await ComplianceEndpoints.GetAuditPack(
            claimCase.CaseId,
            service,
            Options(),
            context,
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task CompliancePosture_UsesResolvedScopeAndReturnsAggregateComplianceState()
    {
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var tenantRepository = new InMemoryTenantConfigurationRepository();
        var caseRepository = new InMemoryCaseRepository();
        var onboardingRepository = new RecordingTenantOnboardingRepository(
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

        var context = MembershipScopedContext(
            membershipScope,
            "membership-compliance-user",
            "attacker-tenant",
            "attacker-workspace",
            "attacker-user");
        var service = CreateComplianceService(
            context,
            tenantRepository: tenantRepository,
            onboardingRepository: onboardingRepository,
            caseRepository: caseRepository);

        var result = await ComplianceEndpoints.GetCompliancePosture(
            service,
            Options(),
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<CompliancePostureResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.Equal(365, body!.Retention.AuditRetentionDays);
        Assert.True(body.Retention.MeetsMinimum);
        Assert.Equal(1, body.Consent.OnboardingRecords);
        Assert.Equal(1, body.Consent.ConsentedRecords);
        Assert.False(body.Consent.BlockedConsent);
        Assert.Equal(2, body.Sla.TotalOpenClaims);
        Assert.Equal(1, body.Sla.AtRisk);
        Assert.Equal(1, body.Sla.Breached);
        Assert.Equal("Uganda", body.DataResidency.TenantCountry);
        Assert.Equal("East Africa", body.DataResidency.TenantRegion);
        Assert.Equal("Approved jurisdiction", body.DataResidency.ResidencyCountry);
        Assert.Equal("approved-region-1", body.DataResidency.AzureRegion);
        Assert.True(body.DataResidency.IsConfigured);
        Assert.True(body.DataResidency.IsDeploymentRegionApproved);
        Assert.Equal("Compliant", body.DataResidency.Status);
        Assert.Equal("Entra", body.Providers.AuthMode);
        Assert.False(body.Providers.SeedDemoData);
    }

    [Fact]
    public async Task EfTenantConfigurationRepository_MissingRecordReturnsUnconfiguredTenant()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfTenantConfigurationRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-production", "workspace-onboarding");

        var configuration = await repository.GetAsync(scope);

        Assert.True(configuration.Scope.Matches(scope));
        Assert.Equal(string.Empty, configuration.InsurerName);
        Assert.Empty(configuration.BusinessLines);
        Assert.Empty(configuration.Channels);
        Assert.Equal("NotConfigured", configuration.ProviderModes.StorageProvider);
        Assert.Equal("NotConfigured", configuration.ProviderModes.AuthMode);
        Assert.False(configuration.ProviderModes.SeedDemoData);
    }

    [Fact]
    public async Task EfDocumentTypeCatalogRepository_SavesAndRetrievesTheCurrentVersionOfACustomType()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new InsuranceIntelligence.Infrastructure.Documents.EfDocumentTypeCatalogRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var now = DateTimeOffset.UtcNow;
        var definition = new DocumentTypeDefinition
        {
            DefinitionId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = 1,
            Key = "SchoolAttendanceLetter",
            CustomerLabel = "School attendance letter",
            Description = "Confirms the student was enrolled.",
            BuiltInType = null,
            Active = true,
            UpdatedByUserId = "admin-1",
            UpdatedAt = now,
            ChangeReason = "New custom document type for school fees cover.",
        };

        await repository.SaveAsync(definition);
        var current = await repository.GetCurrentAsync(scope, "SchoolAttendanceLetter");
        var otherScope = await repository.GetCurrentAsync(TenantScope.Create("tenant-b", "workspace-a"), "SchoolAttendanceLetter");

        Assert.NotNull(current);
        Assert.Equal("School attendance letter", current!.CustomerLabel);
        Assert.Null(otherScope);
    }

    [Fact]
    public async Task EfDocumentTypeCatalogRepository_ListCurrentAsync_ReturnsOnlyTheHighestVersionPerKey()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new InsuranceIntelligence.Infrastructure.Documents.EfDocumentTypeCatalogRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var now = DateTimeOffset.UtcNow;
        var definitionId = Guid.NewGuid();
        await repository.SaveAsync(new DocumentTypeDefinition
        {
            DefinitionId = definitionId,
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = 1,
            Key = "SchoolAttendanceLetter",
            CustomerLabel = "School letter",
            Description = "Original wording.",
            BuiltInType = null,
            Active = true,
            UpdatedByUserId = "admin-1",
            UpdatedAt = now,
            ChangeReason = "First version.",
        });
        await repository.SaveAsync(new DocumentTypeDefinition
        {
            DefinitionId = definitionId,
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = 2,
            Key = "SchoolAttendanceLetter",
            CustomerLabel = "School attendance letter",
            Description = "Clarified wording.",
            BuiltInType = null,
            Active = true,
            UpdatedByUserId = "admin-1",
            UpdatedAt = now.AddMinutes(1),
            ChangeReason = "Clarified the customer-facing label.",
        });

        var current = await repository.ListCurrentAsync(scope);
        var history = await repository.ListVersionsAsync(scope, "SchoolAttendanceLetter");

        var only = Assert.Single(current);
        Assert.Equal(2, only.Version);
        Assert.Equal("School attendance letter", only.CustomerLabel);
        Assert.Equal(2, history.Count);
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

    private static UpdateTenantConfigurationRequest TenantConfigurationRequest() =>
        new(
            "Updated Insurer",
            null,
            "Uganda",
            "East Africa",
            [new BusinessLineConfiguration("Motor", true)],
            [new ChannelConfiguration("Email", true)],
            [new ApprovalThresholdConfiguration("Motor", 1000m, "ClaimsSupervisor")],
            [new ChecklistConfiguration("Motor", ["ClaimForm"])],
            365);

    private static DefaultHttpContext ScopedContext(string role = "Admin", string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["x-demo-role"] = role;
        context.Request.Headers["x-demo-user-id"] = "local-admin";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext MembershipScopedContext(
        TenantScope membershipScope,
        string membershipUserId,
        string spoofedTenantId,
        string spoofedWorkspaceId,
        string spoofedUserId,
        string? auditReason = null)
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            membershipScope.TenantId,
            membershipScope.WorkspaceId,
            membershipUserId,
            ["Admin"]));
        var context = ScopedContext(auditReason: auditReason);
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenantContext)
            .BuildServiceProvider();
        context.Request.Headers["X-Tenant-Id"] = spoofedTenantId;
        context.Request.Headers["X-Workspace-Id"] = spoofedWorkspaceId;
        context.Request.Headers["X-User-Id"] = spoofedUserId;
        context.Request.Headers["x-demo-user-id"] = spoofedUserId;
        return context;
    }

    private static IComplianceQueryService CreateComplianceService(
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
        var permissionService = new PermissionService(tenantContext, new LocalTestPermissionReadRepository());
        var options = Options();
        return new ComplianceQueryService(
            tenantContext,
            permissionService,
            tenantRepository ?? new InMemoryTenantConfigurationRepository(),
            onboardingRepository ?? new RecordingTenantOnboardingRepository([]),
            caseRepository ?? new InMemoryCaseRepository(),
            auditRepository ?? new InMemoryAuditRepository(),
            documentRepository ?? new InMemoryCaseDocumentRepository(),
            actionRepository ?? new InMemoryAgentActionLogRepository(),
            approvalRepository ?? new InMemoryApprovalRepository(),
            updateRepository ?? new InMemoryCustomerUpdateRepository(),
            storageProvider ?? new TestEvidencePackStorageProvider(),
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

    private static async Task<(int StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);
        return (httpContext.Response.StatusCode, body);
    }

    private sealed class EfFixture : IAsyncDisposable
    {
        public InsuranceIntelligenceDbContext DbContext { get; }

        private EfFixture(InsuranceIntelligenceDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public static Task<EfFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
                .UseInMemoryDatabase($"insuranceintelligence-tests-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new InsuranceIntelligenceDbContext(options);
            return Task.FromResult(new EfFixture(dbContext));
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
        }
    }

    private sealed class TestEvidencePackStorageProvider : IEvidencePackStorageProvider
    {
        public Dictionary<string, byte[]> StoredPayloads { get; } = [];

        public async Task<EvidencePackStorageResult> StoreAsync(
            EvidencePackStorageRequest request,
            CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            var objectKey = $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/evidence-packs/{request.CaseId}/{request.FileName}";
            var bytes = memory.ToArray();
            StoredPayloads[objectKey] = bytes;
            return new EvidencePackStorageResult(objectKey, request.ContentType, bytes.Length);
        }

        public Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default) =>
            StoredPayloads.TryGetValue(objectKey, out var bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes))
                : throw new InvalidOperationException($"No stored evidence pack for object key '{objectKey}'.");
    }

    private sealed class RecordingTenantOnboardingRepository(
        IReadOnlyList<TenantOnboardingRecord> records) : ITenantOnboardingRepository
    {
        private readonly List<TenantOnboardingRecord> records = [.. records];

        public Task UpsertAsync(TenantOnboardingRecord record, CancellationToken cancellationToken = default)
        {
            records.RemoveAll(item => string.Equals(item.OnboardingId, record.OnboardingId, StringComparison.OrdinalIgnoreCase));
            records.Add(record);
            return Task.CompletedTask;
        }

        public Task<TenantOnboardingRecord?> GetAsync(string onboardingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(item => string.Equals(item.OnboardingId, onboardingId, StringComparison.OrdinalIgnoreCase)));

        public Task<TenantOnboardingRecord?> GetByStateAsync(string state, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(item => string.Equals(item.State, state, StringComparison.Ordinal)));

        public Task<IReadOnlyList<TenantOnboardingRecord>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantOnboardingRecord>>(records.ToArray());
    }
}
