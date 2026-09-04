using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class DocumentCompletenessTests
{
    [Fact]
    public void MotorChecklistRequiresClaimFormPoliceAbstractAndPhotoEvidence()
    {
        var rules = DocumentChecklistRules.ForProductLine("Motor");

        Assert.Equal(
            [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence],
            rules.RequiredDocuments);
    }

    [Fact]
    public void InjuryChecklistRequiresMedicalReportOnGovernedPath()
    {
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = "Bodily injury",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Injury reported",
            Status = CaseStatus.Escalated,
            CompletenessScore = 0m,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-doc-injury"
        };

        var rules = DocumentChecklistRules.ForCase(claimCase);

        Assert.Contains(DocumentType.MedicalReport, rules.RequiredDocuments);
        Assert.Contains(DocumentType.PoliceAbstract, rules.RequiredDocuments);
    }

    [Fact]
    public void TheftChecklistRequiresCustomerIdOnGovernedPath()
    {
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = "Theft",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Vehicle stolen",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-doc-theft"
        };

        var rules = DocumentChecklistRules.ForCase(claimCase);

        Assert.Contains(DocumentType.CustomerId, rules.RequiredDocuments);
        Assert.Contains(DocumentType.PoliceAbstract, rules.RequiredDocuments);
    }

    [Fact]
    public void MedicalChecklistRequiresClaimFormMedicalReportAndInvoice()
    {
        var rules = DocumentChecklistRules.ForProductLine("Medical");

        Assert.Equal(
            [DocumentType.ClaimForm, DocumentType.MedicalReport, DocumentType.Invoice],
            rules.RequiredDocuments);
    }

    [Fact]
    public void MedicalCase_UsesMedicalChecklist_EvenThoughClaimTypeTextContainsMedical()
    {
        // WO-15: ClaimTypeClassifier's motor taxonomy previously ran unconditionally regardless of
        // product line — a Medical claim's own claim-type text (e.g. "Medical reimbursement")
        // matched the InjuryFatality keyword "medical" and pulled in PoliceAbstract/PhotoEvidence,
        // documents that make no sense for a medical claim. Product line now gates that taxonomy.
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-2001",
            ProductLine = "Medical",
            ClaimType = "Medical reimbursement",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Outpatient treatment reimbursement",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-doc-medical"
        };

        var rules = DocumentChecklistRules.ForCase(claimCase);

        Assert.Equal(
            [DocumentType.ClaimForm, DocumentType.MedicalReport, DocumentType.Invoice],
            rules.RequiredDocuments);
        Assert.DoesNotContain(DocumentType.PoliceAbstract, rules.RequiredDocuments);
        Assert.DoesNotContain(DocumentType.PhotoEvidence, rules.RequiredDocuments);
    }

    [Fact]
    public void CompletenessScoreUpdatesFromRequiredDocumentsReceived()
    {
        var score = CompletenessScore.Calculate(
            [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence],
            [DocumentType.ClaimForm, DocumentType.PhotoEvidence]);

        Assert.Equal(66.67m, score.Percentage);
        Assert.Equal(2, score.RequiredReceived);
        Assert.Equal(3, score.RequiredTotal);
    }

    [Fact]
    public void UnknownDocumentTypeDoesNotSatisfyRequiredChecklist()
    {
        var score = CompletenessScore.Calculate(
            [DocumentType.ClaimForm],
            [DocumentType.Unknown]);

        Assert.Equal(0m, score.Percentage);
        Assert.Equal(0, score.RequiredReceived);
        Assert.Equal(1, score.RequiredTotal);
    }

    [Fact]
    public void DocumentAuditEventShapeIsValid()
    {
        var document = ValidDocument();
        var auditEvent = AuditEvent.DocumentProcessed(document, "corr-doc-001");

        Assert.Equal("document.processed", auditEvent.ActionName);
        Assert.Equal(document.Scope, auditEvent.Scope);
        Assert.Equal(document.CaseId, auditEvent.CaseId);
        Assert.Equal(document.DocumentId.ToString(), auditEvent.DocumentId);
        Assert.Equal("Mock", auditEvent.ModelProvider);
        Assert.Equal(document.Classification.ConfidenceScore, auditEvent.Confidence);
        Assert.Equal("corr-doc-001", auditEvent.CorrelationId);
    }

    private static CaseDocument ValidDocument()
    {
        var now = DateTimeOffset.UtcNow;

        return new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            OriginalFileName = "claim-form.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 128,
            ObjectKey = "tenant-demo/workspace-claims/case/document.pdf",
            DocumentType = DocumentType.ClaimForm,
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(DocumentType.ClaimForm, 0.92m, "Mock", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string> { ["mockDocumentType"] = "ClaimForm" },
                new Dictionary<string, decimal> { ["mockDocumentType"] = 0.92m },
                "Mock",
                now,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = "user-claims-1",
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = "corr-doc-001"
        };
    }
}
