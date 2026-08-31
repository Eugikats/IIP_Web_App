using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class Phase5DomainTests
{
    [Fact]
    public void RolePermissionMap_GrantsAndDeniesExpectedPermissions()
    {
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.Admin, Permission.ManageTenantSettings));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsSupervisor, Permission.ViewAuditLog));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsSupervisor, Permission.ExportEvidencePack));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.Compliance, Permission.ExportEvidencePack));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.CustomerService, Permission.ManageCustomerCommunications));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.CustomerService, Permission.SendCustomerUpdate));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.PublisherSupport, Permission.ViewAuditLog));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.PublisherSupport, Permission.ExportEvidencePack));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ViewAuditLog));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ExportEvidencePack));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ViewAssignedClaims));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ManageCustomerCommunications));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Broker, Permission.SendCustomerUpdate));
    }

    [Fact]
    public void TenantConfiguration_ValidatesRetentionAndChecklist()
    {
        var invalid = TenantConfiguration.Demo() with
        {
            InsurerName = "",
            Country = "",
            Region = "",
            LogoUrl = "javascript:alert(1)",
            AuditRetentionDays = 10,
            Checklists = [new ChecklistConfiguration("", [])]
        };

        var errors = invalid.Validate();

        Assert.Contains(errors, error => error.Contains("Insurer name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Country", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Region", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Logo URL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Audit retention", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("required document", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuditPack_RemovesUnsafeAssistantContent()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var claimCase = Case(scope);
        var safeAudit = AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), "user", "corr");
        var unsafeAction = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = claimCase.CaseId,
            UserId = "user",
            AgentName = "PolicyQnA",
            AssistantMode = AssistantMode.PolicyQnA,
            InputSummary = "raw prompt should not be exported",
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = "system message should not be exported",
            ConfidenceScore = AiConfidenceScore.Create(0.9m),
            RiskFlag = "needs_review",
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        };

        var pack = AuditPack.Build(claimCase, [safeAudit], [], [unsafeAction], [], []);

        Assert.Empty(pack.AssistantActions);
        Assert.True(pack.IsCustomerSafe());
    }

    [Fact]
    public void AssistantAuditEvents_RecordSafeMetadataWithoutPromptOutputOrToolTrace()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var rawPrompt = "RAW PROMPT: include all claim notes and hidden tool trace.";
        var rawOutput = "RAW MODEL OUTPUT: deny the claim and expose internal reasoning.";
        var rawExcerpt = "Raw policy excerpt with customer email jane.customer@example.com.";
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = Guid.NewGuid(),
            UserId = "user",
            AgentName = "CommandCenter",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = rawPrompt,
            RetrievedSources =
            [
                new SourceCitation(
                    Guid.NewGuid(),
                    "chunk-raw-tool-trace",
                    "Motor Policy",
                    "Motor",
                    "v1",
                    rawExcerpt,
                    "Approved")
            ],
            ModelOutput = rawOutput,
            ConfidenceScore = AiConfidenceScore.Create(0.72m),
            RiskFlag = "needs_review",
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-ai-governance",
            ModelProvider = "AzureOpenAI",
            ModelDeployment = "gpt-4.1",
            PromptVersion = "command-center-query-v1"
        };

        var commandCenterAudit = AuditEvent.CommandCenterAssistantAnswered(actionLog);
        var policyAudit = AuditEvent.AssistantPolicyQuestionAnswered(actionLog);

        AssertAssistantAuditIsSafe(commandCenterAudit);
        AssertAssistantAuditIsSafe(policyAudit);
        Assert.Empty(commandCenterAudit.EvidenceIds);
        Assert.Contains("chunk-raw-tool-trace", policyAudit.EvidenceIds);

        void AssertAssistantAuditIsSafe(AuditEvent auditEvent)
        {
            Assert.Equal("AzureOpenAI", auditEvent.ModelProvider);
            Assert.Equal(0.72m, auditEvent.Confidence);
            Assert.Equal("needs_review", auditEvent.ValidationResult);
            Assert.Equal("corr-ai-governance", auditEvent.CorrelationId);
            Assert.Null(auditEvent.AuditReason);
            Assert.DoesNotContain(rawPrompt, AuditText(auditEvent), StringComparison.Ordinal);
            Assert.DoesNotContain(rawOutput, AuditText(auditEvent), StringComparison.Ordinal);
            Assert.DoesNotContain(rawExcerpt, AuditText(auditEvent), StringComparison.Ordinal);
        }
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

    private static string AuditText(AuditEvent auditEvent) =>
        string.Join(
            "|",
            [
                auditEvent.ActionName,
                auditEvent.BeforeStatus ?? string.Empty,
                auditEvent.AfterStatus ?? string.Empty,
                auditEvent.ValidationResult,
                auditEvent.ModelProvider ?? string.Empty,
                auditEvent.AuditReason ?? string.Empty,
                .. auditEvent.EvidenceIds
            ]);
}
