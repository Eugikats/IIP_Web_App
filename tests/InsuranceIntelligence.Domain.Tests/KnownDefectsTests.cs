using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 2: defects found by the audit, each pinned so it cannot come back.</summary>
public sealed class KnownDefectsTests
{
    [Theory]
    [InlineData("police_letter.pdf", DocumentType.PoliceAbstract)]
    [InlineData("Police-Abstract (2).jpg", DocumentType.PoliceAbstract)]
    [InlineData("claim_form.pdf", DocumentType.ClaimForm)]
    [InlineData("garage estimate.pdf", DocumentType.GarageEstimate)]
    [InlineData("national_id.jpg", DocumentType.CustomerId)]
    [InlineData("driving-licence.png", DocumentType.CustomerId)]
    [InlineData("damage photo 3.jpg", DocumentType.PhotoEvidence)]
    // The audit's example: "accident" and "valid" contain the letters "id" but are not an ID.
    [InlineData("accident_report.pdf", DocumentType.Unknown)]
    [InlineData("valid.pdf", DocumentType.Unknown)]
    [InlineData("paid_receipt_hidden.pdf", DocumentType.Invoice)]
    [InlineData("IMG_2031.jpg", DocumentType.Unknown)]
    [InlineData("", DocumentType.Unknown)]
    [InlineData(null, DocumentType.Unknown)]
    public void FileNameClassifier_MatchesWholeTokensOnly(string? fileName, DocumentType expected) =>
        Assert.Equal(expected, DocumentFileNameClassifier.Classify(fileName));

    [Fact]
    public void CustomerService_HoldsNoAdministrativePermission()
    {
        var customerService = RolePermissionMap.ForRole(PlatformRole.CustomerService);

        foreach (var forbidden in new[]
                 {
                     Permission.ManageProducts, Permission.ManageTenantOnboarding, Permission.ManageTenantSettings,
                     Permission.ManageAgents, Permission.ManageAgentWorkflows, Permission.ViewBilling,
                     Permission.ConfigureIntegrations, Permission.ApproveSettlementWorkflowStep,
                     Permission.ExportEvidencePack, Permission.ViewAuditLog, Permission.ManageKnowledgeBase,
                     Permission.OverrideAiRecommendation,
                 })
        {
            Assert.DoesNotContain(forbidden, customerService);
        }

        Assert.Contains(Permission.CreateClaim, customerService);
        Assert.Contains(Permission.SendCustomerUpdate, customerService);
    }

    [Fact]
    public void OnlyPublisherSupportCanManageTenantOnboarding()
    {
        var holders = Enum.GetValues<PlatformRole>()
            .Where(role => RolePermissionMap.HasPermission(role, Permission.ManageTenantOnboarding))
            .ToArray();

        Assert.Equal(new[] { PlatformRole.PublisherSupport }, holders);
    }

    [Fact]
    public void Broker_CannotSeeOngoingClaims()
    {
        Assert.DoesNotContain(Permission.ViewAssignedClaims, RolePermissionMap.ForRole(PlatformRole.Broker));
        Assert.DoesNotContain(Permission.ViewAllClaims, RolePermissionMap.ForRole(PlatformRole.Broker));
    }

    [Fact]
    public void ThereIsOnlyOneRoleVocabulary()
    {
        var domain = typeof(PlatformRole).Assembly;

        Assert.Null(domain.GetType("InsuranceIntelligence.Domain.Users.UserRole"));
        var roleProperty = typeof(InsuranceIntelligence.Domain.Users.InsuranceUser).GetProperty("Role");
        Assert.Equal(typeof(PlatformRole), roleProperty!.PropertyType);
    }

    [Fact]
    public void CustomerAssistant_IsLabelledModelBacked_BecauseItsRepliesComeFromAModel()
    {
        var kato = AgentCatalog.Find("customer-assistant");

        Assert.NotNull(kato);
        Assert.Equal(AgentImplementationKind.ModelBacked, kato!.ImplementationKind);
    }

    [Fact]
    public void AgentCatalog_HasNoDuplicateKeys_AndEveryKindIsNamed()
    {
        var keys = AgentCatalog.Agents.Select(agent => agent.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(AgentCatalog.Agents, agent => Assert.True(Enum.IsDefined(agent.ImplementationKind)));
    }

    [Fact]
    public void Completeness_CountsTheCustomersSlot_EvenWhenTheClassifierDisagrees()
    {
        var document = Document(documentTypeKey: "PoliceAbstract", classified: DocumentType.Invoice, DocumentProcessingStatus.Processed);

        Assert.True(document.CountsTowardsCompleteness);
        Assert.True(document.DisagreesWithClassifier);
        Assert.Equal("PoliceAbstract", document.EffectiveKey);
    }

    [Theory]
    [InlineData(DocumentProcessingStatus.Uploaded, true)]
    [InlineData(DocumentProcessingStatus.Processing, true)]
    [InlineData(DocumentProcessingStatus.Processed, true)]
    [InlineData(DocumentProcessingStatus.ExtractionApproved, true)]
    [InlineData(DocumentProcessingStatus.Failed, false)]
    [InlineData(DocumentProcessingStatus.Rejected, false)]
    public void Completeness_CountsStoredFiles_ButNotFailedOrRejectedOnes(DocumentProcessingStatus status, bool counts) =>
        Assert.Equal(counts, Document("ClaimForm", DocumentType.ClaimForm, status).CountsTowardsCompleteness);

    [Fact]
    public void Completeness_IgnoresSupersededAndUnknownFiles()
    {
        Assert.False((Document("ClaimForm", DocumentType.ClaimForm, DocumentProcessingStatus.Processed) with { IsSuperseded = true }).CountsTowardsCompleteness);
        Assert.False(Document(null, DocumentType.Unknown, DocumentProcessingStatus.Processed).CountsTowardsCompleteness);
    }

    [Fact]
    public void NoDisagreement_WhenTheCustomerDidNotChooseASlot_OrTheClassifierIsUnsure()
    {
        Assert.False(Document(null, DocumentType.Invoice, DocumentProcessingStatus.Processed).DisagreesWithClassifier);
        Assert.False(Document("PoliceAbstract", DocumentType.Unknown, DocumentProcessingStatus.Processed).DisagreesWithClassifier);
        Assert.False(Document("PoliceAbstract", DocumentType.PoliceAbstract, DocumentProcessingStatus.Processed).DisagreesWithClassifier);
    }

    private static CaseDocument Document(string? documentTypeKey, DocumentType classified, DocumentProcessingStatus status) =>
        new()
        {
            DocumentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("t", "w"),
            OriginalFileName = "f.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 10,
            ObjectKey = "t/w/f",
            DocumentType = classified,
            DocumentTypeKey = documentTypeKey,
            ProcessingStatus = status,
            Classification = new DocumentClassificationResult(classified, 0.9m, "Test", DateTimeOffset.UtcNow),
            Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "Test", DateTimeOffset.UtcNow, false, null, null),
            ValidationWarnings = [],
            UploadedByUserId = "u",
            UploadedAt = DateTimeOffset.UtcNow,
            CorrelationId = "c",
        };
}
